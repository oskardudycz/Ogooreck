using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed partial class TestRunTool(CollectedSpans collected, TestRunner runner, SourceFiles sources)
{
    private const int OutputTailLines = 30;
    private const int SlowestTestsShown = 10;

    [McpServerTool(Name = "ogooreck_test_run", Title = "Run tests", ReadOnly = false, Destructive = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Runs the tests of a .NET test project and collects the spans of the tests, Ogooreck and the tested app. Returns the test counts, the target frameworks the tests ran on, the code coverage of a library when asked, and each failed test with its message and the source location of its Ogooreck runs. Every project runs with dotnet test; a Microsoft.Testing.Platform project in dotnet test's Microsoft.Testing.Platform mode, opted into for this run only, without changing the repository's global.json. As with dotnet test, a project with several target frameworks runs on each of them unless --framework picks one. Each test that ran also gets a span, named after the test, timed from the test results and with its target framework in test.framework, so ogooreck_concurrent_spans shows what ran during any test, also one that doesn't use Ogooreck. Then call ogooreck_test_spans, ogooreck_find_spans, ogooreck_concurrent_spans, ogooreck_slowest_steps or ogooreck_export_spans. The run sets OTEL_EXPORTER_OTLP_ENDPOINT, which turns on the test project's tracing, and that tracing may listen to every ActivitySource. Code that behaves differently when its ActivitySource has a listener can then pass here and fail in a plain dotnet test, so if a test passes here but fails in CI, run it with plain dotnet test too.")]
    public async Task<TestRunResult> TestRun(
        [Description("Path to the test project (.csproj or .fsproj) or its folder, relative to the working directory.")] string project,
        [Description("Extra arguments for dotnet test, passed as given, for example [\"--framework\", \"net11.0\", \"--filter\", \"FullyQualifiedName~CartTests\"] for a VSTest project, or [\"--framework\", \"net11.0\", \"--treenode-filter\", \"/*/*/CartTests/*\"] for a Microsoft.Testing.Platform project. Run only the tests you need. Pass \"--no-build\" when the project is already built and unchanged, to skip its build and restore.")] string[]? arguments = null,
        [Description("How many times to run the tests at most. The runs stop at the first one that has a failed test or ends with an error, and its spans are kept. Use it to catch a flaky test.")] int repeat = 1,
        [Description("Seconds that a test may run before the run is aborted, for a VSTest project. The result then names the tests that were running and the Ogooreck spans whose parent never ended. Use it when a test hangs.")] int? hangTimeout = null,
        [Description("Name of a library assembly, for example \"Ogooreck\", to collect its code coverage with Microsoft's collector: Microsoft.NET.Test.Sdk brings it to a VSTest project, Microsoft.Testing.Extensions.CodeCoverage to a Microsoft.Testing.Platform project. The result then has the covered and valid lines and branches, each uncovered line or range of lines, and each line with partly covered branches, leaving out the code generated under obj/, as the build's coverage check does. Lines.Valid is 0 when the tests never loaded the library. Use it to find the code that no test reaches.")] string? coverage = null,
        CancellationToken ct = default)
    {
        var path = Path.GetFullPath(project);
        sources.RootAt(path);
        using var activity = McpTracing.Source.StartActivity("test_run")!;
        activity.SetTag("ogooreck.mcp.project", path);
        activity.SetTag("ogooreck.mcp.arguments", arguments ?? []);

        for (var attempt = 1; ; attempt++)
        {
            var result = await RunOnce(path, arguments ?? [], hangTimeout, coverage, attempt, ct);
            if (result.FailedTests.Length == 0 && result.ExitCode == 0 && attempt < repeat)
                continue;

            activity.SetTag("process.exit.code", result.ExitCode);
            activity.SetTag("ogooreck.mcp.attempts", attempt);
            if (result.FailedTests.Length > 0 || result.ExitCode != 0)
                activity.SetStatus(ActivityStatusCode.Error,
                    $"{result.FailedTests.Length} failed tests, exit code {result.ExitCode}");
            return result;
        }
    }

    private async Task<TestRunResult> RunOnce(string path, string[] arguments, int? hangTimeout, string? coverage,
        int attempt, CancellationToken ct)
    {
        collected.Replace([]);
        var results = Directory.CreateTempSubdirectory("ogooreck-mcp-");
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var process = await runner.Run(path, arguments, results.FullName, hangTimeout, coverage is not null, ct);
            var tests = TrxResults.Read(results.FullName);
            collected.Add(tests.Where(test => !test.Skipped).Select(SpanOf).ToArray());
            var tree = new SpanTree(collected.All());
            var failedRuns = tree.Runs.Where(run => run.Error is not null).ToLookup(SpanTree.TestOf);
            var failedTests = tests.Where(test => test.Failed)
                .Select(test => new FailedTest(test.Name, test.Message, test.StackTrace,
                    failedRuns[test.Name].Select(run => new FailedRun(sources.Location(run), run.Error!)).ToArray()))
                .ToArray();
            var buildErrors = BuildErrorsIn(process.Output);

            return new TestRunResult(
                process.ExitCode,
                process.Runner,
                stopwatch.Elapsed.TotalSeconds,
                attempt,
                new TestCounts(tests.Count, tests.Count(test => test.Passed), tests.Count(test => test.Failed),
                    tests.Count(test => test.Skipped)),
                tests.Select(test => test.Framework).Distinct().Order().ToArray(),
                failedTests,
                tests.Where(test => !test.Skipped).OrderByDescending(test => test.Duration).Take(SlowestTestsShown)
                    .Select(test => new SlowTest(test.Name, test.Framework, test.Duration.TotalSeconds)).ToArray(),
                buildErrors,
                tree.Spans.Count,
                process.ExitCode != 0 && failedTests.Length == 0 && buildErrors.Length == 0
                    ? process.Output.Where(line => line.Length > 0).TakeLast(OutputTailLines).ToArray()
                    : [],
                RunningWhenAborted(process.Output),
                tree.OgooreckSpansUnderSpansThatNeverEnded()
                    .Select(children => new NeverEndedSpan(children.First().TraceId, children.Key,
                        children.Select(child => child.Name).ToArray()))
                    .ToArray(),
                coverage is null ? null : CoverageReports.Read(results.FullName, coverage, sources));
        }
        finally
        {
            results.Delete(true);
        }
    }

    private static CollectedSpan SpanOf(TrxResult test) =>
        new("Ogooreck.Mcp", "", McpTracing.Source.Name, ActivityTraceId.CreateRandom().ToHexString(),
            ActivitySpanId.CreateRandom().ToHexString(), null, test.Name, "internal", test.Start, test.Start + test.Duration,
            null,
            new Dictionary<string, JsonNode>
            {
                ["test.case.name"] = test.Name,
                ["test.framework"] = test.Framework,
                ["test.case.result.status"] = test.Passed ? "pass" : "fail"
            });

    private BuildError[] BuildErrorsIn(string[] output) =>
        output.Select(line => BuildErrorLine().Match(line)).Where(match => match.Success)
            .Select(match => new BuildError(
                $"{Path.GetRelativePath(sources.Root, match.Groups["file"].Value)}:{match.Groups["line"].Value}",
                match.Groups["code"].Value, match.Groups["message"].Value))
            .Distinct()
            .ToArray();

    [GeneratedRegex(@"^(?<file>[^(]+)\((?<line>\d+),\d+\): error (?<code>\w+): (?<message>.*?)(?: \[[^\]]+\])?$")]
    private static partial Regex BuildErrorLine();

    private static string[] RunningWhenAborted(string[] output) =>
        output.SkipWhile(line => !line.Contains("running when the crash occurred"))
            .Skip(1)
            .TakeWhile(line => !line.Contains("may, or may not be the source of the crash"))
            .Where(line => line.Length > 0)
            .ToArray();
}

public sealed record TestRunResult(int ExitCode, string Runner, double Seconds, int Attempts, TestCounts Tests, string[] Frameworks,
    FailedTest[] FailedTests, SlowTest[] SlowestTests, BuildError[] BuildErrors, int Spans, string[] OutputTail, string[] RunningWhenAborted, NeverEndedSpan[] NeverEnded,
    Coverage? Coverage);

public sealed record TestCounts(int Total, int Passed, int Failed, int Skipped);

public sealed record FailedTest(string Name, string? Message, string? StackTrace, FailedRun[] Runs);

public sealed record FailedRun(string Location, string Error);

public sealed record SlowTest(string Name, string Framework, double Seconds);

public sealed record BuildError(string Location, string Code, string Message);

public sealed record NeverEndedSpan(string TraceId, string SpanId, string[] EndedChildren);

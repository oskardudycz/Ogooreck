using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class TestRunTool(CollectedSpans collected, TestRunner runner, SourceFiles sources)
{
    private const int OutputTailLines = 30;

    [McpServerTool(Name = "ogooreck_test_run", Title = "Run tests", ReadOnly = false, Destructive = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Runs the tests of a .NET test project and collects the spans of the tests, Ogooreck and the tested app. Returns the test counts and each failed test with its message and the source location of its Ogooreck runs. A VSTest project runs with dotnet test, a Microsoft.Testing.Platform project with dotnet run. Then call ogooreck_test_spans, ogooreck_find_spans, ogooreck_concurrent_spans, ogooreck_slowest_steps or ogooreck_export_spans. The run sets OTEL_EXPORTER_OTLP_ENDPOINT, which turns on the test project's tracing, and that tracing may listen to every ActivitySource. Code that behaves differently when its ActivitySource has a listener can then pass here and fail in a plain dotnet test, so if a test passes here but fails in CI, run it with plain dotnet test too.")]
    public async Task<TestRunResult> TestRun(
        [Description("Path to the test project (.csproj or .fsproj) or its folder, relative to the working directory.")] string project,
        [Description("Extra arguments, passed as given. For VSTest projects they go to dotnet test, for example [\"--filter\", \"FullyQualifiedName~CartTests\"]; for Microsoft.Testing.Platform projects they go to the test application, for example [\"--treenode-filter\", \"/*/*/CartTests/*\"]. Run only the tests you need.")] string[]? arguments = null,
        [Description("How many times to run the tests at most. The runs stop at the first one that has a failed test or ends with an error, and its spans are kept. Use it to catch a flaky test.")] int repeat = 1,
        [Description("Seconds that a test may run before the run is aborted, for a VSTest project. The result then names the tests that were running and the Ogooreck spans whose parent never ended. Use it when a test hangs.")] int? hangTimeout = null,
        CancellationToken ct = default)
    {
        var path = Path.GetFullPath(project);
        sources.RootAt(path);
        using var activity = McpTracing.Source.StartActivity("test_run");
        activity?.SetTag("ogooreck.mcp.project", path);
        activity?.SetTag("ogooreck.mcp.arguments", arguments ?? []);

        for (var attempt = 1; ; attempt++)
        {
            var result = await RunOnce(path, arguments ?? [], hangTimeout, attempt, ct);
            if (result.FailedTests.Length == 0 && result.ExitCode == 0 && attempt < repeat)
                continue;

            activity?.SetTag("process.exit.code", result.ExitCode);
            activity?.SetTag("ogooreck.mcp.attempts", attempt);
            if (result.FailedTests.Length > 0 || result.ExitCode != 0)
                activity?.SetStatus(ActivityStatusCode.Error,
                    $"{result.FailedTests.Length} failed tests, exit code {result.ExitCode}");
            return result;
        }
    }

    private async Task<TestRunResult> RunOnce(string path, string[] arguments, int? hangTimeout, int attempt,
        CancellationToken ct)
    {
        collected.Replace([]);
        var results = Directory.CreateTempSubdirectory("ogooreck-mcp-");
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var process = await runner.Run(path, arguments, results.FullName, hangTimeout, ct);
            var tests = TrxResults.Read(results.FullName);
            var tree = new SpanTree(collected.All());
            var failedRuns = tree.Runs.Where(run => run.Error is not null).ToLookup(SpanTree.TestOf);
            var failedTests = tests.Where(test => test.Failed)
                .Select(test => new FailedTest(test.Name, test.Message, test.StackTrace,
                    failedRuns[test.Name].Select(run => new FailedRun(sources.Location(run), run.Error!)).ToArray()))
                .ToArray();

            return new TestRunResult(
                process.ExitCode,
                process.Runner,
                stopwatch.Elapsed.TotalSeconds,
                attempt,
                new TestCounts(tests.Count, tests.Count(test => test.Passed), tests.Count(test => test.Failed),
                    tests.Count(test => test.Skipped)),
                failedTests,
                tree.Spans.Count,
                process.ExitCode != 0 && failedTests.Length == 0
                    ? process.Output.Where(line => line.Length > 0).TakeLast(OutputTailLines).ToArray()
                    : [],
                RunningWhenAborted(process.Output),
                tree.OgooreckSpansUnderSpansThatNeverEnded()
                    .Select(children => new NeverEndedSpan(children.First().TraceId, children.Key,
                        children.Select(child => child.Name).ToArray()))
                    .ToArray());
        }
        finally
        {
            results.Delete(true);
        }
    }

    private static string[] RunningWhenAborted(string[] output) =>
        output.SkipWhile(line => !line.Contains("running when the crash occurred"))
            .Skip(1)
            .TakeWhile(line => !line.Contains("may, or may not be the source of the crash"))
            .Where(line => line.Length > 0)
            .ToArray();
}

public sealed record TestRunResult(int ExitCode, string Runner, double Seconds, int Attempts, TestCounts Tests,
    FailedTest[] FailedTests, int Spans, string[] OutputTail, string[] RunningWhenAborted, NeverEndedSpan[] NeverEnded);

public sealed record TestCounts(int Total, int Passed, int Failed, int Skipped);

public sealed record FailedTest(string Name, string? Message, string? StackTrace, FailedRun[] Runs);

public sealed record FailedRun(string Location, string Error);

public sealed record NeverEndedSpan(string TraceId, string SpanId, string[] EndedChildren);

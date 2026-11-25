using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class TestRunTools(CollectedSpans collected, TestRunner runner, SourceFiles sources)
{
    private const int OutputTailLines = 30;

    [McpServerTool(Name = "ogooreck_test_run", Title = "Run tests", ReadOnly = false, Destructive = false, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Runs the tests of a .NET test project and collects the spans of the tests, Ogooreck and the tested app. Returns the test counts and each failed test with its message and the source location of its Ogooreck runs. A VSTest project runs with dotnet test, a Microsoft.Testing.Platform project with dotnet run. Then call ogooreck_find_spans, ogooreck_slowest_steps or ogooreck_export_spans.")]
    public async Task<TestRunResult> TestRun(
        [Description("Path to the test project (.csproj or .fsproj) or its folder, relative to the working directory.")] string project,
        [Description("Extra arguments, passed as given. For VSTest projects they go to dotnet test, for example [\"--filter\", \"FullyQualifiedName~CartTests\"]; for Microsoft.Testing.Platform projects they go to the test application, for example [\"--treenode-filter\", \"/*/*/CartTests/*\"]. Run only the tests you need.")] string[]? arguments = null,
        CancellationToken ct = default)
    {
        collected.Replace([]);
        var path = Path.GetFullPath(project);
        sources.RootAt(path);
        using var activity = McpTracing.Source.StartActivity("test_run");
        activity?.SetTag("ogooreck.mcp.project", path);
        activity?.SetTag("ogooreck.mcp.arguments", arguments ?? []);

        var results = Directory.CreateTempSubdirectory("ogooreck-mcp-");
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var process = await runner.Run(path, arguments ?? [], results.FullName, ct);
            activity?.SetTag("process.exit.code", process.ExitCode);
            var tests = TrxResults.Read(results.FullName);
            var tree = new SpanTree(collected.All());
            var failedRuns = tree.Runs.Where(run => run.Error is not null).ToLookup(SpanTree.TestOf);
            var failedTests = tests.Where(test => test.Failed)
                .Select(test => new FailedTest(test.Name, test.Message, test.StackTrace,
                    failedRuns[test.Name].Select(run => new FailedRun(sources.Location(run), run.Error!)).ToArray()))
                .ToArray();
            if (failedTests.Length > 0 || process.ExitCode != 0)
                activity?.SetStatus(ActivityStatusCode.Error, $"{failedTests.Length} failed tests, exit code {process.ExitCode}");

            return new TestRunResult(
                process.ExitCode,
                process.Runner,
                stopwatch.Elapsed.TotalSeconds,
                new TestCounts(tests.Count, tests.Count(test => test.Passed), tests.Count(test => test.Failed),
                    tests.Count(test => test.Skipped)),
                failedTests,
                tree.Spans.Count,
                process.ExitCode != 0 && failedTests.Length == 0
                    ? process.Output.Where(line => line.Length > 0).TakeLast(OutputTailLines).ToArray()
                    : []);
        }
        finally
        {
            results.Delete(true);
        }
    }
}

public sealed record TestRunResult(int ExitCode, string Runner, double Seconds, TestCounts Tests, FailedTest[] FailedTests,
    int Spans, string[] OutputTail);

public sealed record TestCounts(int Total, int Passed, int Failed, int Skipped);

public sealed record FailedTest(string Name, string? Message, string? StackTrace, FailedRun[] Runs);

public sealed record FailedRun(string Location, string Error);

public sealed record SlowSteps(SlowStep[] Steps, string? File);

public sealed record SlowStep(string Name, string? Test, string Location, double Milliseconds, string? Error,
    SpanNode[] WaitedOn, SpanNode[] App);

public sealed record SpanNode(string Name, string Scope, string Service, double Milliseconds, string? Error, SpanNode[] Children);

public sealed record FoundSpans(int Total, FoundSpan[] Spans, string? File);

public sealed record FoundSpan(string Name, string Scope, string Service, string[] Path, string? Test, string? Location,
    double Milliseconds, string? Error, IReadOnlyDictionary<string, string> Attributes);

using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.Running;

public class TestRunTests(McpServer server, SharedRuns runs): IClassFixture<McpServer>
{
    [Fact]
    public async Task TestRun_NamesEachFailedTestWithTheLocationOfItsRun()
    {
        var run = (await runs.Of(Fixture)).Result;

        Assert.Equal(new TestCounts(5, 2, 1, 2), run.Tests);
        Assert.Empty(run.NeverEnded);
        var failed = Assert.Single(run.FailedTests);
        Assert.Equal(FailingTest, failed.Name);
        Assert.Contains("expected <200 OK>, but found <201 Created>", failed.Message);
        var failedRun = Assert.Single(failed.Runs);
        Assert.Equal(FailingWhen, failedRun.Location);
    }

    [Fact]
    public async Task TestRun_PassesTheArgumentsToDotnetTest()
    {
        var run = (await runs.Of(Fixture, "--filter", "FullyQualifiedName~RespondsWithCreated")).Result;

        Assert.Equal(new TestCounts(1, 1, 0, 0), run.Tests);
        Assert.Empty(run.FailedTests);
        Assert.Empty(run.OutputTail);
        Assert.Empty(run.RunningWhenAborted);
        Assert.Empty(run.NeverEnded);
        Assert.Null(run.Coverage);
    }

    [Fact]
    public async Task TestRun_WithCoverage_ReturnsTheLinesAndBranchesOfTheLibraryThatNoTestRan()
    {
        var coverage = (await runs.Covering("Ogooreck.Sample.Api", Fixture, "--filter", "FullyQualifiedName~RespondsWithCreated")).Result.Coverage!;

        Assert.Equal("Ogooreck.Sample.Api", coverage.Library);
        Assert.InRange(coverage.Lines.Covered, 1, coverage.Lines.Valid - 1);
        Assert.Equal(coverage.Lines.Valid - coverage.Lines.Covered, coverage.UncoveredLines.Sum(LineCount));
        Assert.All(coverage.UncoveredLines, lines => Assert.Matches(@"^src/dotnet/Ogooreck\.Sample\.Api/.+\.cs:\d+(-\d+)?$", lines));
        Assert.InRange(coverage.Branches.Covered, 0, coverage.Branches.Valid - 1);
        Assert.NotEmpty(coverage.PartlyCoveredBranches);
        Assert.All(coverage.PartlyCoveredBranches, branch => Assert.Matches(@"^src/dotnet/Ogooreck\.Sample\.Api/.+\.cs:\d+ \(\d+/\d+\)$", branch));
    }

    [Fact]
    public async Task TestRun_WithCoverage_OfATestingPlatformProject_ReturnsTheCoverageOfTheLibrary()
    {
        var coverage = (await runs.Covering("Ogooreck", TestingPlatformProject, "--framework", "net11.0")).Result.Coverage!;

        Assert.Equal("Ogooreck", coverage.Library);
        Assert.InRange(coverage.Lines.Covered, 1, coverage.Lines.Valid - 1);
    }

    [Fact]
    public async Task TestRun_WithCoverage_OfALibraryTheTestsNeverLoaded_HasNoLines()
    {
        var coverage = (await runs.Covering("Ogooreck.Missing", Fixture, "--no-such-option")).Result.Coverage!;

        Assert.Equal(new CoverageCounts(0, 0), coverage.Lines);
        Assert.Empty(coverage.UncoveredLines);
    }

    private static int LineCount(string lines)
    {
        var range = lines[(lines.LastIndexOf(':') + 1)..].Split('-').Select(int.Parse).ToArray();
        return range[^1] - range[0] + 1;
    }

    [Fact]
    public async Task TestRun_CollectsTheSpansOfTheTestsWithoutTheSpansOfTheDotnetCli()
    {
        var server = (await runs.Of(Fixture, "--filter", "FullyQualifiedName~RespondsWithCreated")).Server;

        var spans = await server.TestRunSpans();
        Assert.NotEmpty(spans);
        Assert.DoesNotContain(spans, span => span.Service == "dotnet-cli");
    }

    [Theory]
    [InlineData(Fixture, new[] { "--filter", "FullyQualifiedName~RespondsWithCreated" }, new[] { "dotnet msbuild", "dotnet test" })]
    [InlineData(TestingPlatformProject, new[] { "--framework", "net11.0" }, new[] { "dotnet msbuild", "dotnet --version", "dotnet test" })]
    public async Task TestRun_HasASpanForEachDotnetProcessItStarted(string project, string[] arguments, string[] processes)
    {
        var server = (await runs.Of(project, arguments)).Server;

        var started = (await server.TestRunSpans()).Where(span => span.Attributes.ContainsKey("process.command_args")).OrderBy(span => span.Start).ToArray();
        Assert.Equal(processes, started.Select(span => span.Name));
        Assert.All(started, span =>
        {
            Assert.Equal("0", span.Attribute("process.exit.code"));
            Assert.Equal("dotnet", span.Attributes["process.command_args"][0]!.GetValue<string>());
            Assert.True(span.Duration > TimeSpan.Zero);
        });
        Assert.Contains(arguments[^1], started[^1].Attributes["process.command_args"]!.AsArray().Select(item => item!.GetValue<string>()));
    }

    [Fact]
    public async Task TestRun_HasASpanForEachTestThatRan_WithinTheDotnetTestProcess()
    {
        var (server, run) = await runs.Of(Fixture);

        var spans = await server.TestRunSpans();
        var tests = spans.Where(span => span.Attributes.ContainsKey("test.case.name")).ToArray();
        Assert.Equal(run.Tests.Passed + run.Tests.Failed, tests.Length);
        Assert.All(tests, test => Assert.Equal(test.Name, test.Attribute("test.case.name")));
        var failing = Assert.Single(tests, test => test.Name == FailingTest);
        Assert.Equal("fail", failing.Attribute("test.case.result.status"));
        var dotnetTest = Assert.Single(spans, span => span.Name == "dotnet test");
        Assert.All(tests, test => Assert.InRange(test.Start, dotnetTest.Start, dotnetTest.End));
    }

    [Fact]
    public async Task TestRun_ReturnsTheTestsThatRan_SlowestFirst()
    {
        var run = (await runs.Of(Fixture)).Result;

        Assert.Equal(run.Tests.Passed + run.Tests.Failed, run.SlowestTests.Length);
        Assert.Contains(run.SlowestTests, test => test.Name == FailingTest && test.Seconds > 0);
        Assert.Equal(run.SlowestTests.OrderByDescending(test => test.Seconds), run.SlowestTests);
        Assert.All(run.SlowestTests, test => Assert.Equal("net10.0", test.Framework));
        Assert.Equal(["net10.0"], run.Frameworks);
    }

    [Fact]
    public async Task TestRun_OfAProjectWithSeveralFrameworks_NamesTheFrameworkOfEachTest()
    {
        var (server, run) = await runs.Of(TestingPlatformProject);

        Assert.Equal(["net10.0", "net11.0"], run.Frameworks);
        Assert.Equal(["net10.0", "net11.0"], run.SlowestTests.Select(test => test.Framework).Distinct().Order());
        var tests = (await server.TestRunSpans())
            .Where(span => span.Service == "Ogooreck.Mcp" && span.Attributes.ContainsKey("test.case.name")).ToArray();
        Assert.Equal(["net10.0", "net11.0"], tests.Select(test => test.Attribute("test.framework")).Distinct().Order());
    }

    [Fact]
    public async Task TestRun_WithAHangTimeout_OfATestingPlatformProject_IsAnError()
    {
        var result = await server.Call("ogooreck_test_run", new() { ["project"] = TestingPlatformProject, ["hangTimeout"] = 10 });

        Assert.True(result.IsError);
        Assert.Contains("hangTimeout works only for VSTest projects", Text(result));
    }

    [Fact]
    public async Task TestRun_ThatRunsNoTests_ReturnsTheEndOfTheOutput()
    {
        var run = (await runs.Of(Fixture, "--no-such-option")).Result;

        Assert.NotEqual(0, run.ExitCode);
        Assert.Equal(new TestCounts(0, 0, 0, 0), run.Tests);
        Assert.Contains(run.OutputTail, line => line.Contains("--no-such-option"));
    }

    [Fact]
    public async Task TestRun_RunsATestingPlatformProjectOnEachOfItsFrameworks_AsDotnetTestDoes()
    {
        var run = (await runs.Of(TestingPlatformProject)).Result;

        Assert.Equal("Microsoft.Testing.Platform", run.Runner);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(new TestCounts(4, 4, 0, 0), run.Tests);
        Assert.True(run.Spans > 0);
        Assert.Empty(run.NeverEnded);
    }

    [Fact]
    public async Task TestRun_OfATestingPlatformProject_TakesTheArgumentsOfDotnetTest()
    {
        var run = (await runs.Of(TestingPlatformProject, "--framework", "net11.0")).Result;

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(new TestCounts(2, 2, 0, 0), run.Tests);
    }

    [Fact]
    public async Task TestRun_OfAnFSharpProject_NamesTheTestAndTheLineOfEachStep()
    {
        var (fsharp, run) = await runs.Of("Ogooreck.Wolverine.FSharp.Tests");

        Assert.Equal(0, run.ExitCode);
        var steps = Structured<SlowSteps>(await fsharp.Call("ogooreck_slowest_steps", new() { ["top"] = 50 }));
        Assert.Contains(steps.Steps, step => step.Test == "PlacingAnOrderTests.placing an order publishes OrderPlaced" &&
                                             step.Location.StartsWith("src/dotnet/Ogooreck.Wolverine.FSharp.Tests/PlacingAnOrderTests.fs:"));
    }

    [Fact]
    public async Task TestRun_OfAMissingProject_IsAnError()
    {
        var result = await server.Call("ogooreck_test_run", new() { ["project"] = "Ogooreck.Missing.Tests" });

        Assert.True(result.IsError);
        Assert.Contains($"No test project at {Path.Combine(SourceDirectory, "Ogooreck.Missing.Tests")}. The path is relative to {SourceDirectory}", Text(result));
    }

    [Fact]
    public async Task TestRun_OfAFolderWithoutAProject_IsAnError()
    {
        var result = await server.Call("ogooreck_test_run", new() { ["project"] = "Shared" });

        Assert.True(result.IsError);
        Assert.Contains("Cannot read the test runner of", Text(result));
    }
}

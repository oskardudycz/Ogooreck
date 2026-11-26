using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.Running;

[Collection(BuildsTheFixture)]
public class TestRunTests(McpServer server): IClassFixture<McpServer>
{
    [Fact]
    public async Task TestRun_NamesEachFailedTestWithTheLocationOfItsRun()
    {
        var run = await server.TestRun(Fixture);

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
        var run = await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~RespondsWithCreated"]);

        Assert.Equal(new TestCounts(1, 1, 0, 0), run.Tests);
        Assert.Empty(run.FailedTests);
        Assert.Empty(run.OutputTail);
        Assert.Empty(run.RunningWhenAborted);
        Assert.Empty(run.NeverEnded);
    }

    [Fact]
    public async Task TestRun_Repeated_StopsAtTheFirstRunWithAFailedTest()
    {
        var run = await server.TestRun(Fixture, repeat: 3);

        Assert.Equal(1, run.Attempts);
        Assert.Equal(FailingTest, Assert.Single(run.FailedTests).Name);
        Assert.Equal(run.Spans, (await server.TestRunSpans()).Length);
    }

    [Fact]
    public async Task TestRun_Repeated_RunsAgainWhileTheTestsPass()
    {
        var run = await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~RespondsWithCreated"], repeat: 2);

        Assert.Equal(2, run.Attempts);
        Assert.Equal(new TestCounts(1, 1, 0, 0), run.Tests);
    }

    [Fact]
    public async Task TestRun_WithAHangTimeout_NamesTheHungTestAndTheSpansThatNeverEnded()
    {
        var run = await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~NeverComes", "--", "xUnit.Explicit=on"],
            hangTimeout: 10);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Equal([HangingTest], run.RunningWhenAborted);
        var neverEnded = Assert.Single(run.NeverEnded);
        Assert.Equal(["Before", "Given"], neverEnded.EndedChildren);
    }

    [Fact]
    public async Task TestRun_WithAHangTimeout_OfATestingPlatformProject_IsAnError()
    {
        var result = await server.Call("ogooreck_test_run", new() { ["project"] = "Ogooreck.TUnit.Tests", ["hangTimeout"] = 10 });

        Assert.True(result.IsError);
        Assert.Contains("hangTimeout works only for VSTest projects", Text(result));
    }

    [Fact]
    public async Task TestRun_ThatRunsNoTests_ReturnsTheEndOfTheOutput()
    {
        var run = await server.TestRun(Fixture, ["--no-such-option"]);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Equal(new TestCounts(0, 0, 0, 0), run.Tests);
        Assert.Contains(run.OutputTail, line => line.Contains("--no-such-option"));
    }

    [Fact]
    public async Task TestRun_OfADeterministicBuild_MapsTheSourcePathsToTheRepository()
    {
        var run = await server.TestRun(Fixture, ["-p:ContinuousIntegrationBuild=true", "--filter", "FullyQualifiedName~RespondsWithOk"]);

        Assert.Equal(FailingWhen, Assert.Single(Assert.Single(run.FailedTests).Runs).Location);
        Assert.Contains("/_/", Assert.Single(run.FailedTests).StackTrace);
    }

    [Fact]
    public async Task TestRun_RunsATestingPlatformProjectAsAnApplication()
    {
        var run = await server.TestRun("Ogooreck.TUnit.Tests");

        Assert.Equal("Microsoft.Testing.Platform", run.Runner);
        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Tests.Passed > 0);
        Assert.True(run.Spans > 0);
        Assert.Empty(run.NeverEnded);
    }

    [Fact]
    public async Task TestRun_OfAnFSharpProject_NamesTheTestAndTheLineOfEachStep()
    {
        var run = await server.TestRun("Ogooreck.Wolverine.FSharp.Tests");

        Assert.Equal(0, run.ExitCode);
        var steps = Structured<SlowSteps>(await server.Call("ogooreck_slowest_steps", new() { ["top"] = 50 }));
        Assert.Contains(steps.Steps, step => step.Test == "PlacingAnOrderTests.placing an order publishes OrderPlaced" &&
                                             step.Location.StartsWith("src/dotnet/Ogooreck.Wolverine.FSharp.Tests/PlacingAnOrderTests.fs:"));
    }

    [Fact]
    public async Task TestRun_OfAMissingProject_IsAnError()
    {
        var missing = Path.Combine(Path.GetTempPath(), "Ogooreck.Missing.Tests");

        var result = await server.Call("ogooreck_test_run", new() { ["project"] = missing });

        Assert.True(result.IsError);
        Assert.Contains($"No test project at {missing}", Text(result));
    }

    [Fact]
    public async Task TestRun_OfAFolderWithoutAProject_IsAnError()
    {
        var result = await server.Call("ogooreck_test_run", new() { ["project"] = "Shared" });

        Assert.True(result.IsError);
        Assert.Contains("Cannot read the test runner of", Text(result));
    }
}

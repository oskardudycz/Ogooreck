using System.Diagnostics;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;

namespace Ogooreck.Mcp.Tests.Running;

[CollectionDefinition(nameof(StoppingTestProcessesTests), DisableParallelization = true)]
public sealed class StoppingTestProcessesCollection;

[Collection(nameof(StoppingTestProcessesTests))]
public class StoppingTestProcessesTests(McpServer server): IClassFixture<McpServer>
{
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
    public async Task TestRun_WithAHangTimeout_LeavesNoTestProcessRunning()
    {
        await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~NeverComes", "--", "xUnit.Explicit=on"],
            hangTimeout: 10);

        Assert.Empty(Process.GetProcessesByName(Fixture));
    }

    [Fact]
    public async Task TestRun_WhenCancelled_StopsTheTestProcess()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = server.Client.CallToolAsync("ogooreck_test_run",
            new Dictionary<string, object?>
            {
                ["project"] = Fixture,
                ["arguments"] = new[] { "--no-build", "--filter", "FullyQualifiedName~NeverComes", "--", "xUnit.Explicit=on" }
            },
            cancellationToken: cancel.Token).AsTask();
        await Until(() => Process.GetProcessesByName(Fixture)
            .Any(process => DateTime.Now - process.StartTime > TimeSpan.FromSeconds(3)), TimeSpan.FromMinutes(2));

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await Until(() => Process.GetProcessesByName(Fixture).Length == 0, TimeSpan.FromSeconds(30));
    }

    private static async Task Until(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"The condition did not hold within {timeout}.");
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }
    }
}

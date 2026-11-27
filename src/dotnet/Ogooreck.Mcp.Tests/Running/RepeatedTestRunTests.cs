using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.Running;

public class RepeatedTestRunTests(McpServer server): IClassFixture<McpServer>
{
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
}

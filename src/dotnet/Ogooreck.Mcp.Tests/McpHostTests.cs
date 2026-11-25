using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests;

[Collection(BuildsTheFixture)]
public class McpHostTests(McpServer server): IClassFixture<McpServer>
{
    [Fact]
    public void Instructions_TellAnAgentWhereToStart()
    {
        var instructions = server.Client.ServerInstructions;

        Assert.NotNull(instructions);
        Assert.Contains("ogooreck_test_run", instructions);
        Assert.Contains("ogooreck_find_spans", instructions);
        Assert.Contains("ogooreck_scenario_diagram", instructions);
        Assert.Contains("ogooreck_slowest_steps", instructions);
    }

    [Fact]
    public async Task Tools_ShareTheOgooreckPrefix_HaveTitles_AndSayWhichOnlyRead()
    {
        var tools = (await server.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken))
            .ToDictionary(tool => tool.Name, tool => tool.ProtocolTool.Annotations);

        Assert.Equal(["ogooreck_export_spans", "ogooreck_find_spans", "ogooreck_scenario_diagram", "ogooreck_slowest_steps", "ogooreck_test_run"],
            tools.Keys.Order());
        Assert.All(tools.Values, annotations => Assert.False(string.IsNullOrEmpty(annotations?.Title)));
        Assert.True(tools["ogooreck_slowest_steps"]!.ReadOnlyHint);
        Assert.True(tools["ogooreck_find_spans"]!.ReadOnlyHint);
        Assert.True(tools["ogooreck_scenario_diagram"]!.ReadOnlyHint);
        Assert.False(tools["ogooreck_test_run"]!.ReadOnlyHint);
    }
}

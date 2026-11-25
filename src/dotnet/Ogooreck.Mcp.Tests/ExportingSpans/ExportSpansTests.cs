using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.ExportingSpans;

[Collection(BuildsTheFixture)]
public class ExportSpansTests(McpServer server): IClassFixture<McpServer>
{
    [Fact]
    public async Task ExportSpans_WritesAllSpansOfTheLastRun()
    {
        var run = await server.TestRun(Fixture);

        var spans = await server.TestRunSpans();

        Assert.Equal(run.Spans, spans.Length);
        Assert.Contains(spans, span => span.Attributes.GetValueOrDefault("code.function.name") == FailingTest);
    }
}

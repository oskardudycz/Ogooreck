using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.ExportingSpans;

public class ExportSpansTests(SharedRuns runs)
{
    [Fact]
    public async Task ExportSpans_WritesAllSpansOfTheLastRun()
    {
        var (server, run) = await runs.Of(Fixture);

        var spans = await server.TestRunSpans();

        Assert.Equal(run.Spans, spans.Length);
        Assert.Contains(spans, span => span.Attributes.GetValueOrDefault("code.function.name")?.GetValue<string>() == FailingTest);
    }

    [Fact]
    public async Task ExportSpans_WriteArrayAttributesAsArrays()
    {
        var server = (await runs.Of(Fixture, "--filter", "FullyQualifiedName~WritingSpansOfItsOwn")).Server;

        var spans = await server.TestRunSpans();

        var own = Assert.Single(spans, span => span.Name == "own work");
        Assert.Equal("""["first","second"]""", own.Attributes["own.labels"].ToJsonString());
    }
}

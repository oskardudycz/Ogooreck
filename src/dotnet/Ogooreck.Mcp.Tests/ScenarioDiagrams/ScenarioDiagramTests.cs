using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.ScenarioDiagrams;

[Collection(BuildsTheFixture)]
public class ScenarioDiagramTests(McpServer server): IClassFixture<McpServer>
{
    [Fact]
    public async Task ScenarioDiagram_ShowsTheStepsAndTheMessagesTheAppConsumed()
    {
        await server.TestRun("Ogooreck.Wolverine.Tests", ["--filter", "FullyQualifiedName~ActionAdvancingAFakeClock"]);

        var diagram = Text(await server.Call("ogooreck_scenario_diagram",
            new() { ["test"] = "ActionAdvancingAFakeClock_ExpiresTheCartAndPublishesCartExpired" })).Split('\n');

        Assert.Equal("sequenceDiagram", diagram[0]);
        Assert.Equal("    participant Test", diagram[1]);
        Assert.Contains("    participant p1 as Ogooreck.Wolverine.Tests", diagram);
        Assert.Contains("    participant p2 as local://events/", diagram);
        Assert.Contains("    participant p3 as local://ogooreck.sample.wolverine.carts.expiring.expirecart/", diagram);
        Assert.Contains(diagram, line => line.StartsWith("    Test->>p1: Given step 1 request POST /carts/"));
        Assert.Equal(
            ["Test->>p1: Given", "p2->>p1: ProductAdded", "Test->>p1: When action", "p3->>p1: ExpireCart", "p2->>p1: CartExpired"],
            diagram.Where(line => line.Contains("->>")).Select(line => line.Trim()).Select(line => line.StartsWith("Test->>p1: Given") ? "Test->>p1: Given" : line));
    }

    [Fact]
    public async Task ScenarioDiagram_OfAFailedTest_EndsWithTheFailure()
    {
        await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~RespondsWithOk"]);

        var diagram = Text(await server.Call("ogooreck_scenario_diagram", new() { ["test"] = FailingTest })).Split('\n');

        Assert.Contains("    Test->>p1: When request POST /api/products", diagram);
        Assert.StartsWith("    Note over Test: failed: When request POST /api/products: status: expected <200 OK>, but found <201 Created>.",
            diagram[^1]);
    }

    [Fact]
    public async Task ScenarioDiagram_OfATestWithoutAnOgooreckRun_IsAnError()
    {
        await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~WritingSpansOfItsOwn"]);

        var result = await server.Call("ogooreck_scenario_diagram", new() { ["test"] = "WritingSpansOfItsOwn" });

        Assert.True(result.IsError);
        Assert.Contains("No Ogooreck run of WritingSpansOfItsOwn in the last ogooreck_test_run.", Text(result));
    }
}

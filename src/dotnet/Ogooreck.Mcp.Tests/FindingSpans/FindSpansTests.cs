using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.FindingSpans;

[Collection(BuildsTheFixture)]
public class FindSpansTests(McpServer server): IClassFixture<McpServer>
{
    [Fact]
    public async Task FindSpans_ReturnEachMatchWithItsPathTestAndLine()
    {
        await server.TestRun(Fixture);

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = "expected <200 ok>" }));

        Assert.True(found.Total > 0);
        Assert.All(found.Spans, span => Assert.Equal(FailingTest, span.Test));
        Assert.Contains(found.Spans, span => span.Location == FailingWhen && span.Path.Length == 1);
    }

    [Fact]
    public async Task FindSpans_OfTheApp_LeadToTheTestThatSentTheRequest()
    {
        await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~RespondsWithOk"]);

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = "^POST /api/products" }));

        Assert.Contains(found.Spans, span => span.Service == Fixture && span.Test == FailingTest && span.Location == FailingWhen);
    }

    [Fact]
    public async Task FindSpans_OutsideOfAnOgooreckRun_HaveNoTestOrLocation()
    {
        await server.TestRun(Fixture);

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans", new() { ["pattern"] = "^own\\.ratio$" }));

        var span = Assert.Single(found.Spans);
        Assert.Equal(["own work"], span.Path);
        Assert.Null(span.Test);
        Assert.Null(span.Location);
        Assert.Null(span.Error);
        Assert.Equal("true", span.Attributes["own.done"]);
        Assert.Equal("false", span.Attributes["own.retried"]);
        Assert.Equal("0.5", span.Attributes["own.ratio"]);
        Assert.DoesNotContain("own.labels", span.Attributes.Keys);
    }

    [Fact]
    public async Task FindSpans_ReturnOnlyTheLimit_AndCountAllMatches()
    {
        await server.TestRun(Fixture);

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = ".", ["limit"] = 2 }));

        Assert.Equal(2, found.Spans.Length);
        Assert.Equal(found.Total, (await server.TestRunSpans()).Length);
    }

    [Fact]
    public async Task FindSpans_WithAFilename_WriteTheMatchesToTheFileInsteadOfTheResponse()
    {
        await server.TestRun(Fixture);
        var file = server.FileIn("found.json");

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = "RegisteringAProduct", ["filename"] = file }));

        Assert.Empty(found.Spans);
        Assert.Equal(file, found.File);
        var written = JsonSerializer.Deserialize<FoundSpan[]>(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;
        Assert.Equal(found.Total, written.Length);
    }
}

using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.FindingSpans;

public class FindSpansTests(SharedRuns runs)
{
    [Fact]
    public async Task FindSpans_ReturnEachMatchWithItsPathTestAndLine()
    {
        var server = (await runs.Of(Fixture)).Server;

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = "expected <200 ok>" }));

        Assert.True(found.Total > 0);
        Assert.All(found.Spans, span => Assert.Equal(FailingTest, span.Test));
        Assert.Contains(found.Spans, span => span.Location == FailingWhen && span.Path.Length == 1);
    }

    [Fact]
    public async Task FindSpans_OfTheApp_LeadToTheTestThatSentTheRequest()
    {
        var server = (await runs.Of(Fixture, "--filter", "FullyQualifiedName~RespondsWithOk")).Server;

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = "^POST /api/products" }));

        Assert.Contains(found.Spans, span => span.Service == Fixture && span.Test == FailingTest && span.Location == FailingWhen);
    }

    [Fact]
    public async Task FindSpans_OutsideOfAnOgooreckRun_HaveNoTestOrLocation()
    {
        var server = (await runs.Of(Fixture)).Server;

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans", new() { ["pattern"] = "^own\\.ratio$" }));

        var span = Assert.Single(found.Spans);
        Assert.Equal(["own work"], span.Path);
        Assert.Null(span.Test);
        Assert.Null(span.Location);
        Assert.Null(span.Error);
        Assert.Equal("true", span.Attributes["own.done"].GetValue<string>());
        Assert.Equal("false", span.Attributes["own.retried"].GetValue<string>());
        Assert.Equal("0.5", span.Attributes["own.ratio"].GetValue<string>());
        Assert.Equal("NaN", span.Attributes["own.unknown"].GetValue<string>());
        Assert.Equal("""["first","second"]""", span.Attributes["own.labels"].ToJsonString());
        Assert.Equal("[1,2]", span.Attributes["own.attempts"].ToJsonString());
    }

    [Fact]
    public async Task FindSpans_MatchTheItemsOfAnArrayAttribute()
    {
        var server = (await runs.Of(Fixture)).Server;

        var byItem = Structured<FoundSpans>(await server.Call("ogooreck_find_spans", new() { ["pattern"] = "^second$" }));
        var byJoinedItems = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = "^first\",\"second$" }));

        Assert.Equal("own work", Assert.Single(byItem.Spans).Name);
        Assert.Equal(0, byJoinedItems.Total);
    }

    [Fact]
    public async Task FindSpans_ExportedToAFile_KeepArrayAttributes()
    {
        var server = (await runs.Of(Fixture)).Server;
        var file = server.FileIn("own.json");

        await server.Call("ogooreck_find_spans", new() { ["pattern"] = "^own\\.ratio$", ["filename"] = file });

        var written = JsonSerializer.Deserialize<FoundSpan[]>(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;
        Assert.Equal("""["first","second"]""", Assert.Single(written).Attributes["own.labels"].ToJsonString());
    }

    [Fact]
    public async Task FindSpans_ReturnOnlyTheLimit_AndCountAllMatches()
    {
        var server = (await runs.Of(Fixture)).Server;

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = ".", ["limit"] = 2 }));

        Assert.Equal(2, found.Spans.Length);
        Assert.Equal(found.Total, (await server.TestRunSpans()).Length);
    }

    [Fact]
    public async Task FindSpans_WithAFilename_WriteTheMatchesToTheFileInsteadOfTheResponse()
    {
        var server = (await runs.Of(Fixture)).Server;
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

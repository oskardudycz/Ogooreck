using System.Text.Json;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.ConcurrentSpans;

public class ConcurrentSpansTests(SharedRuns runs)
{
    [Fact]
    public async Task ConcurrentSpans_ListWhatRanDuringTheSpan_WithItsTestAndLocation()
    {
        var server = (await runs.Of(Fixture, "--filter", "FullyQualifiedName~RespondsWithOk")).Server;

        var found = Structured<Mcp.ConcurrentSpans>(await server.Call("ogooreck_concurrent_spans",
            new() { ["pattern"] = "^POST /api/products" }));

        Assert.Equal(1, found.Matches);
        Assert.Equal(Fixture, found.Span.Service);
        Assert.StartsWith("POST /api/products", found.Span.Name);
        var step = Assert.Single(found.Spans, span => span.Name == "When request POST /api/products");
        Assert.Equal(FailingTest, step.Test);
        Assert.Equal(FailingWhen, step.Location);
        Assert.True(step.StartMilliseconds < 0);
        Assert.Equal(found.Total, found.Spans.Length);
    }

    [Fact]
    public async Task ConcurrentSpans_LeaveOutTheParentsAndTheSpansUnderTheSpan()
    {
        var server = (await runs.Of(Fixture, "--filter", "FullyQualifiedName~RespondsWithOk")).Server;

        var found = Structured<Mcp.ConcurrentSpans>(await server.Call("ogooreck_concurrent_spans",
            new() { ["pattern"] = "^When request POST" }));

        Assert.DoesNotContain(found.Spans, span => span.Name is "Ogooreck Given/When/Then" or "catch-up");
        Assert.Contains(found.Spans, span => span.Service == Fixture);
    }

    [Fact]
    public async Task ConcurrentSpans_OfAService_LeaveOutTheSpansOfOtherServices()
    {
        var server = (await runs.Of(Fixture, "--filter", "FullyQualifiedName~RespondsWithOk")).Server;

        var found = Structured<Mcp.ConcurrentSpans>(await server.Call("ogooreck_concurrent_spans",
            new() { ["pattern"] = "^When request POST", ["service"] = Fixture }));

        Assert.NotEmpty(found.Spans);
        Assert.All(found.Spans, span => Assert.Equal(Fixture, span.Service));
        Assert.Equal(found.Total, found.Spans.Length);
    }

    [Fact]
    public async Task ConcurrentSpans_ReturnOnlyTheLimit_OrWriteThemToTheFile()
    {
        var server = (await runs.Of(Fixture)).Server;
        var file = server.FileIn("concurrent.json");

        var limited = Structured<Mcp.ConcurrentSpans>(await server.Call("ogooreck_concurrent_spans",
            new() { ["pattern"] = "^POST /api/products", ["limit"] = 1 }));
        var written = Structured<Mcp.ConcurrentSpans>(await server.Call("ogooreck_concurrent_spans",
            new() { ["pattern"] = "^POST /api/products", ["filename"] = file }));

        Assert.Single(limited.Spans);
        Assert.True(limited.Total > 1);
        Assert.Empty(written.Spans);
        Assert.Equal(file, written.File);
        var spans = JsonSerializer.Deserialize<ConcurrentSpan[]>(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;
        Assert.Equal(limited.Total, spans.Length);
    }

    [Fact]
    public async Task ConcurrentSpans_OfASpanOutsideOfAnOgooreckRun_HaveNoTestOrLocation()
    {
        var server = (await runs.Of(Fixture)).Server;

        var found = Structured<Mcp.ConcurrentSpans>(await server.Call("ogooreck_concurrent_spans",
            new() { ["pattern"] = "^own work$" }));

        Assert.Equal("own work", found.Span.Name);
        Assert.Null(found.Span.Test);
        Assert.Null(found.Span.Location);
    }

    [Fact]
    public async Task ConcurrentSpans_OfAPatternThatMatchesNoSpan_IsAnError()
    {
        var server = (await runs.Of(Fixture, "--filter", "FullyQualifiedName~RespondsWithCreated")).Server;

        var result = await server.Call("ogooreck_concurrent_spans", new() { ["pattern"] = "^no such span$" });

        Assert.True(result.IsError);
        Assert.Contains("No span matches ^no such span$", Text(result));
    }
}

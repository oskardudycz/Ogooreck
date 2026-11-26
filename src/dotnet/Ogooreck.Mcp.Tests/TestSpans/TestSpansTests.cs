using System.Text.Json;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.TestSpans;

[Collection(BuildsTheFixture)]
public class TestSpansTests(McpServer server): IClassFixture<McpServer>
{
    [Fact]
    public async Task TestSpans_ShowTheRunOfTheTest_WithTheAppSpansOfItsRequestUnderTheStep()
    {
        await server.TestRun(Fixture);

        var found = Structured<Mcp.TestSpans>(await server.Call("ogooreck_test_spans", new() { ["test"] = "respondswithok$" }));

        var test = Assert.Single(found.Runs);
        Assert.Equal(FailingTest, test.Test);
        Assert.Equal(FailingWhen, test.Location);
        Assert.Equal("Ogooreck Given/When/Then", test.Run.Name);
        Assert.Equal(0, test.Run.StartMilliseconds);
        Assert.NotNull(test.Run.Error);
        Assert.Equal(["Before", "When request POST /api/products", "Then"], test.Run.Children.Select(span => span.Name));
        var when = test.Run.Children[1];
        Assert.True(when.StartMilliseconds > 0);
        Assert.Contains(when.Children, span => span.Service == Fixture && span.Name.StartsWith("POST /api/products"));
        Assert.Contains(when.Children, span => span.Name == "catch-up");
    }

    [Fact]
    public async Task TestSpans_WithAFilename_WriteTheRunsToTheFileInsteadOfTheResponse()
    {
        await server.TestRun(Fixture);
        var file = server.FileIn("test-spans.json");

        var found = Structured<Mcp.TestSpans>(await server.Call("ogooreck_test_spans",
            new() { ["test"] = "RegisteringAProduct", ["filename"] = file }));

        Assert.Empty(found.Runs);
        Assert.Equal(file, found.File);
        var written = JsonSerializer.Deserialize<TestSpansRun[]>(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;
        Assert.Equal([FailingTest.Replace("RespondsWithOk", "RespondsWithCreated"), FailingTest], written.Select(run => run.Test).Order());
    }
}

using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.ImportingSpans;

public class ImportSpansTests(McpServer server, CollectedFile collected): IClassFixture<McpServer>, IClassFixture<CollectedFile>
{
    [Fact]
    public async Task ImportSpans_FromTheFileOfACollector_GivesTheOtherToolsTheSpansOfThatRun()
    {
        var file = collected.Path;

        var imported = await server.Call("ogooreck_import_spans", new() { ["path"] = file });

        Assert.Matches($"^imported [1-9][0-9]* spans from {file}$", Text(imported));
        var test = Assert.Single(Structured<Mcp.TestSpans>(await server.Call("ogooreck_test_spans",
            new() { ["test"] = "RespondsWithOk$" })).Runs);
        Assert.Equal(FailingTest, test.Test);
        Assert.Equal(FailingWhen, test.Location);
        Assert.NotNull(test.Run.Error);
    }

    [Fact]
    public async Task ImportSpans_KeepArrayAttributes()
    {
        await server.Call("ogooreck_import_spans", new() { ["path"] = collected.Path });

        var own = Assert.Single(await server.TestRunSpans(), span => span.Name == "own work" && span.Scope == Fixture);
        Assert.Equal("""["first","second"]""", own.Attributes["own.labels"].ToJsonString());
        Assert.Equal("[1,2]", own.Attributes["own.attempts"].ToJsonString());
    }

    [Fact]
    public async Task ImportSpans_SkipAttributesWhoseTypeTheyCannotShow_AndKeepTheRest()
    {
        await server.Call("ogooreck_import_spans", new() { ["path"] = collected.Path });

        var span = Assert.Single(await server.TestRunSpans(), span => span.Name == "own map work");
        Assert.Equal("", span.Service);
        Assert.DoesNotContain("own.map", span.Attributes.Keys);
        Assert.Equal("kept", span.Attribute("own.name"));
    }

    [Fact]
    public async Task ImportSpans_ReplaceTheSpansOfTheLastTestRun()
    {
        await server.TestRun(Fixture);
        var file = server.FileIn("empty.pb");
        await File.WriteAllBytesAsync(file, [], TestContext.Current.CancellationToken);

        var imported = await server.Call("ogooreck_import_spans", new() { ["path"] = file });

        Assert.Equal($"imported 0 spans from {file}", Text(imported));
        Assert.Empty(await server.TestRunSpans());
    }

    [Theory]
    [InlineData("text", new byte[] { (byte)'n', (byte)'o', (byte)'t', (byte)' ', (byte)'s', (byte)'p', (byte)'a', (byte)'n', (byte)'s' })]
    [InlineData("broken message", new byte[] { 0, 0, 0, 1, 0xFF })]
    public async Task ImportSpans_OfAFileThatIsNotFromACollector_IsAnError(string name, byte[] content)
    {
        var file = server.FileIn($"{name}.pb");
        await File.WriteAllBytesAsync(file, content, TestContext.Current.CancellationToken);

        var result = await server.Call("ogooreck_import_spans", new() { ["path"] = file });

        Assert.True(result.IsError);
        Assert.Contains($"{file} is not a file that the OpenTelemetry Collector's file exporter wrote with format: proto", Text(result));
    }

    [Fact]
    public async Task ImportSpans_OfAMissingFile_IsAnError()
    {
        var file = server.FileIn("missing.pb");

        var result = await server.Call("ogooreck_import_spans", new() { ["path"] = file });

        Assert.True(result.IsError);
        Assert.Contains($"No file at {file}", Text(result));
    }
}

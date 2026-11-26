using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.ImportingSpans;

[Collection(BuildsTheFixture)]
public class ImportSpansTests(McpServer server): IClassFixture<McpServer>
{
    [Fact]
    public async Task ImportSpans_FromTheFileOfACollector_GivesTheOtherToolsTheSpansOfThatRun()
    {
        var file = server.FileIn("collected.pb");
        await File.WriteAllBytesAsync(file,
            await Collector.SpansOfAFixtureRun("--filter", "FullyQualifiedName~RespondsWithOk"), TestContext.Current.CancellationToken);

        var imported = await server.Call("ogooreck_import_spans", new() { ["path"] = file });

        Assert.Matches($"^imported [1-9][0-9]* spans from {file}$", Text(imported));
        var test = Assert.Single(Structured<Mcp.TestSpans>(await server.Call("ogooreck_test_spans",
            new() { ["test"] = "RespondsWithOk$" })).Runs);
        Assert.Equal(FailingTest, test.Test);
        Assert.Equal(FailingWhen, test.Location);
        Assert.NotNull(test.Run.Error);
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

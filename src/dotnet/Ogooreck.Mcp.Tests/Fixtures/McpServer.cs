using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;

namespace Ogooreck.Mcp.Tests.Fixtures;

public sealed class McpServer: IAsyncLifetime
{
    private readonly DirectoryInfo files = Directory.CreateTempSubdirectory("ogooreck-mcp-tests-");

    public McpClient Client { get; private set; } = null!;

    public string FileIn(string name) => Path.Combine(files.FullName, name);

    public async ValueTask InitializeAsync() =>
        Client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ogooreck",
            Command = "dotnet",
            Arguments = [Server],
            WorkingDirectory = SourceDirectory
        }), cancellationToken: TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        files.Delete(true);
    }

    public Task<CallToolResult> Call(string tool, Dictionary<string, object?> arguments) =>
        Client.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken).AsTask();

    public async Task<TestRunResult> TestRun(string project, string[]? arguments = null)
    {
        var result = await Call("ogooreck_test_run", new() { ["project"] = project, ["arguments"] = arguments });
        Assert.True(result.IsError is null or false, ToolResults.Text(result));
        return ToolResults.Structured<TestRunResult>(result);
    }

    public async Task<CollectedSpan[]> TestRunSpans()
    {
        var file = FileIn("spans.json");
        var result = await Call("ogooreck_export_spans", new() { ["path"] = file });
        Assert.Contains(file, ToolResults.Text(result));
        return JsonSerializer.Deserialize<CollectedSpan[]>(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;
    }
}

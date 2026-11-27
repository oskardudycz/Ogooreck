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

    private (string Key, TestRunResult Result)? lastRun;

    private IList<McpClientTool>? tools;

    public async Task<CallToolResult> Call(string tool, Dictionary<string, object?> arguments)
    {
        if (tool is "ogooreck_test_run" or "ogooreck_import_spans")
            lastRun = null;
        var result = await Client.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken);
        if (result.StructuredContent is { } content)
            AssertHasEveryRequiredProperty(await OutputSchemaOf(tool), content, tool);
        return result;
    }

    private async Task<JsonElement> OutputSchemaOf(string tool)
    {
        tools ??= await Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        return tools.Single(listed => listed.Name == tool).ProtocolTool.OutputSchema!.Value;
    }

    private static void AssertHasEveryRequiredProperty(JsonElement schema, JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
                foreach (var name in required.EnumerateArray().Select(name => name.GetString()!))
                    Assert.True(value.TryGetProperty(name, out _), $"{path} has no {name}, which its output schema requires.");
            if (schema.TryGetProperty("properties", out var properties))
                foreach (var property in properties.EnumerateObject())
                    if (value.TryGetProperty(property.Name, out var child))
                        AssertHasEveryRequiredProperty(property.Value, child, $"{path}.{property.Name}");
        }

        if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
            foreach (var item in value.EnumerateArray())
                AssertHasEveryRequiredProperty(items, item, $"{path}[]");
    }

    public async Task<TestRunResult> TestRun(string project, string[]? arguments = null, int repeat = 1, int? hangTimeout = null,
        bool build = false)
    {
        var withBuild = build ? arguments ?? [] : ["--no-build", .. arguments ?? []];
        var key = string.Join(' ', [project, .. withBuild, $"repeat={repeat}", $"hangTimeout={hangTimeout}"]);
        if (!build && lastRun?.Key == key)
            return lastRun.Value.Result;

        var result = await Call("ogooreck_test_run",
            new() { ["project"] = project, ["arguments"] = withBuild, ["repeat"] = repeat, ["hangTimeout"] = hangTimeout });
        Assert.True(result.IsError is null or false, ToolResults.Text(result));
        var run = ToolResults.Structured<TestRunResult>(result);
        lastRun = (key, run);
        return run;
    }

    public async Task<CollectedSpan[]> TestRunSpans()
    {
        var file = FileIn($"spans-{Guid.NewGuid():N}.json");
        var result = await Call("ogooreck_export_spans", new() { ["path"] = file });
        Assert.Contains(file, ToolResults.Text(result));
        return JsonSerializer.Deserialize<CollectedSpan[]>(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;
    }
}

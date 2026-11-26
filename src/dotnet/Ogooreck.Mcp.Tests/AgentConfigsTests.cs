using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;

namespace Ogooreck.Mcp.Tests;

[Collection(BuildsTheFixture)]
public class AgentConfigsTests
{
    private static readonly string Repository = Path.GetFullPath(Path.Combine(SourceDirectory, "..", ".."));

    [Fact]
    public async Task CodexAndOpenCode_StartTheServerAsClaudeCodeDoes()
    {
        var claude = await ClaudeCode();

        Assert.Equal(claude, await Codex());
        Assert.Equal(claude, await OpenCode());
    }

    [Fact]
    public async Task TheConfiguredCommand_StartsTheServer_FromTheRepository()
    {
        var command = await ClaudeCode();
        await using var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ogooreck",
            Command = command[0],
            Arguments = command[1..],
            WorkingDirectory = Repository
        }), cancellationToken: TestContext.Current.CancellationToken);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(tools, tool => tool.Name == "ogooreck_test_run");
    }

    private static async Task<string[]> ClaudeCode()
    {
        using var config = JsonDocument.Parse(await Read(".mcp.json"));
        var server = config.RootElement.GetProperty("mcpServers").GetProperty("ogooreck");
        return [server.GetProperty("command").GetString()!, .. server.GetProperty("args").Deserialize<string[]>()!];
    }

    private static async Task<string[]> Codex()
    {
        var config = await Read(".codex/config.toml");
        Assert.Contains("[mcp_servers.ogooreck]", config);
        var command = Regex.Match(config, "^command = (\".*\")$", RegexOptions.Multiline).Groups[1].Value;
        var args = Regex.Match(config, @"^args = (\[.*\])$", RegexOptions.Multiline).Groups[1].Value;
        return [JsonSerializer.Deserialize<string>(command)!, .. JsonSerializer.Deserialize<string[]>(args)!];
    }

    private static async Task<string[]> OpenCode()
    {
        using var config = JsonDocument.Parse(await Read("opencode.json"));
        var server = config.RootElement.GetProperty("mcp").GetProperty("ogooreck");
        Assert.Equal("local", server.GetProperty("type").GetString());
        return server.GetProperty("command").Deserialize<string[]>()!;
    }

    private static Task<string> Read(string file) =>
        File.ReadAllTextAsync(Path.Combine(Repository, file), TestContext.Current.CancellationToken);
}

using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Ogooreck.Mcp.Tests.Fixtures;

internal static class ToolResults
{
    public static T Structured<T>(CallToolResult result) =>
        JsonSerializer.Deserialize<T>(result.StructuredContent!.Value, JsonSerializerOptions.Web)!;

    public static string Text(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}

using System.Text.Json;

namespace Ogooreck.Mcp;

internal static class JsonFiles
{
    public static async Task<string> Write<T>(string path, T content, CancellationToken ct)
    {
        await using var file = File.Create(path);
        await JsonSerializer.SerializeAsync(file, content, JsonSerializerOptions.Web, ct);
        return Path.GetFullPath(path);
    }
}

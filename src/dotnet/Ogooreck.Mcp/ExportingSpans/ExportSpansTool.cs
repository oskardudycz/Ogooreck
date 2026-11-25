using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class ExportSpansTool(CollectedSpans collected)
{
    [McpServerTool(Name = "ogooreck_export_spans", Title = "Export spans", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description("Writes all spans of the last ogooreck_test_run to a JSON file, for other tools or a viewer. Overwrites the file.")]
    public async Task<string> ExportSpans([Description("Path of the JSON file to write.")] string path,
        CancellationToken ct = default)
    {
        var spans = collected.All();
        return $"wrote {spans.Count} spans to {await JsonFiles.Write(path, spans, ct)}";
    }
}

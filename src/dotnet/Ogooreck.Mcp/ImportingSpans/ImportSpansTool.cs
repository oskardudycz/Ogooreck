using System.Buffers.Binary;
using System.ComponentModel;
using Google.Protobuf;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class ImportSpansTool(CollectedSpans collected, SourceFiles sources)
{
    private const int LengthPrefix = 4;

    [McpServerTool(Name = "ogooreck_import_spans", Title = "Import spans", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description("Replaces the spans of the last ogooreck_test_run with the spans in a file that the file exporter of an OpenTelemetry Collector wrote with format: proto, for example the spans of a CI run. The other tools then read those spans. Source locations are relative to the repository of the working directory.")]
    public async Task<string> ImportSpans([Description("Path of the file to read.")] string path, CancellationToken ct = default)
    {
        var file = Path.GetFullPath(path);
        if (!File.Exists(file))
            throw new McpException($"No file at {file}.");

        var spans = Decode(await File.ReadAllBytesAsync(file, ct), file);
        collected.Replace(spans);
        sources.RootAt(Directory.GetCurrentDirectory());
        return $"imported {spans.Count} spans from {file}";
    }

    private static List<CollectedSpan> Decode(byte[] content, string file)
    {
        var spans = new List<CollectedSpan>();
        try
        {
            for (var offset = 0; offset < content.Length;)
            {
                var length = (int)BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(offset, LengthPrefix));
                offset += LengthPrefix;
                spans.AddRange(OtlpSpans.Decode(content.AsSpan(offset, length).ToArray()));
                offset += length;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidProtocolBufferException)
        {
            throw new McpException(
                $"{file} is not a file that the OpenTelemetry Collector's file exporter wrote with format: proto.", exception);
        }

        return spans;
    }
}

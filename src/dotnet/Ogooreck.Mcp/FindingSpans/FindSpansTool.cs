using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class FindSpansTool(CollectedSpans collected, SourceFiles sources)
{
    [McpServerTool(Name = "ogooreck_find_spans", Title = "Find spans", ReadOnly = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Finds the spans of the last ogooreck_test_run whose name, error, attribute key or attribute value matches a regular expression, ignoring case. Returns each match with its path from the root span, its test and the source location of the When step that led to it, also for spans of the tested app.")]
    public async Task<FoundSpans> FindSpans(
        [Description("Regular expression, matched ignoring case, for example \"timeout\", \"kafka\" or \"POST /api/carts\".")] string pattern,
        [Description("How many matches to return.")] int limit = 20,
        [Description("Path of a JSON file to write all matches to instead of returning them. Use it for big runs, then search the file.")] string? filename = null,
        CancellationToken ct = default)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var tree = new SpanTree(collected.All());
        var matches = tree.Spans
            .Where(span => regex.IsMatch(span.Name) || regex.IsMatch(span.Error ?? "") ||
                           span.Attributes.Any(attribute => regex.IsMatch(attribute.Key) || Matches(regex, attribute.Value)))
            .OrderBy(span => span.Start)
            .Select(span => (Span: span, Run: tree.RunOf(span)))
            .Select(found => new FoundSpan(found.Span.Name, found.Span.Scope, found.Span.Service, tree.PathOf(found.Span),
                SpanTree.TestOf(found.Run), found.Run is null ? null : sources.Location(found.Run),
                found.Span.Duration.TotalMilliseconds, found.Span.Error, found.Span.Attributes))
            .ToArray();

        return filename is null
            ? new FoundSpans(matches.Length, matches.Take(limit).ToArray(), null)
            : new FoundSpans(matches.Length, [], await JsonFiles.Write(filename, matches, ct));
    }

    private static bool Matches(Regex regex, JsonNode value) =>
        value is JsonArray items ? items.Any(item => Matches(regex, item!)) : regex.IsMatch(value.ToString());
}

public sealed record FoundSpans(int Total, FoundSpan[] Spans, string? File);

public sealed record FoundSpan(string Name, string Scope, string Service, string[] Path, string? Test, string? Location,
    double Milliseconds, string? Error, IReadOnlyDictionary<string, JsonNode> Attributes);

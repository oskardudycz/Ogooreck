using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class ConcurrentSpansTool(CollectedSpans collected, SourceFiles sources)
{
    [McpServerTool(Name = "ogooreck_concurrent_spans", Title = "Concurrent spans", ReadOnly = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists the spans of the last ogooreck_test_run that ran at the same time as a span, also those of other tests, each with its test and the source location of its When step. Use it for races, for example a catch-up or an expectation that saw the work of another test. The span is the first, by start, whose name matches a regular expression, ignoring case. Its parents and the spans under it are left out. Each span has its start in milliseconds from the start of the span, negative when it started before.")]
    public async Task<ConcurrentSpans> ConcurrentSpans(
        [Description("Regular expression for the span name, matched ignoring case, for example \"^catch-up$\" or \"When request POST /api/carts\".")] string pattern,
        [Description("Only the spans of this service, for example the tested app, to leave out the others.")] string? service = null,
        [Description("How many spans to return.")] int limit = 50,
        [Description("Path of a JSON file to write all spans to instead of returning them. Use it for big runs, then search the file.")] string? filename = null,
        CancellationToken ct = default)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var tree = new SpanTree(collected.All());
        var matches = tree.Spans.Where(span => regex.IsMatch(span.Name)).OrderBy(span => span.Start).ToArray();
        if (matches.Length == 0)
            throw new McpException($"No span matches {pattern}.");

        var span = matches[0];

        ConcurrentSpan Describe(CollectedSpan other)
        {
            var run = tree.RunOf(other);
            return new ConcurrentSpan(other.Name, other.Scope, other.Service, SpanTree.TestOf(run),
                run is null ? null : sources.Location(run), (other.Start - span.Start).TotalMilliseconds,
                other.Duration.TotalMilliseconds, other.Error);
        }

        var related = tree.AncestorsOf(span).Concat(tree.DescendantsOf(span)).Append(span)
            .Select(relative => relative.SpanId).ToHashSet();
        var concurrent = tree.Spans
            .Where(other => other.Start < span.End && other.End > span.Start && !related.Contains(other.SpanId))
            .Where(other => service is null || other.Service == service)
            .OrderBy(other => other.Start)
            .Select(Describe)
            .ToArray();
        var of = Describe(span);

        return filename is null
            ? new ConcurrentSpans(matches.Length, of, concurrent.Length, concurrent.Take(limit).ToArray(), null)
            : new ConcurrentSpans(matches.Length, of, concurrent.Length, [], await JsonFiles.Write(filename, concurrent, ct));
    }
}

public sealed record ConcurrentSpans(int Matches, ConcurrentSpan Span, int Total, ConcurrentSpan[] Spans, string? File);

public sealed record ConcurrentSpan(string Name, string Scope, string Service, string? Test, string? Location,
    double StartMilliseconds, double Milliseconds, string? Error);

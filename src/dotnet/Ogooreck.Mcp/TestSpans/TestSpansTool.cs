using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class TestSpansTool(CollectedSpans collected, SourceFiles sources)
{
    [McpServerTool(Name = "ogooreck_test_spans", Title = "Spans of a test", ReadOnly = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Shows the span tree of each Ogooreck run of the last ogooreck_test_run whose test name matches a regular expression, ignoring case: Before, Given, When and Then with their catch-ups and expectations, and under each request step the spans of the tested app for that request. Each span has its start in milliseconds from the start of its run, its duration and its error.")]
    public async Task<TestSpans> TestSpans(
        [Description("Regular expression for the full test name, matched ignoring case, for example \"CartTests\" or \"PlacingAnOrder_PublishesOrderPlaced$\".")] string test,
        [Description("Path of a JSON file to write the runs to instead of returning them. Use it for big runs, then search the file.")] string? filename = null,
        CancellationToken ct = default)
    {
        var regex = new Regex(test, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var tree = new SpanTree(collected.All());

        TimedSpan Node(CollectedSpan span, DateTimeOffset runStart) =>
            new(span.Name, span.Scope, span.Service, (span.Start - runStart).TotalMilliseconds, span.Duration.TotalMilliseconds,
                span.Error, tree.ChildrenOf(span).Concat(tree.AppRootsOf(span)).OrderBy(child => child.Start)
                    .Select(child => Node(child, runStart)).ToArray());

        var runs = tree.Runs.Where(run => regex.IsMatch(SpanTree.TestOf(run)!))
            .OrderBy(run => run.Start)
            .Select(run => new TestSpansRun(SpanTree.TestOf(run)!, sources.Location(run), Node(run, run.Start)))
            .ToArray();

        return filename is null ? new TestSpans(runs, null) : new TestSpans([], await JsonFiles.Write(filename, runs, ct));
    }
}

public sealed record TestSpans(TestSpansRun[] Runs, string? File);

public sealed record TestSpansRun(string Test, string Location, TimedSpan Run);

public sealed record TimedSpan(string Name, string Scope, string Service, double StartMilliseconds, double Milliseconds,
    string? Error, TimedSpan[] Children);

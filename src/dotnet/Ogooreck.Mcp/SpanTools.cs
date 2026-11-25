using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class SpanTools(CollectedSpans collected, SourceFiles sources)
{
    [McpServerTool(Name = "ogooreck_slowest_steps", Title = "Slowest steps", ReadOnly = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists the slowest Ogooreck steps, catch-ups, app starts and expectations of the last ogooreck_test_run, each with its test, the source location of its When step, the spans it waited on and the spans of the tested app for the request the step sent.")]
    public async Task<SlowSteps> SlowestSteps(
        [Description("How many steps to list.")] int top = 10,
        [Description("How deep to show the spans under each step.")] int depth = 3,
        [Description("How many of the slowest child spans to show at each level.")] int children = 5,
        [Description("Path of a JSON file to write the steps to instead of returning them. Use it for big runs, then search the file.")] string? filename = null,
        CancellationToken ct = default)
    {
        var tree = new SpanTree(collected.All());

        SpanNode[] Below(CollectedSpan span, int levels) => levels <= 0
            ? []
            : tree.ChildrenOf(span).OrderByDescending(child => child.Duration).Take(children)
                .Select(child => Node(child, levels - 1)).ToArray();

        SpanNode Node(CollectedSpan span, int levels) =>
            new(span.Name, span.Scope, span.Service, span.Duration.TotalMilliseconds, span.Error, Below(span, levels));

        var steps = tree.Steps.OrderByDescending(span => span.Duration).Take(top)
            .Select(step => (Step: step, Run: tree.RunOf(step)!))
            .Select(found => new SlowStep(found.Step.Name, SpanTree.TestOf(found.Run), sources.Location(found.Run),
                found.Step.Duration.TotalMilliseconds, found.Step.Error, Below(found.Step, depth),
                tree.AppRootsOf(found.Step).Select(span => Node(span, depth - 1)).ToArray()))
            .ToArray();

        return filename is null ? new SlowSteps(steps, null) : new SlowSteps([], await Write(filename, steps, ct));
    }

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
                           span.Attributes.Any(attribute => regex.IsMatch(attribute.Key) || regex.IsMatch(attribute.Value)))
            .OrderBy(span => span.Start)
            .Select(span => (Span: span, Run: tree.RunOf(span)))
            .Select(found => new FoundSpan(found.Span.Name, found.Span.Scope, found.Span.Service, tree.PathOf(found.Span),
                SpanTree.TestOf(found.Run), found.Run is null ? null : sources.Location(found.Run),
                found.Span.Duration.TotalMilliseconds, found.Span.Error, found.Span.Attributes))
            .ToArray();

        return filename is null
            ? new FoundSpans(matches.Length, matches.Take(limit).ToArray(), null)
            : new FoundSpans(matches.Length, [], await Write(filename, matches, ct));
    }

    [McpServerTool(Name = "ogooreck_scenario_diagram", Title = "Scenario diagram", ReadOnly = true, OpenWorld = false)]
    [Description("Draws a test of the last ogooreck_test_run as a Mermaid sequence diagram: each Given and When step the test sent to the app, the messages the app consumed after each step with their destination, and the failure of a failed run.")]
    public string ScenarioDiagram([Description("The test, as its method name or its fully qualified name.")] string test)
    {
        var tree = new SpanTree(collected.All());
        var runs = tree.Runs
            .Where(run => SpanTree.TestOf(run)! == test || SpanTree.TestOf(run)!.EndsWith($".{test}", StringComparison.Ordinal))
            .OrderBy(run => run.Start).ToArray();
        if (runs.Length == 0)
            throw new McpException($"No Ogooreck run of {test} in the last ogooreck_test_run.");
        return Mcp.ScenarioDiagram.Of(tree, runs);
    }

    [McpServerTool(Name = "ogooreck_export_spans", Title = "Export spans", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description("Writes all spans of the last ogooreck_test_run to a JSON file, for other tools or a viewer. Overwrites the file.")]
    public async Task<string> ExportSpans([Description("Path of the JSON file to write.")] string path,
        CancellationToken ct = default)
    {
        var spans = collected.All();
        return $"wrote {spans.Count} spans to {await Write(path, spans, ct)}";
    }

    private static async Task<string> Write<T>(string path, T content, CancellationToken ct)
    {
        await using var file = File.Create(path);
        await JsonSerializer.SerializeAsync(file, content, JsonSerializerOptions.Web, ct);
        return Path.GetFullPath(path);
    }
}

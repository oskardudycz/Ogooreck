using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class SlowestStepsTool(CollectedSpans collected, SourceFiles sources)
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

        return filename is null ? new SlowSteps(steps, null) : new SlowSteps([], await JsonFiles.Write(filename, steps, ct));
    }
}

public sealed record SlowSteps(SlowStep[] Steps, string? File);

public sealed record SlowStep(string Name, string? Test, string Location, double Milliseconds, string? Error,
    SpanNode[] WaitedOn, SpanNode[] App);

public sealed record SpanNode(string Name, string Scope, string Service, double Milliseconds, string? Error, SpanNode[] Children);

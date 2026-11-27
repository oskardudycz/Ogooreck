using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ogooreck.Mcp;

[McpServerToolType]
public sealed class ScenarioDiagramTool(CollectedSpans collected)
{
    [McpServerTool(Name = "ogooreck_scenario_diagram", Title = "Scenario diagram", ReadOnly = true, OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Draws a test of the last ogooreck_test_run as Mermaid sequence diagrams: each Given and When step the test sent to the app, the messages the app consumed after each step with their destination, and the failure of a failed run.")]
    public ScenarioDiagramResult ScenarioDiagram([Description("The test, as its method name or its fully qualified name.")] string test)
    {
        var tree = new SpanTree(collected.All());
        var runs = tree.Runs
            .Where(run => SpanTree.TestOf(run)! == test || SpanTree.TestOf(run)!.EndsWith($".{test}", StringComparison.Ordinal))
            .OrderBy(run => run.Start).ToArray();
        if (runs.Length == 0)
            throw new McpException($"No Ogooreck run of {test} in the last ogooreck_test_run.");
        return new ScenarioDiagramResult(runs.GroupBy(run => run.ServiceInstance)
            .Select(testProcess => Mcp.ScenarioDiagram.Of(tree, testProcess)).ToArray());
    }
}

public sealed record ScenarioDiagramResult(string[] Diagrams);

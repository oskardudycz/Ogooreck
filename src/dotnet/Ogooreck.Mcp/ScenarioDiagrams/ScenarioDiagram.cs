namespace Ogooreck.Mcp;

internal static class ScenarioDiagram
{
    public static string Of(SpanTree tree, IEnumerable<CollectedSpan> runs)
    {
        var participants = new List<string>();
        var lines = new List<string>();

        string Id(string participant)
        {
            if (!participants.Contains(participant))
                participants.Add(participant);
            return $"p{participants.IndexOf(participant) + 1}";
        }

        foreach (var run in runs)
        {
            foreach (var step in tree.RequestStepsOf(run))
            {
                var app = tree.AppRootsOf(step).Select(span => span.Service).FirstOrDefault(run.Service);
                lines.Add($"    Test->>{Id(app)}: {Text(step.Name)}");
                foreach (var consumed in tree.AppSpansOf(step).Where(span => span.Kind == "consumer"))
                    lines.Add($"    {Id(DestinationOf(consumed))}->>{Id(consumed.Service)}: {MessageTypeOf(consumed)}");
            }

            if (run.Error is not null)
                lines.Add($"    Note over Test: failed: {Text(run.Error)}");
        }

        return string.Join('\n',
            ["sequenceDiagram", "    participant Test", .. participants.Select((name, index) => $"    participant p{index + 1} as {name}"), .. lines]);
    }

    private static string DestinationOf(CollectedSpan span) =>
        span.Attribute("messaging.destination.name", span.Attribute("messaging.destination", "?"));

    private static string MessageTypeOf(CollectedSpan span)
    {
        var type = span.Attribute("messaging.message_type", span.Name);
        return type[(type.LastIndexOf('.') + 1)..];
    }

    private static string Text(string value) => value.ReplaceLineEndings(" ").Replace(';', ',');
}

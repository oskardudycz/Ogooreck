namespace Ogooreck.Mcp;

internal sealed class SpanTree
{
    private const string OgooreckScope = "Ogooreck";
    private const string RunName = "Ogooreck Given/When/Then";
    private const string StepTraceId = "ogooreck.step.trace_id";

    private readonly Dictionary<string, CollectedSpan> byId;
    private readonly ILookup<string, CollectedSpan> childrenOf;
    private readonly ILookup<string, CollectedSpan> stepsOfAppTrace;

    public SpanTree(IReadOnlyList<CollectedSpan> spans)
    {
        Spans = spans;
        byId = spans.GroupBy(span => span.SpanId).ToDictionary(group => group.Key, group => group.First());
        childrenOf = spans.Where(span => span.ParentSpanId is not null).ToLookup(span => span.ParentSpanId!);
        stepsOfAppTrace = spans.Where(span => span.Attributes.ContainsKey(StepTraceId))
            .ToLookup(span => span.Attributes[StepTraceId]);
    }

    public IReadOnlyList<CollectedSpan> Spans { get; }

    public IEnumerable<CollectedSpan> Runs => Spans.Where(IsRun);

    public IEnumerable<CollectedSpan> Steps => Spans.Where(span =>
        span.Scope == OgooreckScope && Parent(span) is { Scope: OgooreckScope });

    public IEnumerable<CollectedSpan> ChildrenOf(CollectedSpan span) => childrenOf[span.SpanId];

    public IEnumerable<CollectedSpan> AppRootsOf(CollectedSpan step) =>
        step.Attributes.TryGetValue(StepTraceId, out var traceId)
            ? Spans.Where(span => span.TraceId == traceId && Parent(span) is null).OrderBy(span => span.Start)
            : [];

    public IEnumerable<CollectedSpan> RequestStepsOf(CollectedSpan run) =>
        Spans.Where(span => span.Attributes.ContainsKey(StepTraceId) && RunOf(span)!.SpanId == run.SpanId)
            .OrderBy(span => span.Start);

    public IEnumerable<CollectedSpan> AppSpansOf(CollectedSpan step) =>
        Spans.Where(span => span.TraceId == step.Attributes[StepTraceId]).OrderBy(span => span.Start);

    public CollectedSpan? RunOf(CollectedSpan span)
    {
        var current = span;
        while (!IsRun(current))
        {
            var parent = Parent(current);
            if (parent is null)
                return stepsOfAppTrace[current.TraceId].Select(RunOf).FirstOrDefault();
            current = parent;
        }

        return current;
    }

    public string[] PathOf(CollectedSpan span)
    {
        var path = new List<string>();
        for (var current = span; current is not null; current = Parent(current))
            path.Insert(0, current.Name);
        return [.. path];
    }

    public static string? TestOf(CollectedSpan? run) => run?.Attributes.GetValueOrDefault("code.function.name");

    private CollectedSpan? Parent(CollectedSpan span) =>
        span.ParentSpanId is not null && byId.TryGetValue(span.ParentSpanId, out var parent) ? parent : null;

    private static bool IsRun(CollectedSpan span) => span.Scope == OgooreckScope && span.Name == RunName;
}

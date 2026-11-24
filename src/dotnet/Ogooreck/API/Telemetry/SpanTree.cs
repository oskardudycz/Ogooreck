namespace Ogooreck.API;

internal static class SpanTree
{
    public static string? MismatchOf(ExpectedSpan expected, IReadOnlyList<RecordedSpan> spans)
    {
        var named = spans.Where(span => span.Name == expected.Name).ToList();
        if (named.Count == 0)
            return $"No span '{expected.Name}'";

        var candidates = named.Where(span => Matches(span, expected)).ToList();
        if (candidates.Count == 0)
            return $"No span '{expected.Name}' with attributes {Describe(expected.Attributes)}, the spans of that name have {Describe(named)}";

        string? firstMismatch = null;
        foreach (var candidate in candidates)
        {
            var mismatch = MismatchBelow(expected, candidate, spans);
            if (mismatch is null)
                return null;
            firstMismatch ??= mismatch;
        }

        return firstMismatch;
    }

    public static bool IsBelow(RecordedSpan span, RecordedSpan ancestor, IReadOnlyList<RecordedSpan> spans)
    {
        if (span.TraceId != ancestor.TraceId)
            return false;

        var byId = spans
            .Where(each => each.TraceId == ancestor.TraceId)
            .DistinctBy(each => each.SpanId)
            .ToDictionary(each => each.SpanId);

        var parentId = span.ParentSpanId;
        while (parentId is not null && byId.TryGetValue(parentId, out var parent))
        {
            if (parentId == ancestor.SpanId)
                return true;
            parentId = parent.ParentSpanId;
        }

        return parentId == ancestor.SpanId;
    }

    public static string Describe(IReadOnlyList<RecordedSpan> spans) =>
        spans.Count == 0
            ? "  (none)"
            : string.Join(Environment.NewLine, spans.Select(span =>
                $"  {span.Name} (trace {span.TraceId}, span {span.SpanId}, parent {span.ParentSpanId ?? "none"}) {Describe(span.Attributes)}"));

    public static string Describe(IReadOnlyDictionary<string, string> attributes) =>
        $"{{{string.Join(", ", attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"))}}}";

    private static string? MismatchBelow(ExpectedSpan expected, RecordedSpan parent, IReadOnlyList<RecordedSpan> spans)
    {
        foreach (var child in expected.Children)
        {
            var named = spans.Where(span => span.Name == child.Name && IsBelow(span, parent, spans)).ToList();
            if (named.Count == 0)
                return $"No span '{child.Name}' below '{expected.Name}'";

            var candidates = named.Where(span => Matches(span, child)).ToList();
            if (candidates.Count == 0)
                return $"No span '{child.Name}' with attributes {Describe(child.Attributes)} below '{expected.Name}', the spans of that name have {Describe(named)}";

            string? firstMismatch = null;
            var matched = false;
            foreach (var candidate in candidates)
            {
                var mismatch = MismatchBelow(child, candidate, spans);
                if (mismatch is null)
                {
                    matched = true;
                    break;
                }

                firstMismatch ??= mismatch;
            }

            if (!matched)
                return firstMismatch;
        }

        return null;
    }

    private static bool Matches(RecordedSpan span, ExpectedSpan expected) =>
        expected.Attributes.All(pair => span.Attributes.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static string Describe(List<RecordedSpan> named) =>
        $"[{string.Join(", ", named.Select(span => Describe(span.Attributes)))}]";
}

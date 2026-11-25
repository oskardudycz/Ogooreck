namespace Ogooreck.Mcp;

public sealed class CollectedSpans
{
    private readonly Lock gate = new();
    private List<CollectedSpan> spans = [];

    public void Replace(IEnumerable<CollectedSpan> with)
    {
        lock (gate)
            spans = [.. with];
    }

    public void Add(IEnumerable<CollectedSpan> received)
    {
        lock (gate)
            spans.AddRange(received);
    }

    public IReadOnlyList<CollectedSpan> All()
    {
        lock (gate)
            return spans.ToArray();
    }
}

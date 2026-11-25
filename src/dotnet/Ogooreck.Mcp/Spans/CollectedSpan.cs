namespace Ogooreck.Mcp;

public sealed record CollectedSpan(
    string Service,
    string Scope,
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string Name,
    string Kind,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Error,
    IReadOnlyDictionary<string, string> Attributes)
{
    public TimeSpan Duration => End - Start;
}

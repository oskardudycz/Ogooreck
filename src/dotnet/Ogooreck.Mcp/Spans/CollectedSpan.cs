using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace Ogooreck.Mcp;

public sealed record CollectedSpan(
    string Service,
    string ServiceInstance,
    string Scope,
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string Name,
    string Kind,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Error,
    IReadOnlyDictionary<string, JsonNode> Attributes)
{
    public TimeSpan Duration => End - Start;

    [return: NotNullIfNotNull(nameof(fallback))]
    public string? Attribute(string key, string? fallback = null) =>
        Attributes.TryGetValue(key, out var value) ? value.ToString() : fallback;
}

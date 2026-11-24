namespace Ogooreck.API;

/// <summary>
/// A finished span, as an <see cref="ITelemetrySource"/> adds it. <c>ExpectTrace</c> and <c>ExpectCorrelatedTrace</c> read these.
/// Create one only in a custom source.
/// </summary>
/// <param name="TraceId">The trace id as 32 lowercase hex characters, as in a W3C <c>traceparent</c>. The span counts for a test when it is one of the test's trace ids.</param>
/// <param name="SpanId">The span id as 16 lowercase hex characters.</param>
/// <param name="ParentSpanId">The parent span id, or null for a root span.</param>
/// <param name="Name">The span name that <c>Span(name, ...)</c> matches, for example <c>POST /carts/{id}/orders</c>.</param>
/// <param name="Attributes">The attributes, with values written as Java's <c>String.valueOf</c> does: <c>true</c>, <c>1.5</c>.</param>
/// <example>
/// <code>
/// spans.Add(new RecordedSpan(traceId, spanId, parentSpanId, "publish OrderPlaced", new Dictionary&lt;string, string&gt;()));
/// </code>
/// </example>
public sealed record RecordedSpan(string TraceId, string SpanId, string? ParentSpanId, string Name,
    IReadOnlyDictionary<string, string> Attributes);

namespace Ogooreck.API;

/// <summary>
/// What a recorded item says to Ogooreck: the message, the W3C <c>traceparent</c> it carries, and, for causation checks, its message, correlation and causation ids.
/// </summary>
/// <example>
/// <code>
/// new Observed(envelope.Message!, envelope.ParentId, envelope.Id.ToString(), envelope.CorrelationId, causationId)
/// </code>
/// </example>
public sealed record Observed(object Message, string? Traceparent, string? MessageId, string? CorrelationId,
    string? CausationId)
{
    /// <summary>Creates an observation without message, correlation and causation ids.</summary>
    /// <example>
    /// <code>
    /// new Observed(message, traceparent)
    /// </code>
    /// </example>
    public Observed(object message, string? traceparent) : this(message, traceparent, null, null, null) { }
}

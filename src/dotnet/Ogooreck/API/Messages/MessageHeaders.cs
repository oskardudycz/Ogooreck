namespace Ogooreck.API;

/// <summary>
/// The names of the headers a <see cref="MessageReader"/> reads. The defaults are the recommended names:
/// <c>messageType</c>, <c>messageId</c>, <c>correlationId</c>, <c>causationId</c> and <c>traceparent</c>.
/// Set the properties your app uses when it names its headers differently.
/// </summary>
/// <example>
/// <code>
/// new MessageHeaders { MessageType = "message-type", MessageId = "message-id" }
/// </code>
/// </example>
public sealed class MessageHeaders
{
    /// <summary>The header with the message's type name, <c>FullName</c> or <c>Name</c>. Default <c>messageType</c>.</summary>
    /// <example>
    /// <code>
    /// new MessageHeaders { MessageType = "message-type" }
    /// </code>
    /// </example>
    public string MessageType { get; init; } = "messageType";

    /// <summary>The header with the message id. Default <c>messageId</c>.</summary>
    /// <example>
    /// <code>
    /// new MessageHeaders { MessageId = "message-id" }
    /// </code>
    /// </example>
    public string MessageId { get; init; } = "messageId";

    /// <summary>The header with the correlation id. Default <c>correlationId</c>.</summary>
    /// <example>
    /// <code>
    /// new MessageHeaders { CorrelationId = "correlation-id" }
    /// </code>
    /// </example>
    public string CorrelationId { get; init; } = "correlationId";

    /// <summary>The header with the id of the message that caused this one. Default <c>causationId</c>.</summary>
    /// <example>
    /// <code>
    /// new MessageHeaders { CausationId = "causation-id" }
    /// </code>
    /// </example>
    public string CausationId { get; init; } = "causationId";

    /// <summary>The header with the W3C trace context. Default <c>traceparent</c>.</summary>
    /// <example>
    /// <code>
    /// new MessageHeaders { Traceparent = "trace-parent" }
    /// </code>
    /// </example>
    public string Traceparent { get; init; } = "traceparent";
}

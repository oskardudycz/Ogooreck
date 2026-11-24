namespace Ogooreck.API;

/// <summary>What a <c>Publish</c> step asks the publish function to put on the bus: the messages, in order, and the step's W3C <c>traceparent</c>.</summary>
/// <example>
/// <code>
/// Publishing((request, ct) =&gt; producer.SendAsync(request.Messages, request.Traceparent, ct))
/// </code>
/// </example>
public sealed record PublishRequest(IReadOnlyList<object> Messages, string Traceparent);

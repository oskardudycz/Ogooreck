using RabbitMQ.Client;

namespace Ogooreck.RabbitMQ;

/// <summary>
/// A message the app published through its decorated channel: the exchange and routing key it was sent with, its properties and a copy of its body.
/// <see cref="RabbitMQPlugin.Published"/> holds these.
/// </summary>
/// <example>
/// <code>
/// new RabbitMQMessage("shop", "OrderPlaced", properties, body)
/// </code>
/// </example>
public sealed record RabbitMQMessage(string Exchange, string RoutingKey, IReadOnlyBasicProperties Properties,
    ReadOnlyMemory<byte> Body);

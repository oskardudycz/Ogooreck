using Confluent.Kafka;

namespace Ogooreck.Kafka;

/// <summary>A message a Kafka plugin recorded: the topic it went to or came from, and the message itself.</summary>
/// <example>
/// <code>
/// ExpectPublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId))
/// </code>
/// </example>
public sealed record KafkaMessage<TKey, TValue>(string Topic, Message<TKey, TValue> Message);

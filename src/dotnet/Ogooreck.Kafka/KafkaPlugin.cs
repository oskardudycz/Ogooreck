namespace Ogooreck.Kafka;

/// <summary>
/// Plugins Kafka into a specification, the way Java's <c>KafkaMessageBus</c> does: <see cref="Decorating()"/> records and waits for an in-process app's own producer and consumer,
/// and <see cref="Consuming"/> records from the topics for an app that runs out of process.
/// </summary>
/// <example>
/// <code>
/// public static class Messaging
/// {
///     public static readonly KafkaPlugin&lt;string, string&gt; Kafka = KafkaPlugin.Decorating().MessageTypes(typeof(OrderPlaced), typeof(ShipOrder));
/// }
///
/// public class ShopApi(): ApiSpecification&lt;Program&gt;(spec =&gt; spec.Use(Messaging.Kafka));
/// </code>
/// </example>
public static class KafkaPlugin
{
    /// <summary>
    /// Decorates the app's <c>IProducer&lt;TKey, TValue&gt;</c> and <c>IConsumer&lt;TKey, TValue&gt;</c> registrations that exist. The host fails to start when the app registers neither.
    /// </summary>
    /// <example>
    /// <code>
    /// KafkaPlugin.Decorating&lt;Guid, byte[]&gt;().Read(message =&gt; Observed(message))
    /// </code>
    /// </example>
    public static KafkaPlugin<TKey, TValue> Decorating<TKey, TValue>() => new();

    /// <summary>The same as <see cref="Decorating{TKey, TValue}"/> for an app whose keys and values are strings.</summary>
    /// <example>
    /// <code>
    /// public static readonly KafkaPlugin&lt;string, string&gt; Kafka = KafkaPlugin.Decorating();
    /// </code>
    /// </example>
    public static KafkaPlugin<string, string> Decorating() => Decorating<string, string>();

    /// <summary>
    /// Starts a test-side consumer with its own new group when the first step starts, reading the topics from their end, and records each message.
    /// It is for out-of-process apps, whose consumers it can't see: it has no catch-up, so <c>ExpectPublished</c> checks that the expected messages arrive in order, not that nothing else does.
    /// The consumer runs until the caller disposes the plugin.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var kafka = KafkaPlugin.Consuming(async () =&gt; await app.GetConnectionStringAsync("kafka"), ["orders"]);
    /// </code>
    /// </example>
    public static KafkaConsumingPlugin Consuming(Func<Task<string>> bootstrapServers, string[] topics) =>
        new(new KafkaConsumption(bootstrapServers, topics));
}

using System.Text.Json;
using Ogooreck.API;

namespace Ogooreck.Kafka;

/// <summary>
/// The plugin that <see cref="KafkaPlugin.Consuming"/> returns: a test-side consumer that records the messages on the given topics, for apps that run out of process.
/// The caller owns it and disposes it, which stops its consumer.
/// </summary>
/// <remarks>
/// It can't see the app's consumers, so it has no catch-up. Use <c>ExpectPublished</c> to check that the expected messages arrive in order.
/// Every option returns a plugin that shares this plugin's recorder and consumer, so a test class can read the messages differently without a second consumer.
/// </remarks>
/// <example>
/// <code>
/// await using var kafka = KafkaPlugin.Consuming(async () =&gt; await app.GetConnectionStringAsync("kafka"), ["orders"])
///     .MessageTypes(typeof(OrderPlaced));
/// </code>
/// </example>
public sealed class KafkaConsumingPlugin: IDerivedPlugin, IAsyncDisposable
{
    private readonly KafkaConsumption consumption;
    private readonly KafkaReading<string, string> reading;

    internal KafkaConsumingPlugin(KafkaConsumption consumption) : this(consumption, KafkaReading<string, string>.Default, null)
    {
    }

    private KafkaConsumingPlugin(KafkaConsumption consumption, KafkaReading<string, string> reading, object? origin)
    {
        this.consumption = consumption;
        this.reading = reading;
        Origin = origin ?? this;
    }

    /// <summary>The messages the consumer read, for <c>ExpectPublished</c>, <c>ExpectNothingPublished</c> and <c>ExpectCausation</c>.</summary>
    /// <example>
    /// <code>
    /// ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))
    /// </code>
    /// </example>
    public Recorded<KafkaMessage<string, string>> Published => consumption.Published;

    /// <inheritdoc />
    public object Origin { get; }

    /// <summary>Lists the message types. The type header's value matches a listed type's <c>FullName</c> first, then its <c>Name</c>.</summary>
    /// <example>
    /// <code>
    /// kafka.MessageTypes(typeof(OrderPlaced), typeof(ShipOrder))
    /// </code>
    /// </example>
    public KafkaConsumingPlugin MessageTypes(params Type[] types) => With(reading.With(reader => reader.MessageTypes(types)));

    /// <summary>Gives the plugin's own lookup from the type header's value to a type, returning null for an unknown one.</summary>
    /// <example>
    /// <code>
    /// kafka.MessageTypes(name =&gt; Type.GetType($"Shop.Events.{name}"))
    /// </code>
    /// </example>
    public KafkaConsumingPlugin MessageTypes(Func<string, Type?> lookupType) =>
        With(reading.With(reader => reader.MessageTypes(lookupType)));

    /// <summary>Sets the app's own header names.</summary>
    /// <example>
    /// <code>
    /// kafka.Headers(new MessageHeaders { MessageType = "message-type" })
    /// </code>
    /// </example>
    public KafkaConsumingPlugin Headers(MessageHeaders names) => With(reading.With(reader => reader.Headers(names)));

    /// <summary>Sets the JSON options that read the message bodies. The default is <see cref="JsonSerializerOptions.Web"/>.</summary>
    /// <example>
    /// <code>
    /// kafka.Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
    /// </code>
    /// </example>
    public KafkaConsumingPlugin Json(JsonSerializerOptions options) => With(reading.With(reader => reader.Json(options)));

    /// <summary>Replaces the reader entirely: the function turns a recorded message into an <see cref="Observed"/>.</summary>
    /// <example>
    /// <code>
    /// kafka.Read(recorded =&gt; new Observed(Parse(recorded.Message.Value), recorded.Message.Key))
    /// </code>
    /// </example>
    public KafkaConsumingPlugin Read(Func<KafkaMessage<string, string>, Observed> read) =>
        With(reading with { Custom = read });

    /// <inheritdoc />
    public void Register(SpecificationPluginRegistration registration) =>
        registration
            .Recording(Published, reading.Read)
            .Starting(_ => consumption.Start(registration.Deadline));

    /// <summary>Stops the consumer. It is safe to call on any plugin made from this one.</summary>
    /// <example>
    /// <code>
    /// await kafka.DisposeAsync();
    /// </code>
    /// </example>
    public ValueTask DisposeAsync() => consumption.DisposeAsync();

    private KafkaConsumingPlugin With(KafkaReading<string, string> changed) => new(consumption, changed, Origin);
}

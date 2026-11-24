using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using RabbitMQ.Client;

namespace Ogooreck.RabbitMQ;

/// <summary>
/// Plugs RabbitMQ into a specification: it decorates the app's <see cref="IConnection"/>, the channels it creates and the consumers registered on them,
/// records each message the app publishes, and catches up with a probe message per consumed queue, so a step ends when the consumers are done with its messages.
/// It works in-process only, and the app must register its <see cref="IConnection"/> in the service collection.
/// </summary>
/// <remarks>
/// Catch-up publishes a probe with the <c>catchUpProbe</c> header to each queue the app consumes. The decorated consumer acknowledges it when the consumer uses manual acknowledgement,
/// and never passes it to the app's handler. A probe counts when every lower delivery tag on its channel is done, no handler runs and no requeue is pending,
/// so it holds with <c>ConsumerDispatchConcurrency</c> above 1 and with nack-and-requeue retries, including a <c>multiple</c> nack, which counts each requeued delivery. It can't see retries that go through dead-letter exchanges.
/// At the start of every round it passively declares each consumed queue and fails the step when it has more consumers than the decorated ones, because another consumer can take the probe.
/// It checks only the queues its decorated consumers read, and never looks at queues that only other consumers read.
/// Each copy of the app a test project starts runs its own consumers, so two copies on one queue split the messages: run one app per test project, or give each copy its own queue names.
/// A test project usually holds one plug in a static field. Every option returns a new plug that shares <see cref="Published"/>, so a test class uses an option on the same app through <c>app.With(spec =&gt; spec.Use(...))</c>.
/// </remarks>
/// <example>
/// <code>
/// public static class Messaging
/// {
///     public static readonly RabbitMQPlug RabbitMQ = RabbitMQPlug.Decorating();
/// }
///
/// public class ShopApi(): ApiSpecification&lt;Program&gt;(spec =&gt; spec.Use(Messaging.RabbitMQ));
///
/// ExpectPublishedIn(Messaging.RabbitMQ.Published, new OrderPlaced(orderId), new ShipOrder(orderId))
/// </code>
/// </example>
public sealed class RabbitMQPlug: IDerivedPlug
{
    private readonly PlugSettings settings;

    private RabbitMQPlug(Recorded<RabbitMQMessage> published, PlugSettings settings, object? origin)
    {
        Published = published;
        this.settings = settings;
        Origin = origin ?? this;
    }

    /// <summary>The messages the app published, for <c>ExpectPublished</c>, <c>ExpectNothingPublished</c> and <c>ExpectCausation</c>.</summary>
    /// <example>
    /// <code>
    /// ExpectPublishedIn(Messaging.RabbitMQ.Published, new OrderPlaced(orderId))
    /// </code>
    /// </example>
    public Recorded<RabbitMQMessage> Published { get; }

    /// <inheritdoc />
    public object Origin { get; }

    /// <summary>Creates a plug that decorates the app's <see cref="IConnection"/>, reading messages with the recommended headers, see <see cref="MessageHeaders"/>.</summary>
    /// <example>
    /// <code>
    /// public static readonly RabbitMQPlug RabbitMQ = RabbitMQPlug.Decorating();
    /// </code>
    /// </example>
    public static RabbitMQPlug Decorating() => new(new Recorded<RabbitMQMessage>(), new PlugSettings(), null);

    /// <summary>Returns a plug that shares <see cref="Published"/> and finds the message types by the type header's <c>FullName</c>, then <c>Name</c>.</summary>
    /// <example>
    /// <code>
    /// Messaging.RabbitMQ.MessageTypes(typeof(OrderPlaced), typeof(ShipOrder))
    /// </code>
    /// </example>
    public RabbitMQPlug MessageTypes(params Type[] types) =>
        With(settings with { Reader = settings.Reader.MessageTypes(types) });

    /// <summary>Returns a plug that shares <see cref="Published"/> and finds the message types with your own lookup from the type header's value.</summary>
    /// <example>
    /// <code>
    /// Messaging.RabbitMQ.MessageTypes(name =&gt; Type.GetType($"Shop.Events.{name}"))
    /// </code>
    /// </example>
    public RabbitMQPlug MessageTypes(Func<string, Type?> lookupType) =>
        With(settings with { Reader = settings.Reader.MessageTypes(lookupType) });

    /// <summary>Returns a plug that shares <see cref="Published"/> and reads the app's own header names.</summary>
    /// <example>
    /// <code>
    /// Messaging.RabbitMQ.Headers(new MessageHeaders { MessageType = "message-type" })
    /// </code>
    /// </example>
    public RabbitMQPlug Headers(MessageHeaders names) => With(settings with { Reader = settings.Reader.Headers(names) });

    /// <summary>Returns a plug that shares <see cref="Published"/> and reads the body with the app's JSON options.</summary>
    /// <example>
    /// <code>
    /// Messaging.RabbitMQ.Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
    /// </code>
    /// </example>
    public RabbitMQPlug Json(JsonSerializerOptions options) => With(settings with { Reader = settings.Reader.Json(options) });

    /// <summary>Returns a plug that shares <see cref="Published"/> and reads each message with your own function, replacing the header reader.</summary>
    /// <example>
    /// <code>
    /// Messaging.RabbitMQ.Read(message =&gt; new Observed(Decode(message.Body), null))
    /// </code>
    /// </example>
    public RabbitMQPlug Read(Func<RabbitMQMessage, Observed> read) => With(settings with { Read = read });

    /// <summary>
    /// Returns a plug that shares <see cref="Published"/> and replaces its catch-up and its check for consumers with your own wait, for apps whose consumers it can't track.
    /// </summary>
    /// <example>
    /// <code>
    /// Messaging.RabbitMQ.CatchingUpWith(ct =&gt; WaitUntilTheQueuesAreEmpty(ct))
    /// </code>
    /// </example>
    public RabbitMQPlug CatchingUpWith(Func<CancellationToken, Task> catchUp) => With(settings with { CatchUp = catchUp });

    /// <inheritdoc />
    public void Plug(PlugRegistration registration)
    {
        registration.Services(services =>
        {
            var host = new HostState(Published);
            services.AddKeyedSingleton(Origin, host);
            services.Decorate<IConnection>(host.Decorate);
        });
        registration.Recording(Published, Observe, settings.CatchUp ?? (ct =>
            new RabbitMQCatchUp(registration.AppServices.GetRequiredKeyedService<HostState>(Origin), registration).Run(ct)));
    }

    private RabbitMQPlug With(PlugSettings changed) => new(Published, changed, Origin);

    private Observed Observe(RabbitMQMessage message) =>
        settings.Read?.Invoke(message)
        ?? settings.Reader.Read(name => HostState.HeaderOf(message.Properties, name), message.Body);
}

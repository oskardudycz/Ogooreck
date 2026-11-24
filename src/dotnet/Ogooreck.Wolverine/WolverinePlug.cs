using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Wolverine;
using Wolverine.Tracking;

namespace Ogooreck.Wolverine;

/// <summary>
/// Plugs Wolverine into a specification: it records each envelope the app publishes, catches up by wrapping each step in Wolverine's tracked session,
/// and sets the publish function so <c>Publish</c> steps go through <see cref="IMessageBus"/>. It works in-process only.
/// </summary>
/// <remarks>
/// It records what the tracked session saw sent, so only messages published while a step runs count. Wolverine's own messages and the app's commands are recorded too;
/// use <see cref="MessageTypes(Type[])"/> to keep only your messages. A test project usually holds one plug in a static field.
/// Wolverine's tracked sessions don't run in parallel, so keep the tests that use the plug in one serial collection.
/// </remarks>
/// <example>
/// <code>
/// public static class Messaging
/// {
///     public static readonly WolverinePlug Wolverine = new();
/// }
///
/// public class ShopApi(): ApiSpecification&lt;Program&gt;(spec =&gt; spec.Use(Messaging.Wolverine));
///
/// ExpectPublishedIn(Messaging.Wolverine.Published, new OrderPlaced(cartId))
/// </code>
/// </example>
public sealed class WolverinePlug: IDerivedPlug
{
    private readonly Func<Type, bool> include;

    /// <summary>Creates a plug that records every message the app publishes. Use <see cref="MessageTypes(Type[])"/> to keep Wolverine's own messages out.</summary>
    /// <example>
    /// <code>
    /// public static readonly WolverinePlug Wolverine = new();
    /// </code>
    /// </example>
    public WolverinePlug() : this(new Recorded<Envelope>(), _ => true, null) { }

    private WolverinePlug(Recorded<Envelope> published, Func<Type, bool> include, object? origin)
    {
        Published = published;
        this.include = include;
        Origin = origin ?? this;
    }

    /// <summary>The envelopes the app published, for <c>ExpectPublished</c>, <c>ExpectNothingPublished</c> and <c>ExpectCausation</c>.</summary>
    /// <example>
    /// <code>
    /// ExpectPublishedIn(Messaging.Wolverine.Published, new OrderPlaced(cartId))
    /// </code>
    /// </example>
    public Recorded<Envelope> Published { get; }

    /// <inheritdoc />
    public object Origin { get; }

    /// <summary>Returns a plug that shares <see cref="Published"/> and records only messages of the given types, or of types derived from them.</summary>
    /// <example>
    /// <code>
    /// Messaging.Wolverine.MessageTypes(typeof(OrderPlaced), typeof(ShipOrder))
    /// </code>
    /// </example>
    public WolverinePlug MessageTypes(params Type[] types) =>
        MessageTypes(type => types.Any(listed => listed.IsAssignableFrom(type)));

    /// <summary>Returns a plug that shares <see cref="Published"/> and records only messages whose type the function accepts.</summary>
    /// <example>
    /// <code>
    /// Messaging.Wolverine.MessageTypes(type =&gt; type.Namespace == typeof(OrderPlaced).Namespace)
    /// </code>
    /// </example>
    public WolverinePlug MessageTypes(Func<Type, bool> accept) => new(Published, accept, Origin);

    /// <inheritdoc />
    public void Plug(PlugRegistration registration)
    {
        var oneSessionAtATime = new SemaphoreSlim(1, 1);
        registration
            .Recording(Published, Read, (step, ct) => Tracked(registration, oneSessionAtATime, step, ct))
            .Publishing((request, _) => Publish(registration, request));
    }

    private async Task Tracked(PlugRegistration registration, SemaphoreSlim oneSessionAtATime,
        Func<CancellationToken, Task> step, CancellationToken ct)
    {
        await oneSessionAtATime.WaitAsync(ct);
        try
        {
            var session = await registration.AppServices
                .TrackActivity(registration.Deadline * 2)
                .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(_ => step(ct)));

            foreach (var envelope in session.Sent.Envelopes().Where(sent => include(sent.Message!.GetType())))
                Published.Add(envelope);
        }
        finally
        {
            oneSessionAtATime.Release();
        }
    }

    private static async Task Publish(PlugRegistration registration, PublishRequest request)
    {
        using var scope = registration.AppServices.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        foreach (var message in request.Messages)
            await bus.PublishAsync(message);
    }

    private static Observed Read(Envelope envelope) =>
        new(envelope.Message!, envelope.ParentId, envelope.Id.ToString(), envelope.CorrelationId,
            envelope.ConversationId == Guid.Empty ? null : envelope.ConversationId.ToString());
}

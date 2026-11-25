using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Wolverine;
using Wolverine.Tracking;

namespace Ogooreck.Wolverine;

/// <summary>
/// Plugins Wolverine into a specification: it records each envelope the app publishes, catches up by wrapping each step in Wolverine's tracked session,
/// and sets the publish function so <c>Publish</c> steps go through <see cref="IMessageBus"/>. It works in-process only.
/// </summary>
/// <remarks>
/// It records what the tracked session saw sent, so only messages published while a step runs count. Wolverine's own messages and the app's commands are recorded too;
/// use <see cref="MessageTypes(Type[])"/> to keep only your messages. A test project usually holds one plugin in a static field.
/// Wolverine's tracked sessions don't run in parallel, so keep the tests that use the plugin in one serial collection.
/// </remarks>
/// <example>
/// <code>
/// public static class Messaging
/// {
///     public static readonly WolverinePlugin Wolverine = new();
/// }
///
/// public class ShopApi(): ApiSpecification&lt;Program&gt;(spec =&gt; spec.Use(Messaging.Wolverine));
///
/// ExpectPublishedIn(Messaging.Wolverine.Published, new OrderPlaced(cartId))
/// </code>
/// </example>
public sealed class WolverinePlugin: IDerivedPlugin
{
    private readonly Func<Type, bool> include;

    /// <summary>Creates a plugin that records every message the app publishes. Use <see cref="MessageTypes(Type[])"/> to keep Wolverine's own messages out.</summary>
    /// <example>
    /// <code>
    /// public static readonly WolverinePlugin Wolverine = new();
    /// </code>
    /// </example>
    public WolverinePlugin() : this(new Recorded<Envelope>(), _ => true, null) { }

    private WolverinePlugin(Recorded<Envelope> published, Func<Type, bool> include, object? origin)
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

    /// <summary>Returns a plugin that shares <see cref="Published"/> and records only messages of the given types, or of types derived from them.</summary>
    /// <example>
    /// <code>
    /// Messaging.Wolverine.MessageTypes(typeof(OrderPlaced), typeof(ShipOrder))
    /// </code>
    /// </example>
    public WolverinePlugin MessageTypes(params Type[] types) =>
        MessageTypes(type => types.Any(listed => listed.IsAssignableFrom(type)));

    /// <summary>Returns a plugin that shares <see cref="Published"/> and records only messages whose type the function accepts.</summary>
    /// <example>
    /// <code>
    /// Messaging.Wolverine.MessageTypes(type =&gt; type.Namespace == typeof(OrderPlaced).Namespace)
    /// </code>
    /// </example>
    public WolverinePlugin MessageTypes(Func<Type, bool> accept) => new(Published, accept, Origin);

    /// <inheritdoc />
    public void Register(SpecificationPluginRegistration registration)
    {
        var oneSessionAtATime = new SemaphoreSlim(1, 1);
        registration
            .Recording(Published, Read, (step, ct) => Tracked(registration, oneSessionAtATime, step, ct))
            .Publishing((request, _) => Publish(registration, request));
    }

    private async Task Tracked(SpecificationPluginRegistration registration, SemaphoreSlim oneSessionAtATime,
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

    private static async Task Publish(SpecificationPluginRegistration registration, PublishRequest request)
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

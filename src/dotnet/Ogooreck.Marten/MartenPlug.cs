using JasperFx.Events;
using Marten;
using Marten.Events;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;

namespace Ogooreck.Marten;

/// <summary>
/// Plugs Marten into a specification: it records each event the app appends, and catches up by waiting for the async projections.
/// It registers a session listener in the test host, so it works in-process only.
/// </summary>
/// <remarks>
/// The catch-up waits with <c>WaitForNonStaleProjectionDataAsync</c> until the specification's deadline, when the store has any events.
/// It calls it with a 50 ms timeout and calls it again after each timeout, because a longer timeout checks the projections only every 250 ms.
/// It turns on <c>UseListenNotifyForEventAppends</c> in the test host, so the async daemon picks up an append at once instead of at its next poll.
/// Use <see cref="WithoutCatchUp"/> for an app without an async daemon.
/// </remarks>
/// <example>
/// <code>
/// public static class Events
/// {
///     public static readonly MartenPlug Marten = new();
/// }
///
/// public class ShopApi(): ApiSpecification&lt;Program&gt;(spec =&gt; spec.Use(Events.Marten));
///
/// ExpectAppendedIn(Events.Marten.Appended, new OrderPlaced(cartId))
/// </code>
/// </example>
public sealed class MartenPlug: IDerivedPlug
{
    private static readonly TimeSpan StaleCheck = TimeSpan.FromMilliseconds(50);
    private readonly bool catchUp;

    /// <summary>Creates a plug that records the appended events and waits for the projections.</summary>
    /// <example>
    /// <code>
    /// public static readonly MartenPlug Marten = new();
    /// </code>
    /// </example>
    public MartenPlug() : this(new Recorded<IEvent>(), catchUp: true, null) { }

    private MartenPlug(Recorded<IEvent> appended, bool catchUp, object? origin)
    {
        Appended = appended;
        this.catchUp = catchUp;
        Origin = origin ?? this;
    }

    /// <summary>The events the app appended, for <c>ExpectAppended</c>, <c>ExpectNothingAppended</c> and <c>ExpectCausation</c>.</summary>
    /// <example>
    /// <code>
    /// ExpectAppendedIn(Events.Marten.Appended, new OrderPlaced(cartId))
    /// </code>
    /// </example>
    public Recorded<IEvent> Appended { get; }

    /// <inheritdoc />
    public object Origin { get; }

    /// <summary>Returns a plug that shares <see cref="Appended"/> and records without catch-up, so <c>ExpectNothingAppended</c> fails at once.</summary>
    /// <example>
    /// <code>
    /// Events.Marten.WithoutCatchUp()
    /// </code>
    /// </example>
    public MartenPlug WithoutCatchUp() => new(Appended, catchUp: false, Origin);

    /// <inheritdoc />
    public void Plug(PlugRegistration registration)
    {
        registration.Services(AddListener);

        if (catchUp)
            registration.Appending(Appended, Read, ct => WaitForProjections(registration, ct));
        else
            registration.Appending(Appended, Read);
    }

    private void AddListener(IServiceCollection services) =>
        services.ConfigureMarten(options =>
        {
            options.Listeners.Add(new AppendedEventsListener(Appended));
            options.Events.UseListenNotifyForEventAppends = true;
        });

    private static async Task WaitForProjections(PlugRegistration registration, CancellationToken ct)
    {
        var store = registration.AppServices.GetRequiredService<IDocumentStore>();
        await using (var session = store.QuerySession())
        {
            if (!await session.Events.QueryAllRawEvents().AnyAsync(ct))
                return;
        }

        var stale = default(TimeoutException);
        try
        {
            while (true)
                try
                {
                    await store.WaitForNonStaleProjectionDataAsync(StaleCheck).WaitAsync(ct);
                    return;
                }
                catch (TimeoutException timeout)
                {
                    stale = timeout;
                }
        }
        catch (OperationCanceledException) when (stale is not null)
        {
            throw stale;
        }
    }

    private static Observed Read(IEvent appended) =>
        new(appended.Data, appended.GetHeader("traceparent")?.ToString(), appended.Id.ToString(),
            appended.CorrelationId, appended.CausationId);

    private sealed class AppendedEventsListener(Recorded<IEvent> appended): DocumentSessionListenerBase
    {
        public override Task AfterCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token)
        {
            foreach (var @event in commit.GetEvents())
                appended.Add(@event);
            return Task.CompletedTask;
        }
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace Ogooreck.API;

/// <summary>
/// What a <see cref="ISpecificationPlugin"/> can add to a specification. Each call does what the specification's call of the same name does,
/// and the plugin's recorders, catch-ups and services run in the order they were added next to the specification's own.
/// </summary>
/// <example>
/// <code>
/// registration
///     .Services(services =&gt; services.Decorate&lt;IProducer&lt;string, string&gt;&gt;(Record))
///     .Recording(Published, Read, ct =&gt; WaitUntilIdle(ct));
/// </code>
/// </example>
public sealed class SpecificationPluginRegistration
{
    private readonly SpecificationSettings settings;
    private readonly object owner;
    private readonly string plugin;
    private readonly bool inProcess;
    private readonly Func<IServiceProvider> appServices;
    private readonly OwnedItems<RecordingSource>.Slot recordings;
    private readonly OwnedItems<Func<CancellationToken, Task>>.Slot catchUps;
    private readonly OwnedItems<Func<Func<CancellationToken, Task>, CancellationToken, Task>>.Slot aroundSteps;
    private readonly OwnedItems<Action<IServiceCollection>>.Slot services;
    private readonly OwnedItems<Func<CancellationToken, Task>>.Slot starting;

    internal SpecificationPluginRegistration(SpecificationSettings settings, object owner, string plugin, bool inProcess,
        Func<IServiceProvider> appServices)
    {
        this.settings = settings;
        this.owner = owner;
        this.plugin = plugin;
        this.inProcess = inProcess;
        this.appServices = appServices;
        recordings = settings.Recordings.Reserve(owner, plugin);
        catchUps = settings.CatchUps.Reserve(owner, plugin);
        aroundSteps = settings.AroundSteps.Reserve(owner, plugin);
        services = settings.Services.Reserve(owner, plugin);
        starting = settings.Starting.Reserve(owner, plugin);
        settings.ReleasePublishing(owner);
    }

    /// <summary>
    /// Adds a configuration of the app's services, applied to the test host after the host customization given to <c>For&lt;Program&gt;(host)</c> or the constructor.
    /// It works only on <c>ApiSpecification&lt;TProgram&gt;</c>; on an out-of-process specification, <c>Use(...)</c> throws, naming the plugin.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.Services(services =&gt; services.AddSingleton&lt;TimeProvider&gt;(clock))
    /// </code>
    /// </example>
    public SpecificationPluginRegistration Services(Action<IServiceCollection> configure)
    {
        if (!inProcess)
            throw new InvalidOperationException(
                $"Plugin {plugin} calls Services(...), but decorating the app's services needs ApiSpecification<TProgram>: an out-of-process app's services are not in the test process, and a host passed to For(host) is already built, so configure its services when you build it.");
        if (settings.IsPluginOfAParent(owner))
            return this;
        if (settings.SharesApp)
            throw new InvalidOperationException(
                $"Plugin {plugin} changes the app's services, so it can't run on the shared app. Add it to the app's setup, or use WithNewApp(...) to start a separate app for it.");
        services.Add(configure);
        return this;
    }

    /// <summary>Adds a source of the messages the app publishes, for <c>ExpectPublished</c> and <c>ExpectNothingPublished</c>, without catch-up. Repeatable.</summary>
    /// <example>
    /// <code>
    /// registration.Recording(Published, observed =&gt; observed)
    /// </code>
    /// </example>
    public SpecificationPluginRegistration Recording<T>(Recorded<T> recorded, Func<T, Observed> read)
    {
        recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: false));
        return this;
    }

    /// <summary>Adds a source of the messages the app publishes, for <c>ExpectPublished</c> and <c>ExpectNothingPublished</c>, with a catch-up function that returns when the tool is idle. Repeatable.</summary>
    /// <example>
    /// <code>
    /// registration.Recording(Published, Read, ct =&gt; WaitUntilIdle(ct))
    /// </code>
    /// </example>
    public SpecificationPluginRegistration Recording<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<CancellationToken, Task> catchUp)
    {
        recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true));
        catchUps.Add(catchUp);
        return this;
    }

    /// <summary>
    /// Adds a source of the messages the app publishes, for <c>ExpectPublished</c> and <c>ExpectNothingPublished</c>, with a catch-up that wraps each step, such as Wolverine's tracked sessions.
    /// <paramref name="aroundStep"/> gets the step as the function to run; catch-up is done when it returns. Repeatable.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.Recording(Published, Read, (step, ct) =&gt; Tracked(registration.AppServices, step, ct))
    /// </code>
    /// </example>
    public SpecificationPluginRegistration Recording<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<Func<CancellationToken, Task>, CancellationToken, Task> aroundStep)
    {
        recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true));
        aroundSteps.Add(aroundStep);
        return this;
    }

    /// <summary>Adds a source of the events the app appends, such as an event store's listener, for <c>ExpectAppended</c> and <c>ExpectNothingAppended</c>, without catch-up. Repeatable.</summary>
    /// <example>
    /// <code>
    /// registration.Appending(Published, observed =&gt; observed)
    /// </code>
    /// </example>
    public SpecificationPluginRegistration Appending<T>(Recorded<T> recorded, Func<T, Observed> read)
    {
        recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: false, RecordingRole.Appended));
        return this;
    }

    /// <summary>Adds a source of the events the app appends, such as an event store's listener, for <c>ExpectAppended</c> and <c>ExpectNothingAppended</c>, with a catch-up function that returns when the tool is idle. Repeatable.</summary>
    /// <example>
    /// <code>
    /// registration.Appending(Published, Read, ct =&gt; WaitUntilIdle(ct))
    /// </code>
    /// </example>
    public SpecificationPluginRegistration Appending<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<CancellationToken, Task> catchUp)
    {
        recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true, RecordingRole.Appended));
        catchUps.Add(catchUp);
        return this;
    }

    /// <summary>
    /// Adds a source of the events the app appends, such as an event store's listener, for <c>ExpectAppended</c> and <c>ExpectNothingAppended</c>, with a catch-up that wraps each step, such as Wolverine's tracked sessions.
    /// <paramref name="aroundStep"/> gets the step as the function to run; catch-up is done when it returns. Repeatable.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.Appending(Published, Read, (step, ct) =&gt; Tracked(registration.AppServices, step, ct))
    /// </code>
    /// </example>
    public SpecificationPluginRegistration Appending<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<Func<CancellationToken, Task>, CancellationToken, Task> aroundStep)
    {
        recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true, RecordingRole.Appended));
        aroundSteps.Add(aroundStep);
        return this;
    }

    /// <summary>Adds a catch-up function that isn't tied to a recorder, run after each step. Repeatable.</summary>
    /// <example>
    /// <code>
    /// registration.CatchUp(ct =&gt; projections.WaitUntilIdle(ct))
    /// </code>
    /// </example>
    public SpecificationPluginRegistration CatchUp(Func<CancellationToken, Task> catchUp)
    {
        catchUps.Add(catchUp);
        return this;
    }

    /// <summary>Sets how <c>Publish</c> steps put messages on the bus. A specification has one publish function, so a second one throws, naming both sources.</summary>
    /// <example>
    /// <code>
    /// registration.Publishing(async (request, ct) =&gt; await bus.PublishAsync(request.Messages))
    /// </code>
    /// </example>
    public SpecificationPluginRegistration Publishing(Func<PublishRequest, CancellationToken, Task> publish)
    {
        settings.SetPublishing(publish, owner, plugin);
        return this;
    }

    /// <summary>
    /// Adds a function that runs once, after the host starts and before the first step, in the order the functions were added.
    /// A failure fails the first step, naming the plugin.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.Starting(ct =&gt; consumer.StartAsync(ct))
    /// </code>
    /// </example>
    public SpecificationPluginRegistration Starting(Func<CancellationToken, Task> start)
    {
        if (!settings.IsPluginOfAParent(owner))
            starting.Add(start);
        return this;
    }

    /// <summary>
    /// The test host's services, once the host has started. Reading it before that, or on an out-of-process specification, throws with a message saying why.
    /// Read it inside the plugin's functions, which run after the host started, not while <see cref="ISpecificationPlugin.Register"/> runs.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.CatchUp(ct =&gt; registration.AppServices.GetRequiredService&lt;IDocumentStore&gt;().WaitForNonStaleProjectionDataAsync(timeout))
    /// </code>
    /// </example>
    public IServiceProvider AppServices => appServices();

    /// <summary>The deadline of the specification running a step, or of a variant of it running one, so a plugin can bound its tool's own waits with it.</summary>
    /// <example>
    /// <code>
    /// await store.WaitForNonStaleProjectionDataAsync(registration.Deadline);
    /// </code>
    /// </example>
    public TimeSpan Deadline => settings.RunningOrSelf.Deadline;

    /// <summary>The clock of the specification running a step, or of a variant of it running one, so a plugin measures its tool's waits with the same clock as the specification's deadline.</summary>
    /// <example>
    /// <code>
    /// await Task.Delay(PollInterval, registration.Clock, ct);
    /// </code>
    /// </example>
    public TimeProvider Clock => settings.RunningOrSelf.Clock;
}

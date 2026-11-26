using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.Assertions;

namespace Ogooreck.API;

internal sealed class SpecificationSettings
{
    private static readonly AsyncLocal<SpecificationSettings?> RunningOne = new();
    private readonly SemaphoreSlim startingGate = new(1, 1);
    private bool started;
    private volatile bool startingDone;
    private Exception? startingFailure;
    private (object? Owner, string Name)? publishingSource;
    private readonly HashSet<object> plugins = [];
    private SpecificationSettings? app;
    private SpecificationSettings? parent;
    private bool publishingInherited;

    public OwnedItems<RecordingSource> Recordings { get; } = new();

    public OwnedItems<Func<CancellationToken, Task>> CatchUps { get; } = new();

    public OwnedItems<Func<Func<CancellationToken, Task>, CancellationToken, Task>> AroundSteps { get; } = new();

    public OwnedItems<Action<IServiceCollection>> Services { get; } = new();

    public OwnedItems<Func<CancellationToken, Task>> Starting { get; } = new();

    public TimeSpan Deadline { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan StartTimeout { get; set; } = TimeSpan.FromMinutes(3);

    public TimeProvider Clock { get; set; } = TimeProvider.System;

    public Func<PublishRequest, CancellationToken, Task>? Publishing { get; private set; }

    public void SetPublishing(Func<PublishRequest, CancellationToken, Task> publish, object? owner, string name)
    {
        if (publishingSource is { } existing && !publishingInherited)
            throw new InvalidOperationException(
                $"{name} can't set the publish function, because {existing.Name} already did: a specification has one publish function.");
        Publishing = publish;
        publishingSource = (owner, name);
        publishingInherited = false;
    }

    public void ReleasePublishing(object owner)
    {
        if (publishingSource is null || !ReferenceEquals(publishingSource.Value.Owner, owner))
            return;
        Publishing = null;
        publishingSource = null;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        foreach (var configure in Services.Items)
            configure(services);
    }

    public bool SharesApp => app is not null;

    public static SpecificationSettings? Running
    {
        get => RunningOne.Value;
        set => RunningOne.Value = value;
    }

    public SpecificationSettings RunningOrSelf =>
        Running is { } running && running.IsOrDescendsFrom(this) ? running : this;

    private bool IsOrDescendsFrom(SpecificationSettings other) =>
        ReferenceEquals(this, other) || (parent is not null && parent.IsOrDescendsFrom(other));

    public bool IsPluginOfAParent(object owner) => parent is not null && (parent.plugins.Contains(owner) || parent.IsPluginOfAParent(owner));

    public SpecificationSettings Variant()
    {
        var variant = TestSetupCopy();
        variant.app = app ?? this;
        variant.parent = this;
        return variant;
    }

    public SpecificationSettings TestSetupCopy()
    {
        var copy = new SpecificationSettings
        {
            Deadline = Deadline,
            StartTimeout = StartTimeout,
            Clock = Clock,
            Json = Json,
            Publishing = Publishing,
            publishingSource = publishingSource,
            publishingInherited = publishingSource is not null,
            Telemetry = Telemetry,
            Spans = Spans,
            Measurements = Measurements,
            ownsTelemetry = false,
            telemetryOwner = telemetryOwner ?? this
        };
        Recordings.CopyTo(copy.Recordings);
        CatchUps.CopyTo(copy.CatchUps);
        AroundSteps.CopyTo(copy.AroundSteps);
        return copy;
    }

    public void Use(ISpecificationPlugin plugin, bool inProcess, Func<IServiceProvider> appServices)
    {
        EnsureConfigurable("Use(...)");
        var owner = plugin is IDerivedPlugin derived ? derived.Origin : plugin;
        plugins.Add(owner);
        var registration = new SpecificationPluginRegistration(this, owner, plugin.GetType().Name, inProcess, appServices);
        plugin.Register(registration);
    }

    public bool Started => startingDone && (parent?.Started ?? true);

    public async Task EnsureStarted(string step, Diagnostics diagnostics, CancellationToken ct)
    {
        if (parent is not null)
            await parent.EnsureStarted(step, diagnostics, ct);
        await startingGate.WaitAsync(ct);
        try
        {
            if (startingFailure is not null)
                throw startingFailure;
            if (startingDone)
                return;

            foreach (var (name, start) in Starting.Named)
            {
                try
                {
                    using (var plugin = Diagnostics.Start($"start plugin {name}", null, diagnostics))
                        await plugin.Within(() => start(ct));
                }
                catch (Exception exception)
                {
                    startingFailure = new AssertionException(
                        $"{step}: plugin {name} failed to start: {exception.Message}", exception);
                    throw startingFailure;
                }
            }

            startingDone = true;
        }
        finally
        {
            startingGate.Release();
        }
    }

    public ITelemetrySource Telemetry { get; set; } = TelemetrySources.InProcess();

    private bool ownsTelemetry = true;
    private SpecificationSettings? telemetryOwner;
    private bool telemetryAttached;
    private readonly Lock telemetryGate = new();

    public void UseTelemetry(ITelemetrySource source)
    {
        Telemetry = source;
        ownsTelemetry = false;
        telemetryOwner = null;
        Spans = new Recorded<RecordedSpan>();
        Measurements = new Recorded<RecordedMeasurement>();
    }

    public ValueTask DisposeOwnedTelemetry() => ownsTelemetry ? Telemetry.DisposeAsync() : ValueTask.CompletedTask;

    public JsonSerializerOptions? Json { get; set; }

    public Recorded<RecordedSpan> Spans { get; private set; } = new();

    public Recorded<RecordedMeasurement> Measurements { get; private set; } = new();

    public int RecordedCount => Recordings.Items.Sum(recording => recording.Count);

    public void EnsureConfigurable(string call)
    {
        if (started)
            throw new InvalidOperationException(
                $"{call} was called after the specification ran its first step, but configuration must happen before the first step. Use With(...) for other test setup.");
    }

    public void Start()
    {
        started = true;
        AttachTelemetry();
    }

    private void AttachTelemetry()
    {
        if (telemetryOwner is not null)
        {
            telemetryOwner.AttachTelemetry();
            return;
        }

        lock (telemetryGate)
        {
            if (telemetryAttached)
                return;
            Telemetry.Attach(Spans, Measurements);
            telemetryAttached = true;
        }
    }
}

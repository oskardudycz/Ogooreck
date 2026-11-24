using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Ogooreck.API;

/// <summary>
/// The in-process specification for <see cref="WebApplicationFactory{TEntryPoint}"/>. The host starts on the first step and is reused until disposal.
/// </summary>
/// <remarks>
/// A specification created with <c>For&lt;Program&gt;()</c>, <c>new()</c> or a subclass owns one app, and its host customization is given when it is created.
/// <see cref="With"/> makes a specification that runs on that same app with other test setup, and <see cref="WithNewApp(Func{ApiSpecification{TProgram}, ApiSpecification{TProgram}})"/> is the explicit way to start a separate app, which costs an app start.
/// </remarks>
public class ApiSpecification<TProgram>: IDisposable, IAsyncDisposable where TProgram : class
{
    private readonly WebApplicationFactory<TProgram> defaultFactory = new();
    private readonly Lazy<StartedHost> host;
    private readonly SpecificationSettings settings;
    private readonly ApiSpecification<TProgram>? app;
    private readonly List<IPlug> plugs = [];
    private Func<WebApplicationFactory<TProgram>, WebApplicationFactory<TProgram>> customise = factory => factory;
    private WebApplicationFactory<TProgram>? factory;

    /// <summary>Creates a specification with all defaults, so it works directly as a class fixture.</summary>
    /// <example>
    /// <code>
    /// public class ShopApi: ApiSpecification&lt;Program&gt;;
    ///
    /// public class CartTests(ShopApi api): IClassFixture&lt;ShopApi&gt;;
    /// </code>
    /// </example>
    public ApiSpecification()
    {
        settings = new SpecificationSettings();
        host = new Lazy<StartedHost>(Start);
    }

    private ApiSpecification(ApiSpecification<TProgram> app, SpecificationSettings settings)
    {
        this.app = app;
        this.settings = settings;
        host = new Lazy<StartedHost>(() =>
        {
            settings.Start();
            return app.host.Value;
        });
    }

    /// <summary>Creates a specification configured by <paramref name="configure"/>, for subclasses.</summary>
    /// <example>
    /// <code>
    /// public class AppApi: ApiSpecification&lt;Program&gt;
    /// {
    ///     public AppApi() : base(spec =&gt; ((AppApi)spec).Configure()) { }
    /// }
    /// </code>
    /// </example>
    protected ApiSpecification(Func<ApiSpecification<TProgram>, ApiSpecification<TProgram>> configure) : this() =>
        configure(this);

    /// <summary>
    /// Creates a specification whose app is customized by <paramref name="host"/>, for example with <c>WithWebHostBuilder</c>, then configured by <paramref name="configure"/>. For subclasses.
    /// </summary>
    /// <example>
    /// <code>
    /// public class ShopApi(): ApiSpecification&lt;Program&gt;(
    ///     factory =&gt; factory.WithWebHostBuilder(builder =&gt; builder.UseSetting("RabbitMQ:Exchange", "shop")),
    ///     spec =&gt; spec.Use(Messaging.RabbitMQ));
    /// </code>
    /// </example>
    protected ApiSpecification(Func<WebApplicationFactory<TProgram>, WebApplicationFactory<TProgram>> host,
        Func<ApiSpecification<TProgram>, ApiSpecification<TProgram>> configure) : this()
    {
        customise = host;
        configure(this);
    }

    internal ApiSpecification(Func<WebApplicationFactory<TProgram>, WebApplicationFactory<TProgram>> host) : this() =>
        customise = host;

    /// <summary>
    /// Returns a specification that runs on this specification's app, with its test setup copied and then changed by <paramref name="configure"/>:
    /// the deadline, recordings, catch-ups, publishing, telemetry, JSON options and plug options. It starts no app of its own, and disposing it leaves the app running.
    /// The host can't be changed here. A plug that changes the app's services and isn't in the app's setup throws, saying to add it to the app's setup or to use <c>WithNewApp(...)</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// public class ForeignHeadersTests(ShopApi shop)
    /// {
    ///     private readonly ApiSpecification&lt;Program&gt; api =
    ///         shop.With(spec =&gt; spec.Use(Messaging.RabbitMQ.Headers(new MessageHeaders { MessageType = "message-type" })));
    /// }
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> With(Func<ApiSpecification<TProgram>, ApiSpecification<TProgram>> configure)
    {
        var variant = new ApiSpecification<TProgram>(app ?? this, settings.Variant());
        if (app is not null)
            variant.plugs.AddRange(plugs);
        return configure(variant);
    }

    /// <summary>
    /// Starts a separate app: a specification with this one's host customization, plugs and test setup, then changed by <paramref name="configure"/>, which may add plugs that change the app's services.
    /// Each one costs an app start, so use it only when the app itself must differ, and dispose it.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var separate = shop.WithNewApp(spec =&gt; spec.Use(new FakeClock()));
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> WithNewApp(Func<ApiSpecification<TProgram>, ApiSpecification<TProgram>> configure) =>
        WithNewApp(factory => factory, configure);

    /// <summary>
    /// Starts a separate app, as <see cref="WithNewApp(Func{ApiSpecification{TProgram}, ApiSpecification{TProgram}})"/> does, whose host is also customized by <paramref name="host"/>, after this specification's own customization.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var concurrent = shop.WithNewApp(
    ///     factory =&gt; factory.WithWebHostBuilder(builder =&gt; builder.UseSetting("RabbitMQ:DispatchConcurrency", "4")),
    ///     spec =&gt; spec);
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> WithNewApp(Func<WebApplicationFactory<TProgram>, WebApplicationFactory<TProgram>> host,
        Func<ApiSpecification<TProgram>, ApiSpecification<TProgram>> configure)
    {
        var root = app ?? this;
        var separate = new ApiSpecification<TProgram>(settings.TestSetupCopy())
        {
            customise = factory => host(root.customise(factory))
        };
        foreach (var plug in root.plugs.Concat(app is null ? [] : plugs))
            separate.Use(plug);
        return configure(separate);
    }

    private ApiSpecification(SpecificationSettings testSetup)
    {
        settings = testSetup;
        host = new Lazy<StartedHost>(Start);
    }

    /// <summary>
    /// Adds a plug that wires one tool into the specification: its recorders, catch-ups, publish function and changes to the app's services.
    /// Repeatable, and plugs mix freely with direct configuration calls. A plug made from another one through an option replaces it.
    /// </summary>
    /// <example>
    /// <code>
    /// For&lt;Program&gt;().Use(Messaging.Wolverine).Use(clock)
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Use(IPlug plug)
    {
        settings.Use(plug, inProcess: true, AppServices);
        plugs.Add(plug);
        return this;
    }

    /// <summary>Adds a source of the messages the app publishes, for <c>ExpectPublished</c> and <c>ExpectNothingPublished</c>, without catch-up. Repeatable.</summary>
    /// <example>
    /// <code>
    /// spec.Recording(Items, observed =&gt; observed)
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Recording<T>(Recorded<T> recorded, Func<T, Observed> read)
    {
        settings.EnsureConfigurable("Recording(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: false));
        return this;
    }

    /// <summary>Adds a source of the messages the app publishes, for <c>ExpectPublished</c> and <c>ExpectNothingPublished</c>, with a catch-up function that returns when the tool is idle. Repeatable.</summary>
    /// <example>
    /// <code>
    /// spec.Recording(Items, observed =&gt; observed, _ =&gt; Task.CompletedTask)
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Recording<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<CancellationToken, Task> catchUp)
    {
        settings.EnsureConfigurable("Recording(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true));
        settings.CatchUps.Add(catchUp);
        return this;
    }

    /// <summary>
    /// Adds a source of the messages the app publishes, for <c>ExpectPublished</c> and <c>ExpectNothingPublished</c>, with a catch-up that wraps each step, such as Wolverine's tracked sessions.
    /// <paramref name="aroundStep"/> gets the step as the function to run; catch-up is done when it returns. Repeatable.
    /// </summary>
    /// <example>
    /// <code>
    /// Recording(PublishedMessages, Read, (step, ct) =&gt; Tracked(Services, timeout, step, ct))
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Recording<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<Func<CancellationToken, Task>, CancellationToken, Task> aroundStep)
    {
        settings.EnsureConfigurable("Recording(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true));
        settings.AroundSteps.Add(aroundStep);
        return this;
    }

    /// <summary>Adds a source of the events the app appends, such as an event store's listener, for <c>ExpectAppended</c> and <c>ExpectNothingAppended</c>, without catch-up. Repeatable.</summary>
    /// <example>
    /// <code>
    /// spec.Appending(Items, observed =&gt; observed)
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Appending<T>(Recorded<T> recorded, Func<T, Observed> read)
    {
        settings.EnsureConfigurable("Appending(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: false, RecordingRole.Appended));
        return this;
    }

    /// <summary>Adds a source of the events the app appends, such as an event store's listener, for <c>ExpectAppended</c> and <c>ExpectNothingAppended</c>, with a catch-up function that returns when the tool is idle. Repeatable.</summary>
    /// <example>
    /// <code>
    /// spec.Appending(Items, observed =&gt; observed, _ =&gt; Task.CompletedTask)
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Appending<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<CancellationToken, Task> catchUp)
    {
        settings.EnsureConfigurable("Appending(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true, RecordingRole.Appended));
        settings.CatchUps.Add(catchUp);
        return this;
    }

    /// <summary>
    /// Adds a source of the events the app appends, such as an event store's listener, for <c>ExpectAppended</c> and <c>ExpectNothingAppended</c>, with a catch-up that wraps each step, such as Wolverine's tracked sessions.
    /// <paramref name="aroundStep"/> gets the step as the function to run; catch-up is done when it returns. Repeatable.
    /// </summary>
    /// <example>
    /// <code>
    /// Appending(PublishedMessages, Read, (step, ct) =&gt; Tracked(Services, timeout, step, ct))
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Appending<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<Func<CancellationToken, Task>, CancellationToken, Task> aroundStep)
    {
        settings.EnsureConfigurable("Appending(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true, RecordingRole.Appended));
        settings.AroundSteps.Add(aroundStep);
        return this;
    }

    /// <summary>Adds a catch-up function that isn't tied to a recorder, run after each step. Repeatable.</summary>
    /// <example>
    /// <code>
    /// For&lt;Program&gt;().CatchUp(ct =&gt; projections.WaitUntilIdle(ct))
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> CatchUp(Func<CancellationToken, Task> catchUp)
    {
        settings.EnsureConfigurable("CatchUp(...)");
        settings.CatchUps.Add(catchUp);
        return this;
    }

    /// <summary>Adds a catch-up that wraps each step and isn't tied to a recorder. Catch-up is done when it returns. Repeatable.</summary>
    /// <example>
    /// <code>
    /// For&lt;Program&gt;().CatchUp((step, ct) =&gt; services.TrackActivity(timeout).ExecuteAndWaitAsync(_ =&gt; step(ct)))
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> CatchUp(Func<Func<CancellationToken, Task>, CancellationToken, Task> aroundStep)
    {
        settings.EnsureConfigurable("CatchUp(...)");
        settings.AroundSteps.Add(aroundStep);
        return this;
    }

    /// <summary>Sets how <c>Publish</c> steps put messages on the bus.</summary>
    /// <example>
    /// <code>
    /// For&lt;Program&gt;().Publishing(async (request, ct) =&gt;
    /// {
    ///     using var scope = Services.CreateScope();
    ///     var bus = scope.ServiceProvider.GetRequiredService&lt;IMessageBus&gt;();
    ///     foreach (var message in request.Messages)
    ///         await bus.PublishAsync(message);
    /// })
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Publishing(Func<PublishRequest, CancellationToken, Task> publish)
    {
        settings.EnsureConfigurable("Publishing(...)");
        settings.SetPublishing(publish, null, "a direct Publishing(...) call");
        return this;
    }

    /// <summary>Sets the bound that makes a waiting operation fail, 30 seconds by default. It never makes a passing test wait.</summary>
    /// <example>
    /// <code>
    /// For&lt;Program&gt;().Deadline(TimeSpan.FromSeconds(2))
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Deadline(TimeSpan deadline)
    {
        settings.EnsureConfigurable("Deadline(...)");
        settings.Deadline = deadline;
        return this;
    }

    /// <summary>
    /// Sets the clock that deadlines and waits use, <see cref="TimeProvider.System"/> by default. A test clock makes a test of a deadline run without waiting for real time.
    /// </summary>
    /// <example>
    /// <code>
    /// spec.Clock(testClock).Deadline(TimeSpan.FromHours(1))
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Clock(TimeProvider clock)
    {
        settings.EnsureConfigurable("Clock(...)");
        settings.Clock = clock;
        return this;
    }

    /// <summary>
    /// Sets where <c>ExpectTrace</c>, <c>ExpectCorrelatedTrace</c> and <c>ExpectMetric</c> get spans and measurements from.
    /// The default is a new <see cref="TelemetrySources.InProcess"/> source; pass <see cref="TelemetrySources.Off"/> to stop listening, or your own <see cref="ITelemetrySource"/>.
    /// The specification attaches the source on its first step. The source belongs to the caller, who disposes it; the specification disposes only its default. Call it before the first step.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var spec = ApiSpecification.For&lt;Program&gt;().Telemetry(TelemetrySources.Off);
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Telemetry(ITelemetrySource source)
    {
        settings.EnsureConfigurable("Telemetry(...)");
        settings.UseTelemetry(source);
        return this;
    }

    /// <summary>
    /// Sets the JSON options that <c>Body(...)</c> uses to serialize the expected value, replacing the options taken from the app's services.
    /// Use it when a response doesn't follow the app's JSON settings, for example an export in snake case. Call it before the first step.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var spec = ApiSpecification.For&lt;Program&gt;()
    ///     .Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    /// </code>
    /// </example>
    public ApiSpecification<TProgram> Json(JsonSerializerOptions options)
    {
        settings.EnsureConfigurable("Json(...)");
        settings.Json = options;
        return this;
    }

    /// <summary>Sets the Given steps.</summary>
    /// <example>
    /// <code>
    /// await api.Given(CreateCart(), AddProduct(cartId, "socks")).When(ConfirmCart(cartId)).Then([ExpectResponse(OK)], ct);
    /// </code>
    /// </example>
    public GivenApiSpecificationBuilder Given(params TestStep[] steps) => new(host, settings, steps);

    /// <summary>Sets a named group of Given steps followed by more steps.</summary>
    /// <example>
    /// <code>
    /// await api.Given(paidOrder, ShipOrder(orderId)).When(GetOrder(orderId)).Then([ExpectResponse(OK)], ct);
    /// </code>
    /// </example>
    public GivenApiSpecificationBuilder Given(TestStep[] scenario, params TestStep[] more) =>
        Given(ApiSpecification.Scenario(scenario, more));

    private IServiceProvider AppServices() =>
        app is not null
            ? app.AppServices()
            : host.IsValueCreated
            ? factory!.Services
            : throw new InvalidOperationException(
                "The host has not started yet: AppServices is available after the first step has started it. Read it inside a function the plug passes to the registration, such as a catch-up or a Starting function.");

    private StartedHost Start()
    {
        settings.Start();
        factory = customise(defaultFactory);
        if (settings.Services.Items.Length > 0)
            factory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(settings.ConfigureServices));
        return new StartedHost(
            factory.Server.CreateHandler(),
            factory.Server.BaseAddress,
            ApiJsonSettings.FromServices(factory.Services).Response);
    }

    /// <summary>Stops the host and disposes the default in-process telemetry, so its listeners stop recording. A source passed to <c>Telemetry(...)</c> belongs to the caller. A class fixture is disposed by the test framework.</summary>
    /// <example>
    /// <code>
    /// await using var spec = ApiSpecification.For&lt;Program&gt;();
    /// </code>
    /// </example>
    public void Dispose()
    {
        if (app is not null)
        {
            settings.DisposeOwnedTelemetry().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
            return;
        }

        if (host.IsValueCreated)
            host.Value.Handler.Dispose();
        factory?.Dispose();
        defaultFactory.Dispose();
        settings.DisposeOwnedTelemetry().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    /// <summary>Stops the host and disposes the default in-process telemetry, so its listeners stop recording. A source passed to <c>Telemetry(...)</c> belongs to the caller. A class fixture is disposed by the test framework.</summary>
    /// <example>
    /// <code>
    /// await using var spec = ApiSpecification.For&lt;Program&gt;();
    /// </code>
    /// </example>
    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await settings.DisposeOwnedTelemetry();
            GC.SuppressFinalize(this);
            return;
        }

        if (host.IsValueCreated)
            host.Value.Handler.Dispose();
        if (factory is not null)
            await factory.DisposeAsync();
        await defaultFactory.DisposeAsync();
        await settings.DisposeOwnedTelemetry();
        GC.SuppressFinalize(this);
    }
}

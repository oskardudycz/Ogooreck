using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ogooreck.Assertions;

namespace Ogooreck.API;

/// <remarks>
/// As an instance, it is the specification for an app hosted outside the test process, such as an Aspire app, reached by its base address.
/// It builds its own client for that address. Use <see cref="For(Func{Task{Uri}})"/> for a fixture, or subclass it and pass the address to the base constructor.
/// The defaults are JSON web defaults, telemetry off and a 30-second deadline. The app is not Ogooreck's: disposing the specification disposes its handler only, and the caller stops the app and disposes the telemetry source or <see cref="OtlpReceiver"/> it passed to <c>Telemetry(...)</c>.
/// </remarks>
public partial class ApiSpecification: IDisposable, IAsyncDisposable
{
    private readonly SpecificationSettings settings = new() { Telemetry = TelemetrySources.Off };
    private readonly Lazy<StartedHost> host;
    private Func<Task<Uri>>? baseAddress;
    private Func<HttpMessageHandler>? handlerFactory;
    private IServiceProvider? hostServices;

    private ApiSpecification() => host = new Lazy<StartedHost>(Start);

    /// <summary>Creates a specification configured by <paramref name="configure"/>, which must call <c>Host(...)</c>. For subclasses.</summary>
    /// <example>
    /// <code>
    /// public sealed class AspireApi(): ApiSpecification(spec =&gt; spec.Host(async () =&gt; app.GetEndpoint("api")));
    /// </code>
    /// </example>
    protected ApiSpecification(Func<ApiSpecification, ApiSpecification> configure) : this() =>
        configure(this);

    /// <summary>Creates a specification for the host at <paramref name="baseAddress"/>, then applies <paramref name="configure"/>. For subclasses.</summary>
    /// <example>
    /// <code>
    /// public sealed class AspireApi(): ApiSpecification(async () =&gt; app.GetEndpoint("api"), spec =&gt; spec.Telemetry(Otlp));
    /// </code>
    /// </example>
    protected ApiSpecification(Func<Task<Uri>> baseAddress, Func<ApiSpecification, ApiSpecification> configure)
        : this()
    {
        this.baseAddress = baseAddress;
        configure(this);
    }

    /// <summary>
    /// Creates a specification for the host at <paramref name="baseAddress"/> whose requests go through the handler from <paramref name="handler"/>,
    /// then applies <paramref name="configure"/>. For subclasses.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class AspireApi(): ApiSpecification(
    ///     async () =&gt; app.GetEndpoint("api"), () =&gt; new LoggingHandler(), spec =&gt; spec.Telemetry(Otlp));
    /// </code>
    /// </example>
    protected ApiSpecification(Func<Task<Uri>> baseAddress, Func<HttpMessageHandler> handler,
        Func<ApiSpecification, ApiSpecification> configure) : this()
    {
        this.baseAddress = baseAddress;
        handlerFactory = handler;
        configure(this);
    }

    /// <summary>
    /// Creates a specification for an app hosted at the address the function returns. The function runs once, on the first step.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var api = ApiSpecification.For(async () => app.GetEndpoint("api"));
    /// </code>
    /// </example>
    public static ApiSpecification For(Func<Task<Uri>> baseAddress) => new() { baseAddress = baseAddress };

    /// <summary>
    /// Creates a specification for an app hosted at the address the function returns, sending requests through the handler the factory returns.
    /// The factory runs once, on the first step, and Ogooreck puts its own handler, which sets the step's <c>traceparent</c>, in front of the handler.
    /// A <see cref="DelegatingHandler"/> without an inner handler gets a <see cref="SocketsHttpHandler"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// ApiSpecification.For(async () =&gt; app.GetEndpoint("api"), () =&gt; new LoggingHandler())
    /// </code>
    /// </example>
    public static ApiSpecification For(Func<Task<Uri>> baseAddress, Func<HttpMessageHandler> handler) =>
        new() { baseAddress = baseAddress, handlerFactory = handler };

    /// <summary>
    /// Creates a specification for an app the test builds itself, without a <c>Program</c> class: a started host whose web host uses <c>UseTestServer()</c>.
    /// Requests go to its test server. It takes JSON options from the host's services and listens to traces and metrics in-process, as <c>For&lt;Program&gt;()</c> does,
    /// and plugins can read <see cref="SpecificationPluginRegistration.AppServices"/>. The caller owns the host: it configures the app's services when it builds the host, and stops and disposes it.
    /// A host without a test server throws, naming <c>UseTestServer()</c> and <see cref="For(Func{Task{Uri}})"/> for an app that listens on a port.
    /// </summary>
    /// <example>
    /// <code>
    /// using var host = await new HostBuilder()
    ///     .ConfigureWebHost(web =&gt; web.UseTestServer()
    ///         .ConfigureServices(services =&gt; services.AddRouting())
    ///         .Configure(app =&gt; app.UseRouting().UseEndpoints(endpoints =&gt; endpoints.MapGet("/hello", () =&gt; "Hello"))))
    ///     .StartAsync();
    /// await using var api = ApiSpecification.For(host);
    /// </code>
    /// </example>
    public static ApiSpecification For(IHost host)
    {
        if (host.Services.GetService<IServer>() is not TestServer server)
            throw new ArgumentException(
                "The host has no test server. Call UseTestServer() on its web host builder, or use ApiSpecification.For(async () => address) for an app that listens on a port.",
                nameof(host));
        var specification = new ApiSpecification
        {
            baseAddress = () => Task.FromResult(server.BaseAddress),
            handlerFactory = server.CreateHandler,
            hostServices = host.Services
        };
        specification.settings.Telemetry = TelemetrySources.InProcess();
        return specification;
    }

    /// <summary>Sets or replaces the address of the host. The function runs once, on the first step. Call it before the first step.</summary>
    /// <example>
    /// <code>
    /// public sealed class AspireApi(): ApiSpecification(spec => spec.Host(async () => app.GetEndpoint("api")));
    /// </code>
    /// </example>
    public ApiSpecification Host(Func<Task<Uri>> address)
    {
        settings.EnsureConfigurable("Host(...)");
        baseAddress = address;
        handlerFactory = null;
        hostServices = null;
        return this;
    }

    /// <summary>Sets or replaces the address of the host and the handler that requests go through. Call it before the first step.</summary>
    /// <example>
    /// <code>
    /// spec.Host(async () =&gt; app.GetEndpoint("api"), () =&gt; new LoggingHandler())
    /// </code>
    /// </example>
    public ApiSpecification Host(Func<Task<Uri>> address, Func<HttpMessageHandler> handler)
    {
        settings.EnsureConfigurable("Host(...)");
        baseAddress = address;
        handlerFactory = handler;
        hostServices = null;
        return this;
    }

    /// <summary>
    /// Adds a plugin that wires one tool into the specification: its recorders, catch-ups and publish function. Repeatable.
    /// A plugin made from another one through an option replaces it. A plugin that changes the app's services needs <c>ApiSpecification&lt;TProgram&gt;</c>,
    /// so <c>Use</c> throws for it, naming the plugin.
    /// </summary>
    /// <example>
    /// <code>
    /// ApiSpecification.For(async () =&gt; app.GetEndpoint("api")).Use(Messaging.Kafka)
    /// </code>
    /// </example>
    public ApiSpecification Use(ISpecificationPlugin plugin)
    {
        settings.Use(plugin, inProcess: false,
            () => hostServices ?? throw new InvalidOperationException(
                "AppServices needs ApiSpecification<TProgram>: an out-of-process app's services are not in the test process."));
        return this;
    }

    /// <summary>Adds a source of the messages the app publishes, for <c>ExpectPublished</c> and <c>ExpectNothingPublished</c>, without catch-up. Repeatable.</summary>
    /// <example>
    /// <code>
    /// spec.Recording(consumedOrders, message =&gt; new Observed(message, null))
    /// </code>
    /// </example>
    public ApiSpecification Recording<T>(Recorded<T> recorded, Func<T, Observed> read)
    {
        settings.EnsureConfigurable("Recording(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: false));
        return this;
    }

    /// <summary>Adds a source of the messages the app publishes, for <c>ExpectPublished</c> and <c>ExpectNothingPublished</c>, with a catch-up function that returns when the tool is idle. Repeatable.</summary>
    /// <example>
    /// <code>
    /// spec.Recording(consumedOrders, message =&gt; new Observed(message, null), ct =&gt; kafka.WaitUntilIdle(ct))
    /// </code>
    /// </example>
    public ApiSpecification Recording<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<CancellationToken, Task> catchUp)
    {
        settings.EnsureConfigurable("Recording(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true));
        settings.CatchUps.Add(catchUp);
        return this;
    }

    /// <summary>
    /// Adds a source of the messages the app publishes, for <c>ExpectPublished</c> and <c>ExpectNothingPublished</c>, with a catch-up that wraps each step.
    /// <paramref name="aroundStep"/> gets the step as the function to run; catch-up is done when it returns. Repeatable.
    /// </summary>
    /// <example>
    /// <code>
    /// spec.Recording(consumedOrders, message =&gt; new Observed(message, null), (step, ct) =&gt; kafka.Tracking(step, ct))
    /// </code>
    /// </example>
    public ApiSpecification Recording<T>(Recorded<T> recorded, Func<T, Observed> read,
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
    /// spec.Appending(consumedOrders, message =&gt; new Observed(message, null))
    /// </code>
    /// </example>
    public ApiSpecification Appending<T>(Recorded<T> recorded, Func<T, Observed> read)
    {
        settings.EnsureConfigurable("Appending(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: false, RecordingRole.Appended));
        return this;
    }

    /// <summary>Adds a source of the events the app appends, such as an event store's listener, for <c>ExpectAppended</c> and <c>ExpectNothingAppended</c>, with a catch-up function that returns when the tool is idle. Repeatable.</summary>
    /// <example>
    /// <code>
    /// spec.Appending(consumedOrders, message =&gt; new Observed(message, null), ct =&gt; kafka.WaitUntilIdle(ct))
    /// </code>
    /// </example>
    public ApiSpecification Appending<T>(Recorded<T> recorded, Func<T, Observed> read,
        Func<CancellationToken, Task> catchUp)
    {
        settings.EnsureConfigurable("Appending(...)");
        settings.Recordings.Add(RecordingSource.For(recorded, read, hasCatchUp: true, RecordingRole.Appended));
        settings.CatchUps.Add(catchUp);
        return this;
    }

    /// <summary>
    /// Adds a source of the events the app appends, such as an event store's listener, for <c>ExpectAppended</c> and <c>ExpectNothingAppended</c>, with a catch-up that wraps each step.
    /// <paramref name="aroundStep"/> gets the step as the function to run; catch-up is done when it returns. Repeatable.
    /// </summary>
    /// <example>
    /// <code>
    /// spec.Appending(consumedOrders, message =&gt; new Observed(message, null), (step, ct) =&gt; kafka.Tracking(step, ct))
    /// </code>
    /// </example>
    public ApiSpecification Appending<T>(Recorded<T> recorded, Func<T, Observed> read,
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
    /// spec.CatchUp(ct =&gt; kafka.WaitUntilIdle(ct))
    /// </code>
    /// </example>
    public ApiSpecification CatchUp(Func<CancellationToken, Task> catchUp)
    {
        settings.EnsureConfigurable("CatchUp(...)");
        settings.CatchUps.Add(catchUp);
        return this;
    }

    /// <summary>Adds a catch-up that wraps each step and isn't tied to a recorder. Catch-up is done when it returns. Repeatable.</summary>
    /// <example>
    /// <code>
    /// spec.CatchUp((step, ct) =&gt; kafka.Tracking(step, ct))
    /// </code>
    /// </example>
    public ApiSpecification CatchUp(Func<Func<CancellationToken, Task>, CancellationToken, Task> aroundStep)
    {
        settings.EnsureConfigurable("CatchUp(...)");
        settings.AroundSteps.Add(aroundStep);
        return this;
    }

    /// <summary>Sets how <c>Publish</c> steps put messages on the bus.</summary>
    /// <example>
    /// <code>
    /// spec.Publishing((request, ct) =&gt; producer.ProduceAsync(request.Messages, request.Traceparent, ct))
    /// </code>
    /// </example>
    public ApiSpecification Publishing(Func<PublishRequest, CancellationToken, Task> publish)
    {
        settings.EnsureConfigurable("Publishing(...)");
        settings.SetPublishing(publish, null, "a direct Publishing(...) call");
        return this;
    }

    /// <summary>Sets the bound that makes a waiting operation fail, 30 seconds by default. It never makes a passing test wait.</summary>
    /// <example>
    /// <code>
    /// spec.Deadline(TimeSpan.FromMinutes(1))
    /// </code>
    /// </example>
    public ApiSpecification Deadline(TimeSpan deadline)
    {
        settings.EnsureConfigurable("Deadline(...)");
        settings.Deadline = deadline;
        return this;
    }

    /// <summary>
    /// Sets how long the base address function may take to start the app, 3 minutes by default. A start that takes longer fails the test and its <c>start app</c> span, instead of hanging the test run.
    /// Raise it for an app that starts slowly, such as an Aspire app that pulls container images.
    /// </summary>
    /// <example>
    /// <code>
    /// spec.StartTimeout(TimeSpan.FromMinutes(5))
    /// </code>
    /// </example>
    public ApiSpecification StartTimeout(TimeSpan startTimeout)
    {
        settings.EnsureConfigurable("StartTimeout(...)");
        settings.StartTimeout = startTimeout;
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
    public ApiSpecification Clock(TimeProvider clock)
    {
        settings.EnsureConfigurable("Clock(...)");
        settings.Clock = clock;
        return this;
    }

    /// <summary>
    /// Sets where <c>ExpectTrace</c>, <c>ExpectCorrelatedTrace</c> and <c>ExpectMetric</c> get spans and measurements from.
    /// The default is <see cref="TelemetrySources.Off"/>, because an out-of-process app's telemetry doesn't reach the test process by itself.
    /// Pass an <see cref="OtlpReceiver"/> that the app exports to, or your own <see cref="ITelemetrySource"/>.
    /// The specification attaches the source on its first step, before the address function runs. The source belongs to the caller, who disposes it, so one receiver can serve several specifications.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var api = ApiSpecification.For(async () => app.GetEndpoint("api")).Telemetry(otlp);
    /// </code>
    /// </example>
    public ApiSpecification Telemetry(ITelemetrySource source)
    {
        settings.EnsureConfigurable("Telemetry(...)");
        settings.UseTelemetry(source);
        return this;
    }

    /// <summary>
    /// Sets the JSON options that <c>Body(...)</c> uses to serialize the expected value. The default is the web defaults, <see cref="JsonSerializerDefaults.Web"/>.
    /// Call it before the first step.
    /// </summary>
    /// <example>
    /// <code>
    /// spec.Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
    /// </code>
    /// </example>
    public ApiSpecification Json(JsonSerializerOptions options)
    {
        settings.EnsureConfigurable("Json(...)");
        settings.Json = options;
        return this;
    }

    /// <summary>Sets the Given steps. The host address function runs on the first step.</summary>
    /// <example>
    /// <code>
    /// await api.Given(PlaceOrder(cartId)).When(GetOrder(cartId)).Then([ExpectResponse(OK)], ct);
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
        Given(Scenario(scenario, more));

    private StartedHost Start()
    {
        settings.Start();
        var address = baseAddress ?? throw new AssertionException(
            "A host is required, but this specification has no base address. Pass an address function to For(...) or to the base constructor, or call Host(async () => address) in the configuration.");

        Uri uri;
        try
        {
            uri = address().WaitAsync(settings.StartTimeout).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            throw new AssertionException(
                $"Starting the host did not finish within {settings.StartTimeout}. Give the app more time with StartTimeout(...) if it starts slowly, or check what it waits for at startup.");
        }
        catch (Exception exception)
        {
            throw new AssertionException(
                $"Starting the host failed: the base address function threw {exception.GetType().Name}: {exception.Message}",
                exception);
        }

        var handler = handlerFactory?.Invoke() ?? new SocketsHttpHandler();
        if (handler is DelegatingHandler { InnerHandler: null } delegating)
            delegating.InnerHandler = new SocketsHttpHandler();
        var json = hostServices is null ? ApiJsonSettings.Web : ApiJsonSettings.FromServices(hostServices);
        return new StartedHost(handler, uri, json.Response);
    }

    /// <summary>Disposes the handler. It doesn't stop the app, nor a telemetry source or <see cref="OtlpReceiver"/> passed to <c>Telemetry(...)</c>, which the caller owns.</summary>
    /// <example>
    /// <code>
    /// using var api = ApiSpecification.For(async () =&gt; app.GetEndpoint("api"));
    /// </code>
    /// </example>
    public void Dispose()
    {
        if (host.IsValueCreated)
            host.Value.Handler.Dispose();
        settings.DisposeOwnedTelemetry().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the handler. It doesn't stop the app, nor a telemetry source or <see cref="OtlpReceiver"/> passed to <c>Telemetry(...)</c>, which the caller owns.</summary>
    /// <example>
    /// <code>
    /// await using var api = ApiSpecification.For(async () =&gt; app.GetEndpoint("api"));
    /// </code>
    /// </example>
    public async ValueTask DisposeAsync()
    {
        if (host.IsValueCreated)
            host.Value.Handler.Dispose();
        await settings.DisposeOwnedTelemetry();
        GC.SuppressFinalize(this);
    }
}

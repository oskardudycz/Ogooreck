using System.Net;
using System.Text.Json;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Testing;
using Ogooreck.Tests.API.Hosts.Fixtures;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests.API.Hosts;

public class OutOfProcessSpecificationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TestStep Echo() => Request(client => client.GetAsync("/echo", Ct));

    private static async Task<JsonElement> EchoOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();

    [Fact]
    public async Task AddressFunction_RunsOnceOnTheFirstStep_AndRequestsGoToThatAddress()
    {
        await using var server = EchoServer.Start();
        var calls = 0;
        await using var api = For(() =>
        {
            calls++;
            return Task.FromResult(server.Address);
        });
        var ports = new List<int>();

        await api.Given(Echo()).When(Echo()).Then([
            ExpectResponse(OK, Satisfying(async response => ports.Add((await EchoOf(response)).GetProperty("port").GetInt32())))
        ], Ct);

        Assert.Equal(1, calls);
        Assert.Equal([server.Address.Port], ports);
    }

    [Fact]
    public async Task EveryRequestToAnOutOfProcessHost_CarriesAUniqueTraceparentPerStep()
    {
        await using var server = EchoServer.Start();
        await using var api = For(() => Task.FromResult(server.Address));
        var traceparents = new List<string>();
        var collect = Satisfying(async response =>
            traceparents.Add((await EchoOf(response)).GetProperty("traceparent").GetString()!));

        await api.Given(Echo()).When(Echo()).Then([ExpectResponse(OK, collect)], Ct);
        await api.Given().When(Echo()).Then([ExpectResponse(OK, collect)], Ct);

        Assert.Equal(2, traceparents.Count);
        Assert.All(traceparents, traceparent => Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-01$", traceparent));
        Assert.NotEqual(traceparents[0].Split('-')[1], traceparents[1].Split('-')[1]);
    }

    [Fact]
    public async Task HandlerFactory_IsUsedForRequests_AndTheTraceparentIsStillSet()
    {
        await using var server = EchoServer.Start();
        var handler = new CountingHandler { InnerHandler = new SocketsHttpHandler() };
        await using var api = For(() => Task.FromResult(server.Address), () => handler);
        string? traceparent = null;

        await api.Given().When(Echo()).Then([
            ExpectResponse(OK, Satisfying(async response =>
                traceparent = (await EchoOf(response)).GetProperty("traceparent").GetString()))
        ], Ct);

        Assert.Equal(1, handler.Requests);
        Assert.Matches("^00-[0-9a-f]{32}-[0-9a-f]{16}-01$", traceparent);
    }

    [Fact]
    public async Task HandlerWithoutAnInnerHandler_GetsOneThatSendsTheRequest()
    {
        await using var server = EchoServer.Start();
        var handler = new CountingHandler();
        await using var api = For(() => Task.FromResult(server.Address), () => handler);

        await api.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);

        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task SubclassPassingTheAddressToTheBaseConstructor_BehavesLikeFor()
    {
        await using var server = EchoServer.Start();
        await using var api = new AddressApi(server.Address);

        await api.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);
    }

    [Fact]
    public async Task SubclassPassingTheAddressAndAHandlerToTheBaseConstructor_UsesTheHandler()
    {
        await using var server = EchoServer.Start();
        var handler = new CountingHandler { InnerHandler = new SocketsHttpHandler() };
        await using var api = new AddressAndHandlerApi(server.Address, handler);

        await api.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);

        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task SubclassCallingHost_BehavesLikeFor()
    {
        await using var server = EchoServer.Start();
        await using var api = new HostCallingApi(server.Address);

        await api.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);
    }

    [Fact]
    public async Task HostCalledWithAHandler_UsesTheHandler()
    {
        await using var server = EchoServer.Start();
        var handler = new CountingHandler { InnerHandler = new SocketsHttpHandler() };
        await using var api = new HostWithHandlerApi(server.Address, handler);

        await api.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);

        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task HostCalledTwice_ReplacesTheFirstAddress()
    {
        await using var first = EchoServer.Start();
        await using var second = EchoServer.Start();
        var calls = new List<string>();
        await using var api = new HostCallingApi(first.Address, configure: spec =>
            spec.Host(() =>
            {
                calls.Add("second");
                return Task.FromResult(second.Address);
            }));
        var ports = new List<int>();

        await api.Given().When(Echo()).Then([
            ExpectResponse(OK, Satisfying(async response => ports.Add((await EchoOf(response)).GetProperty("port").GetInt32())))
        ], Ct);

        Assert.Equal(["second"], calls);
        Assert.Equal([second.Address.Port], ports);
    }

    [Fact]
    public async Task SubclassWithoutAHost_FailsOnTheFirstStepSayingAHostIsRequired()
    {
        await using var api = new HostlessApi();

        var failure = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(Echo()).When(Echo()).Then([ExpectResponse(OK)], Ct));

        Assert.Equal(
            "A host is required, but this specification has no base address. Pass an address function to For(...) or to the base constructor, or call Host(async () => address) in the configuration.",
            failure.Message);
    }

    [Fact]
    public async Task AddressFunctionThatThrows_FailsSayingSoAndKeepsTheError()
    {
        await using var api = For(() => throw new InvalidOperationException("the app did not start"));

        var failure = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Echo()).Then([ExpectResponse(OK)], Ct));

        Assert.Equal(
            "Starting the host failed: the base address function threw InvalidOperationException: the app did not start",
            failure.Message);
        Assert.IsType<InvalidOperationException>(failure.InnerException);
    }

    [Fact]
    public async Task ConfigurationAfterTheFirstStep_Throws()
    {
        await using var server = EchoServer.Start();
        await using var api = For(() => Task.FromResult(server.Address));
        await api.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);

        var calls = new (string Call, Action Configure)[]
        {
            ("Host(...)", () => api.Host(() => Task.FromResult(server.Address))),
            ("Host(...)", () => api.Host(() => Task.FromResult(server.Address), () => new SocketsHttpHandler())),
            ("Recording(...)", () => api.Recording(new Recorded<string>(), message => new Observed(message, null))),
            ("Recording(...)",
                () => api.Recording(new Recorded<string>(), message => new Observed(message, null), _ => Task.CompletedTask)),
            ("Recording(...)",
                () => api.Recording(new Recorded<string>(), message => new Observed(message, null),
                    (step, token) => step(token))),
            ("Appending(...)", () => api.Appending(new Recorded<string>(), message => new Observed(message, null))),
            ("Appending(...)",
                () => api.Appending(new Recorded<string>(), message => new Observed(message, null), _ => Task.CompletedTask)),
            ("Appending(...)",
                () => api.Appending(new Recorded<string>(), message => new Observed(message, null),
                    (step, token) => step(token))),
            ("CatchUp(...)", () => api.CatchUp(_ => Task.CompletedTask)),
            ("CatchUp(...)", () => api.CatchUp((step, token) => step(token))),
            ("Publishing(...)", () => api.Publishing((_, _) => Task.CompletedTask)),
            ("Deadline(...)", () => api.Deadline(TimeSpan.FromSeconds(1))),
            ("Telemetry(...)", () => api.Telemetry(TelemetrySources.Off)),
            ("Json(...)", () => api.Json(new JsonSerializerOptions()))
        };

        foreach (var (call, configure) in calls)
        {
            var failure = Assert.Throws<InvalidOperationException>(configure);
            Assert.StartsWith($"{call} was called after the specification ran its first step", failure.Message);
        }
    }

    public static TheoryData<string> AppendingOverloads => ["without catch-up", "with catch-up", "around each step"];

    [Theory]
    [MemberData(nameof(AppendingOverloads))]
    public async Task Appending_RegistersTheRecorderOfAppendedEvents(string overload)
    {
        await using var server = EchoServer.Start();
        var appended = new Recorded<string>();
        await using var api = For(() => Task.FromResult(server.Address));
        _ = overload switch
        {
            "without catch-up" => api.Appending(appended, message => new Observed(message, null)),
            "with catch-up" => api.Appending(appended, message => new Observed(message, null), _ => Task.CompletedTask),
            _ => api.Appending(appended, message => new Observed(message, null), (step, token) => step(token))
        };

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Echo()).Then([ExpectPublished("anything")], Ct));

        Assert.EndsWith("but the specification has none. Add a plugin with Use(...), or register a recorder with Recording(...).", error.Message);
        await api.Given().When(Echo()).Then([ExpectAppendedIn(appended)], Ct);
    }

    [Fact]
    public async Task Defaults_UseWebJsonAndTelemetryOff()
    {
        await using var server = EchoServer.Start();
        await using var api = For(() => Task.FromResult(server.Address));

        await api.Given().When(Echo()).Then([ExpectResponse(OK, Body(new { Port = server.Address.Port }))], Ct);
        var failure = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Echo()).Then([ExpectTrace(Span("GET /echo"))], Ct));

        Assert.Contains("needs telemetry, but telemetry is off for this specification", failure.Message);
    }

    [Fact]
    public async Task ConfiguredDeadlineAndJson_AreUsedByTheNonGenericSpecification()
    {
        await using var server = EchoServer.Start();
        await using var api = For(() => Task.FromResult(server.Address))
            .Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
            .Deadline(TimeSpan.FromSeconds(2))
            .Telemetry(TelemetrySources.InProcess())
            .CatchUp(_ => Task.CompletedTask)
            .CatchUp((step, token) => step(token))
            .Recording(new Recorded<string>(), message => new Observed(message, null))
            .Recording(new Recorded<string>(), message => new Observed(message, null), _ => Task.CompletedTask)
            .Recording(new Recorded<string>(), message => new Observed(message, null), (step, token) => step(token))
            .Publishing((_, _) => Task.CompletedTask);

        await api.Given(Scenario([Echo()], Echo())).When(Echo()).Then([ExpectResponse(OK, Body(new { Port = server.Address.Port }))], Ct);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_StopsTheHandler_AndLeavesAPassedInOtlpReceiverRunning(bool asynchronous)
    {
        await using var server = EchoServer.Start();
        await using var receiver = OtlpReceiver.Start();
        var handler = new CountingHandler { InnerHandler = new SocketsHttpHandler() };
        var api = For(() => Task.FromResult(server.Address), () => handler).Telemetry(receiver);
        await api.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);

        if (asynchronous)
            await api.DisposeAsync();
        else
            api.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            new HttpClient(handler, disposeHandler: false).GetAsync(server.Address, Ct));
        var response = await ExportToTheReceiver(receiver);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_LeavesAPassedInInProcessSourceListening(bool asynchronous)
    {
        await using var server = EchoServer.Start();
        using var activities = new System.Diagnostics.ActivitySource($"test-{Guid.NewGuid()}");
        await using var source = TelemetrySources.InProcess();
        var api = For(() => Task.FromResult(server.Address)).Telemetry(source);
        await api.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);

        if (asynchronous)
            await api.DisposeAsync();
        else
            api.Dispose();

        using var afterwards = activities.StartActivity("after dispose");
        Assert.NotNull(afterwards);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_BeforeTheFirstStep_LeavesAPassedInOtlpReceiverRunningWithoutStartingTheHost(bool asynchronous)
    {
        await using var receiver = OtlpReceiver.Start();
        var starts = 0;
        var api = For(() =>
        {
            starts++;
            return Task.FromResult(new Uri("http://localhost"));
        }).Telemetry(receiver);

        if (asynchronous)
            await api.DisposeAsync();
        else
            api.Dispose();

        Assert.Equal(0, starts);
        var response = await ExportToTheReceiver(receiver);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task OneOtlpReceiver_CanBeSharedBySpecifications_ThatAreDisposedOneAfterAnother()
    {
        await using var server = EchoServer.Start();
        await using var receiver = OtlpReceiver.Start();
        var first = For(() => Task.FromResult(server.Address)).Telemetry(receiver);
        await first.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);
        await first.DisposeAsync();

        await using var second = For(() => Task.FromResult(server.Address)).Telemetry(receiver);

        await second.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);
    }

    [Fact]
    public async Task Collect_OfACustomSourceThatRunsPastTheDeadline_FailsWithoutMentioningWaitForMetricsExport()
    {
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .Telemetry(new NeverCollectingSource())
            .Clock(new PassingClock())
            .Deadline(TimeSpan.FromSeconds(1));

        var failure = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct));

        Assert.Equal(
            "Before the When step: the telemetry source NeverCollectingSource did not finish collecting within the deadline of 00:00:01.",
            failure.Message);
    }

    private sealed class NeverCollectingSource: ITelemetrySource
    {
        public void Attach(Recorded<RecordedSpan> spans, Recorded<RecordedMeasurement> measurements)
        {
        }

        public Task Collect(CancellationToken ct) => Task.Delay(Timeout.Infinite, ct);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static Task<HttpResponseMessage> ExportToTheReceiver(OtlpReceiver receiver)
    {
        var content = new ByteArrayContent([]);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-protobuf");
        return new HttpClient().PostAsync(new Uri(receiver.Endpoint, "v1/traces"), content, Ct);
    }

    private sealed class CountingHandler: DelegatingHandler
    {
        private int requests;

        public int Requests => requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requests);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class AddressApi(Uri address): ApiSpecification(() => Task.FromResult(address), spec => spec);

    private sealed class AddressAndHandlerApi(Uri address, HttpMessageHandler handler)
        : ApiSpecification(() => Task.FromResult(address), () => handler, spec => spec);

    private sealed class HostCallingApi(Uri address, Func<ApiSpecification, ApiSpecification>? configure = null)
        : ApiSpecification(spec => (configure ?? (each => each))(spec.Host(() => Task.FromResult(address))));

    private sealed class HostWithHandlerApi(Uri address, HttpMessageHandler handler)
        : ApiSpecification(spec => spec.Host(() => Task.FromResult(address), () => handler));

    private sealed class HostlessApi(): ApiSpecification(spec => spec);
}

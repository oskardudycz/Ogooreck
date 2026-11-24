using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Sample.Wolverine;
using Ogooreck.Sample.Wolverine.Events;
using Ogooreck.Wolverine;
using Wolverine;
using Xunit;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.ShopRequests;

namespace Ogooreck.Wolverine.Tests;

public class PlugTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly Guid cartId = Guid.NewGuid();

    private sealed class TimeAndRecordingPlug: IPlug
    {
        private readonly List<string> catchUps = [];
        private readonly List<PublishRequest> publishRequests = [];

        public FakeTimeProvider Time { get; } = new();

        public Recorded<Observed> Recorded { get; } = new();

        public int CatchUpRuns => catchUps.Count;

        public IReadOnlyList<PublishRequest> PublishRequests => publishRequests;

        public void Plug(PlugRegistration registration) =>
            registration
                .Services(services => services.AddSingleton<TimeProvider>(Time))
                .Recording(Recorded, observed => observed)
                .CatchUp(_ =>
                {
                    catchUps.Add("ran");
                    return Task.CompletedTask;
                })
                .Publishing((request, _) =>
                {
                    publishRequests.Add(request);
                    return Task.CompletedTask;
                });
    }

    private sealed class AppServicesProbe: IPlug
    {
        private PlugRegistration? registration;

        public IServiceProvider? SeenByTheFirstCatchUp { get; private set; }

        public IServiceProvider AppServices => registration!.AppServices;

        public void Plug(PlugRegistration plugRegistration)
        {
            registration = plugRegistration;
            plugRegistration.CatchUp(_ =>
            {
                SeenByTheFirstCatchUp ??= plugRegistration.AppServices;
                return Task.CompletedTask;
            });
        }
    }

    private sealed class UndecoratedServicePlug: IPlug
    {
        public void Plug(PlugRegistration registration) =>
            registration.Services(services => services.Decorate<IDisposable>(inner => inner));
    }

    private sealed record RawMessage(Dictionary<string, string> Headers, string Body);

    private sealed class ReadingPlug(Func<RawMessage, Observed>? read = null): IPlug
    {
        public Recorded<RawMessage> Raw { get; } = new();

        public void Plug(PlugRegistration registration)
        {
            var reader = new MessageReader().MessageTypes(typeof(OrderPlaced));
            registration.Recording(Raw,
                read ?? (raw => reader.Read(name => raw.Headers.GetValueOrDefault(name), Encoding.UTF8.GetBytes(raw.Body))),
                _ => Task.CompletedTask);
        }
    }

    [Fact]
    public async Task wolverinePlugAndAUserClockPlug_compose_cartExpiresAfterTheClockAdvances()
    {
        var clock = new FakeClock();
        await using var spec = For<Program>().Use(Messaging.Wolverine).Use(clock);

        await spec.Given(AddProduct(cartId, "socks"))
            .When(Action(_ =>
            {
                clock.Advance(TimeSpan.FromDays(1));
                return Task.CompletedTask;
            }))
            .Then([ExpectPublishedIn(Messaging.Wolverine.Published, new CartExpired(cartId))], Ct);
    }

    [Fact]
    public async Task userPlug_registrationCalls_behaveLikeDirectConfigurationCalls()
    {
        var plug = new TimeAndRecordingPlug();
        var orderId = Guid.NewGuid();
        var probe = new AppServicesProbe();
        await using var spec = For<Program>().Use(plug).Use(probe);

        await spec.Given(Publish(new PaymentConfirmed(orderId)))
            .When(Action(ctx =>
            {
                plug.Recorded.Add(new Observed(new OrderPlaced(cartId), ctx.Traceparent));
                return Task.CompletedTask;
            }))
            .Then([ExpectPublishedIn(plug.Recorded, new OrderPlaced(cartId))], Ct);

        Assert.Same(plug.Time, probe.AppServices.GetRequiredService<TimeProvider>());
        Assert.Equal(2, plug.CatchUpRuns);
        Assert.Equal([new PaymentConfirmed(orderId)], plug.PublishRequests.Single().Messages);

        var appsOwn = new AppServicesProbe();
        await using var withoutThePlug = For<Program>().Use(appsOwn);
        await withoutThePlug.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct);
        Assert.Same(TimeProvider.System, appsOwn.AppServices.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task appServices_afterTheHostStarted_isTheHostsProvider_andBeforeItThrows()
    {
        var probe = new AppServicesProbe();
        await using var spec = For<Program>().Use(probe);

        var before = Assert.Throws<InvalidOperationException>(() => probe.AppServices);
        await spec.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct);

        Assert.StartsWith("The host has not started yet", before.Message);
        Assert.Same(probe.SeenByTheFirstCatchUp, probe.AppServices);
        Assert.Same(TimeProvider.System, probe.AppServices.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task decorate_withoutARegistration_failsTheHostStartNamingTheType()
    {
        await using var spec = For<Program>().Use(new UndecoratedServicePlug());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            spec.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct));

        Assert.Equal(
            "Decorate<IDisposable> found no registration of System.IDisposable: the app must register it for a plug to decorate it.",
            error.Message);
    }

    [Fact]
    public async Task useOfAPlugMadeFromAnother_replacesIt_recordingOnce()
    {
        var plain = new WolverinePlug();
        await using var spec = For<Program>().Use(plain).Use(plain.MessageTypes(typeof(OrderPlaced)));

        await spec.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectPublishedIn(plain.Published, new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })], Ct);
    }

    [Fact]
    public async Task wolverinePlugWithoutOptions_recordsEveryMessageTheAppPublishes()
    {
        var plain = new WolverinePlug();
        await using var spec = For<Program>().Use(plain);

        await spec.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectPublishedIn(plain.Published, new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })], Ct);
    }

    [Fact]
    public async Task envelopeWithoutAConversationId_hasNoCausationId_soExpectCausationNamesIt()
    {
        await using var spec = For<Program>().Use(Messaging.Wolverine);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(Action(ctx =>
                {
                    Messaging.Wolverine.Published.Add(new Envelope(new OrderPlaced(cartId)) { ParentId = ctx.Traceparent, CorrelationId = "c1" });
                    Messaging.Wolverine.Published.Add(new Envelope(new ShipOrder(cartId)) { ParentId = ctx.Traceparent, CorrelationId = "c1" });
                    return Task.CompletedTask;
                }))
                .Then([ExpectCausation(PublishedIn(Messaging.Wolverine.Published, new OrderPlaced(cartId),
                    SentIn(Messaging.Wolverine.Published, new ShipOrder(cartId))))], Ct));

        Assert.Contains("read returned no causation id for ShipOrder", error.Message);
    }

    [Fact]
    public async Task messageReader_customRead_replacesTheReader()
    {
        var reading = new ReadingPlug();
        var custom = new ReadingPlug(raw => new Observed(new OrderPlaced(cartId), raw.Headers["traceparent"]));
        await using var spec = For<Program>().Use(reading).Use(custom);

        await spec.Given()
            .When(Action(ctx =>
            {
                var headers = new Dictionary<string, string> { ["messageType"] = "OrderPlaced", ["traceparent"] = ctx.Traceparent };
                reading.Raw.Add(new RawMessage(headers, $$"""{"cartId":"{{cartId}}"}"""));
                custom.Raw.Add(new RawMessage(headers, "not json at all"));
                return Task.CompletedTask;
            }))
            .Then([ExpectPublishedIn(reading.Raw, new OrderPlaced(cartId)), ExpectPublishedIn(custom.Raw, new OrderPlaced(cartId))], Ct);
    }
}

using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Running;

public class ClockTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExpectationThatNeverHolds_FailsAtTheDeadlineOfTheClock()
    {
        await using var spec = api.With(setup => setup.Clock(new PassingClock()).Deadline(OneMinute));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("never"))], ct));

        Assert.Contains("No span 'never', when the deadline of 00:01:00 passed.", error.Message);
    }

    [Fact]
    public async Task CatchUpThatNeverEnds_FailsAtTheDeadlineOfTheClock()
    {
        await using var spec = api.With(setup => setup
            .Clock(new PassingClock())
            .Deadline(OneMinute)
            .CatchUp(token => Task.Delay(Timeout.Infinite, token)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([], ct));

        Assert.StartsWith($"The application kept producing messages for longer than 00:01:00 after the When request GET /carts/{cartId}.",
            error.Message);
    }

    [Fact]
    public async Task EventuallyThatNeverHolds_FailsAtTheDeadlineOfTheClock()
    {
        var clock = new PassingClock();
        await using var spec = api.With(setup => setup.Clock(clock).Deadline(OneMinute));
        var getCartTakingOneMinute = Request(client =>
        {
            clock.Advance(OneMinute);
            return client.GetAsync($"/carts/{cartId}", ct);
        });

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Eventually(getCartTakingOneMinute)).Then([ExpectResponse(OK)], ct));

        Assert.StartsWith($"When eventually request GET /carts/{cartId}: the response expectations still failed when the deadline of 00:01:00 passed, after 1 attempts.",
            error.Message);
    }

    [Fact]
    public async Task TelemetrySourceThatNeverFinishesCollecting_FailsAtTheDeadlineOfTheClock()
    {
        await using var telemetry = new NeverCollecting();
        await using var spec = api.With(setup => setup.Clock(new PassingClock()).Deadline(OneMinute).Telemetry(telemetry));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([ExpectMetric("orders.placed", new Dictionary<string, string>(), 1)], ct));

        Assert.Contains("the telemetry source NeverCollecting did not finish collecting within the deadline of 00:01:00.",
            error.Message);
    }

    [Fact]
    public async Task Plugin_ReadsTheClockOfTheSpecification()
    {
        var clock = new PassingClock();
        var plugin = new ClockReadingPlugin();
        await using var spec = api.With(setup => setup.Clock(clock).Use(plugin));

        await spec.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Same(clock, plugin.Clock);
    }

    [Fact]
    public async Task PluginOfTheSharedApp_ReadsTheClockAndDeadlineOfTheVariantInItsCatchUp()
    {
        var clock = new PassingClock();
        var plugin = new CatchUpReadingPlugin();
        await using var app = For<Program>().Use(plugin);
        await using var spec = app.With(setup => setup.Clock(clock).Deadline(TimeSpan.FromSeconds(5)));

        await spec.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Same(clock, plugin.Clock);
        Assert.Equal(TimeSpan.FromSeconds(5), plugin.Deadline);
    }

    [Fact]
    public async Task Plugin_ReadWhileAnotherSpecificationRuns_KeepsTheDeadlineOfItsOwnSpecification()
    {
        var plugin = new RegistrationKeepingPlugin();
        await using var own = api.With(setup => setup.Deadline(TimeSpan.FromSeconds(5)).Use(plugin));
        await using var other = api.With(setup => setup.Deadline(TimeSpan.FromSeconds(7)));
        var seen = TimeSpan.Zero;

        await other.Given().When(Action(_ =>
        {
            seen = plugin.Registration!.Deadline;
            return Task.CompletedTask;
        })).Then([], ct);

        Assert.Equal(TimeSpan.FromSeconds(5), seen);
    }

    [Fact]
    public async Task Plugin_ReadOutsideOfARun_HasTheDeadlineOfItsSpecification()
    {
        var plugin = new RegistrationKeepingPlugin();
        await using var spec = api.With(setup => setup.Deadline(TimeSpan.FromSeconds(5)).Use(plugin));

        Assert.Equal(TimeSpan.FromSeconds(5), plugin.Registration!.Deadline);
    }

    private sealed class RegistrationKeepingPlugin: ISpecificationPlugin
    {
        public SpecificationPluginRegistration? Registration;

        public void Register(SpecificationPluginRegistration registration) => Registration = registration;
    }

    private sealed class CatchUpReadingPlugin: ISpecificationPlugin
    {
        public TimeProvider? Clock;
        public TimeSpan? Deadline;

        public void Register(SpecificationPluginRegistration registration) =>
            registration.CatchUp(_ =>
            {
                Clock = registration.Clock;
                Deadline = registration.Deadline;
                return Task.CompletedTask;
            });
    }

    private sealed class ClockReadingPlugin: ISpecificationPlugin
    {
        public TimeProvider? Clock;

        public void Register(SpecificationPluginRegistration registration) =>
            registration.Starting(_ =>
            {
                Clock = registration.Clock;
                return Task.CompletedTask;
            });
    }

    private sealed class NeverCollecting: ITelemetrySource
    {
        public void Attach(Recorded<RecordedSpan> spans, Recorded<RecordedMeasurement> measurements) { }

        public Task Collect(CancellationToken ct) => Task.Delay(Timeout.Infinite, ct);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

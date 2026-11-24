using System.Diagnostics;
using System.Text.Json;
using Ogooreck.Api.Tests.Telemetry;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests;

public class ConfigurationTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public async Task ExpectationThatNeverHolds_FailsAtTheConfiguredDeadline_NotTheDefault()
    {
        await using var spec = api.With(setup => setup.Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(2)));
        await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("never"))], TestContext.Current.CancellationToken));
    }

    public static TheoryData<string, Expectation> TelemetryExpectations => new()
    {
        { "ExpectTrace(Span(never))", ExpectTrace(Span("never")) },
        { "ExpectCorrelatedTrace()", ExpectCorrelatedTrace() },
        { "ExpectMetric(orders.placed, {}, 1)", ExpectMetric("orders.placed", new Dictionary<string, string>(), 1) }
    };

    [Theory]
    [MemberData(nameof(TelemetryExpectations))]
    public async Task TelemetryExpectation_WithTelemetryOff_FailsAtOnceSayingTelemetryIsOff(string call,
        Expectation expectation)
    {
        await using var spec = api.With(setup => setup.Telemetry(TelemetrySources.Off));
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([expectation], TestContext.Current.CancellationToken));

        Assert.Equal(
            $"When request GET /carts/{cartId}: {call} needs telemetry, but telemetry is off for this specification; configure a source with Telemetry(...), for example TelemetrySources.InProcess().",
            error.Message);
    }

    public static TheoryData<string> ConfigurationCalls =>
        ["Deadline(...)", "Recording(...)", "CatchUp(...)", "Publishing(...)", "Telemetry(...)", "Json(...)", "Clock(...)"];

    [Theory]
    [MemberData(nameof(ConfigurationCalls))]
    public async Task ConfigurationCallAfterTheFirstStep_Throws_SayingConfigurationMustHappenBeforeTheFirstStep(string call)
    {
        await using var spec = api.With(setup => setup);
        await spec.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], TestContext.Current.CancellationToken);

        Action configure = call switch
        {
            "Deadline(...)" => () => spec.Deadline(TimeSpan.FromSeconds(1)),
            "Clock(...)" => () => spec.Clock(TimeProvider.System),
            "Recording(...)" => () => spec.Recording(new Recorded<string>(), message => new Observed(message, null)),
            "CatchUp(...)" => () => spec.CatchUp(_ => Task.CompletedTask),
            "Publishing(...)" => () => spec.Publishing((_, _) => Task.CompletedTask),
            "Telemetry(...)" => () => spec.Telemetry(TelemetrySources.Off),
            _ => () => spec.Json(new JsonSerializerOptions())
        };

        var error = Assert.Throws<InvalidOperationException>(configure);
        Assert.Equal(
            $"{call} was called after the specification ran its first step, but configuration must happen before the first step. Use With(...) for other test setup.",
            error.Message);
    }

    [Fact]
    public async Task CustomTelemetrySource_AddingASpanWithTheWhenTraceId_SatisfiesExpectTrace()
    {
        var source = new RecordingTelemetrySource();
        await using var spec = api.With(setup => setup.Telemetry(source));

        await spec.Given()
            .When(Action(ctx =>
            {
                var when = ActivityContext.Parse(ctx.Traceparent, null);
                source.Spans!.Add(new RecordedSpan(when.TraceId.ToHexString(), ActivitySpanId.CreateRandom().ToHexString(),
                    when.SpanId.ToHexString(), "custom", new Dictionary<string, string>()));
                return Task.CompletedTask;
            }))
            .Then([ExpectTrace(Span("custom"))], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TelemetryCollect_IsCalledOnceAtTheBaseline_AndBeforeEachExpectMetricCheck()
    {
        var source = new RecordingTelemetrySource();
        await using var spec = api.With(setup => setup.Telemetry(source));
        var none = new Dictionary<string, string>();

        await spec.Given(Attempt(GetCart(cartId)))
            .When(GetCart(cartId))
            .Then([ExpectMetric("orders.placed", none, 0), ExpectMetric("carts.expired", none, 0)],
                TestContext.Current.CancellationToken);

        Assert.Equal(3, source.Collects);
    }

    [Fact]
    public async Task ConfigurationCallsReturnTheSameInstance_AndChainsInTheConfigureConstructorApplyEveryCall()
    {
        var spec = For<Program>();
        await using (spec)
        {
            Assert.Same(spec, spec.Deadline(TimeSpan.FromSeconds(1)).Json(new JsonSerializerOptions()));
        }

        await using var configured = new SnakeCaseApiWithAShortDeadline();
        await configured.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
            .When(GetOrderExport(cartId))
            .Then([ExpectResponse(OK, Body(new { OrderStatus = "Placed" }))], TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<AssertionException>(() =>
            configured.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("never"))], TestContext.Current.CancellationToken));
    }

    private sealed class SnakeCaseApiWithAShortDeadline()
        : ApiSpecification<Program>(spec => spec
            .Clock(new PassingClock())
            .Deadline(TimeSpan.FromSeconds(2))
            .Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
}

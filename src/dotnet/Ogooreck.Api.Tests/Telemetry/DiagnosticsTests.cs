using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.Telemetry;

public class DiagnosticsTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>, IDisposable
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly OgooreckSpans spans = new();

    [Fact]
    public async Task Run_IsTracedAsASpanOfOgooreck_WithItsStartsStepsCatchUpsAndExpectations()
    {
        await api.Given(AddProduct(cartId, "socks"))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken);

        var run = spans.Stopped.Single(span => span.DisplayName == $"Ogooreck When request GET /carts/{cartId}");
        Assert.Equal(
        [
            "start before Given step 1",
            $"Given step 1 request POST /carts/{cartId}/products",
            $"catch-up after Given step 1 request POST /carts/{cartId}/products",
            "start before the baseline",
            "start before When",
            $"When request GET /carts/{cartId}",
            $"catch-up after When request GET /carts/{cartId}",
            "expectation 1"
        ], spans.Stopped.Where(span => span.ParentSpanId == run.SpanId).OrderBy(span => span.StartTimeUtc).Select(span => span.DisplayName));
        Assert.Equal(ActivityStatusCode.Unset, run.Status);
    }

    [Fact]
    public async Task FailedRun_IsMarkedAsAnErrorWithTheFailure()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        var run = spans.Stopped.Single(span => span.DisplayName == $"Ogooreck When request GET /carts/{cartId}");
        Assert.Equal(ActivityStatusCode.Error, run.Status);
        Assert.Equal(error.Message, run.StatusDescription);
    }

    [Fact]
    public async Task RunFailingInTheCatchUpAfterTheWhenStep_IsNamedAfterTheWhenStep()
    {
        await using var spec = api.With(setup => setup.CatchUp(_ => throw new InvalidOperationException("stuck")));

        await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([], TestContext.Current.CancellationToken));

        var run = spans.Stopped.Single(span => span.DisplayName == $"Ogooreck When request GET /carts/{cartId}");
        Assert.Equal(ActivityStatusCode.Error, run.Status);
    }

    [Fact]
    public async Task SpansOfAPlug_NestUnderItsStartAndItsCatchUp()
    {
        await using var app = For<Program>().Use(new TracingPlug());

        await app.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], TestContext.Current.CancellationToken);

        var run = spans.Stopped.Single(span => span.DisplayName == $"Ogooreck When request GET /carts/{cartId}");
        var parents = spans.Stopped.Where(span => span.TraceId == run.TraceId)
            .ToDictionary(span => span.SpanId, span => span.DisplayName);
        Assert.Equal("start plug TracingPlug", parents[spans.Stopped.Single(span => span.DisplayName == "tracing plug started" && span.TraceId == run.TraceId).ParentSpanId]);
        Assert.Equal($"catch-up after When request GET /carts/{cartId}",
            parents[spans.Stopped.Single(span => span.DisplayName == "tracing plug caught up" && span.TraceId == run.TraceId).ParentSpanId]);
    }

    [Fact]
    public void Durations_ArePublishedAsHistogramsOfOgooreck()
    {
        var instruments = new ConcurrentBag<string>();
        using var meters = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == "Ogooreck")
                    instruments.Add(instrument.Name);
            }
        };

        meters.Start();

        Assert.Equal(
            ["ogooreck.catch_up.duration", "ogooreck.expectation.duration", "ogooreck.run.duration", "ogooreck.start.duration", "ogooreck.step.duration"],
            instruments.Order());
    }

    public void Dispose() => spans.Dispose();

    private sealed class TracingPlug: IPlug
    {
        private static readonly ActivitySource Source = new("Ogooreck");

        public void Plug(PlugRegistration registration) =>
            registration
                .Starting(_ => Traced("tracing plug started"))
                .CatchUp(_ => Traced("tracing plug caught up"));

        private static Task Traced(string name)
        {
            using (Source.StartActivity(name))
                return Task.CompletedTask;
        }
    }

    private sealed class OgooreckSpans: IDisposable
    {
        private readonly ActivityListener listener;

        public OgooreckSpans()
        {
            listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Ogooreck",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Stopped.Enqueue
            };
            ActivitySource.AddActivityListener(listener);
        }

        public ConcurrentQueue<Activity> Stopped { get; } = new();

        public void Dispose() => listener.Dispose();
    }
}

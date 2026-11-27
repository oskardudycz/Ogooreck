using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using Ogooreck.Api.Tests.Fixtures;
using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Telemetry;

public class InProcessTelemetryTests: IDisposable
{
    private static readonly Dictionary<string, string> AnyTags = new();

    private readonly string suffix = Guid.NewGuid().ToString("N");
    private readonly ActivitySource activities = new("Ogooreck.Api.Tests.InProcessTelemetry");
    private readonly Meter meter;

    public InProcessTelemetryTests() => meter = new Meter($"Ogooreck.Api.Tests.{suffix}");

    public void Dispose()
    {
        activities.Dispose();
        meter.Dispose();
    }

    [Fact]
    public async Task ExpectTrace_SeesASpanStartedWithAStringParentId()
    {
        await using var spec = Specification();

        await spec
            .Given()
            .When(Action(ctx =>
            {
                using var span = activities.StartActivity("parented by id", ActivityKind.Internal, ctx.Traceparent);
                return Task.CompletedTask;
            }))
            .Then([ExpectTrace(Span("parented by id"))], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Values_AreStringsLikeJavaStringValueOf_WhateverTheCultureOfTheRecordingThread()
    {
        var counter = meter.CreateCounter<int>($"culture.{suffix}");
        await using var spec = Specification();

        await spec
            .Given()
            .When(Action(_ => UnderCulture("pl-PL", () =>
            {
                using var span = activities.StartActivity("culture");
                span?.SetTag("paid", true);
                span?.SetTag("price", 1.5);
                counter.Add(1, new KeyValuePair<string, object?>("paid", true), new KeyValuePair<string, object?>("price", 1.5));
            })))
            .Then([
                ExpectTrace(Span("culture", new Dictionary<string, string> { ["paid"] = "true", ["price"] = "1.5" })),
                ExpectMetric($"culture.{suffix}", new Dictionary<string, string> { ["paid"] = "true", ["price"] = "1.5" }, 1)
            ], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SpanAttributes_AreWrittenLikeJavaStringValueOf_ForNullAndNumbersAndObjects()
    {
        await using var spec = Specification();

        await spec
            .Given()
            .When(Action(_ =>
            {
                using var span = activities.StartActivity("values");
                span?.AddTag("none", null);
                span?.AddTag("count", 3);
                span?.AddTag("list", new List<int>());
                return Task.CompletedTask;
            }))
            .Then([
                ExpectTrace(Span("values", new Dictionary<string, string>
                {
                    ["none"] = "null", ["count"] = "3", ["list"] = new List<int>().ToString()!
                }))
            ], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Histogram_CountsOnePerMeasurement_AndGaugeAndObservableCountTheirValue()
    {
        var histogram = meter.CreateHistogram<double>($"duration.{suffix}");
        var gauge = meter.CreateGauge<int>($"temperature.{suffix}");
        await using var spec = Specification();

        await spec
            .Given()
            .When(Action(_ =>
            {
                meter.CreateObservableCounter($"open.{suffix}", () => 5);
                histogram.Record(0.034);
                gauge.Record(21);
                return Task.CompletedTask;
            }))
            .Then([
                ExpectMetric($"duration.{suffix}", AnyTags, 1),
                ExpectMetric($"temperature.{suffix}", AnyTags, 21),
                ExpectMetric($"open.{suffix}", AnyTags, 5)
            ], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Disposing_ASpecification_LeavesAPassedInTelemetrySourceToTheCaller()
    {
        var source = new RecordingTelemetrySource();
        var spec = For<Program>().Telemetry(source);
        await spec.Given().When(CreateCart()).Then([ExpectResponse(Created)], TestContext.Current.CancellationToken);

        spec.Dispose();

        Assert.Equal(0, source.Disposals);
    }

    [Fact]
    public async Task DisposingAsync_ASpecification_LeavesAPassedInTelemetrySourceToTheCaller()
    {
        var source = new RecordingTelemetrySource();
        var spec = For<Program>().Telemetry(source);
        await spec.Given().When(CreateCart()).Then([ExpectResponse(Created)], TestContext.Current.CancellationToken);

        await spec.DisposeAsync();

        Assert.Equal(0, source.Disposals);
    }

    [Fact]
    public async Task EachSpecification_GetsItsOwnTelemetrySource_SoDisposingOneKeepsTheOtherRecording()
    {
        var first = For<Program>();
        await using var second = For<Program>();
        await first.Given().When(CreateCart()).Then([ExpectResponse(Created)], TestContext.Current.CancellationToken);
        await second.Given().When(CreateCart()).Then([ExpectResponse(Created)], TestContext.Current.CancellationToken);
        await first.DisposeAsync();

        await second.Given()
            .When(Action(_ =>
            {
                using var span = activities.StartActivity("after the first is disposed");
                return Task.CompletedTask;
            }))
            .Then([ExpectTrace(Span("after the first is disposed"))], TestContext.Current.CancellationToken);
    }

    private static ApiSpecification<Program> Specification() => For<Program>().Deadline(TimeSpan.FromSeconds(5));

    private static Task UnderCulture(string name, Action run)
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        try
        {
            run();
            return Task.CompletedTask;
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}

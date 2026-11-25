using System.Globalization;
using System.Net;
using Google.Protobuf;
using Microsoft.Extensions.Time.Testing;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Tests.API.Telemetry.OtlpPayloads;

namespace Ogooreck.Tests.API.Telemetry;

public class OtlpReceiverTests
{
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<HttpResponseMessage> Post(OtlpReceiver receiver, string path, byte[] body,
        string contentType = "application/x-protobuf")
    {
        using var client = new HttpClient();
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return await client.PostAsync(new Uri(receiver.Endpoint, path), content, Ct);
    }

    private static (Recorded<RecordedSpan> Spans, Recorded<RecordedMeasurement> Measurements) Attached(
        OtlpReceiver receiver)
    {
        var (spans, measurements) = (new Recorded<RecordedSpan>(), new Recorded<RecordedMeasurement>());
        receiver.Attach(spans, measurements);
        return (spans, measurements);
    }

    [Fact]
    public async Task Decoder_ReadsSpanIdsNameAndStringIntDoubleBoolAttributes()
    {
        await using var receiver = OtlpReceiver.Start();
        var (spans, _) = Attached(receiver);
        var previous = CultureInfo.DefaultThreadCurrentCulture;
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("pl-PL");

        try
        {
            var response = await Post(receiver, "v1/traces", TraceRequest(
                Span(TraceId, "b7ad6b7169203331", "00f067aa0ba902b7", "POST /carts/{id}/orders",
                    Text("cart.id", "42"), Int("cart.lines", 3), Double("cart.total", 1.5), Bool("cart.closed", true),
                    Array("cart.tags")),
                Span(TraceId, "00f067aa0ba902b7", "", "root"),
                Span(TraceId, "00f067aa0ba902b8", null, "no parent field")));

            Assert.Equal(OK, response.StatusCode);
            Assert.Equal("application/x-protobuf", response.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = previous;
        }

        var recorded = spans.Items();
        Assert.Equal(["POST /carts/{id}/orders", "root", "no parent field"], recorded.Select(span => span.Name));
        var first = recorded[0];
        Assert.Equal((TraceId, "b7ad6b7169203331", "00f067aa0ba902b7"), (first.TraceId, first.SpanId, first.ParentSpanId));
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["cart.id"] = "42",
                ["cart.lines"] = "3",
                ["cart.total"] = "1.5",
                ["cart.closed"] = "true"
            },
            first.Attributes);
        Assert.Null(recorded[1].ParentSpanId);
        Assert.Null(recorded[2].ParentSpanId);
        Assert.Empty(recorded[1].Attributes);
    }

    [Fact]
    public async Task Decoder_SkipsUnknownFieldsOfAnAttributeAndReadsAFalseBool()
    {
        await using var receiver = OtlpReceiver.Start();
        var (spans, _) = Attached(receiver);
        var attributeWithExtraField = Message(attribute =>
        {
            attribute.WriteTag(1, WireFormat.WireType.LengthDelimited);
            attribute.WriteString("note");
            attribute.Nested(2, Message(value =>
            {
                value.WriteTag(1, WireFormat.WireType.LengthDelimited);
                value.WriteString("text");
            }));
            attribute.WriteTag(3, WireFormat.WireType.Varint);
            attribute.WriteInt32(1);
        });

        var response = await Post(receiver, "v1/traces", TraceRequest(
            Span(TraceId, "b7ad6b7169203331", null, "span", attributeWithExtraField, Bool("cart.closed", false))));

        Assert.Equal(OK, response.StatusCode);
        Assert.Equal(
            new Dictionary<string, string> { ["note"] = "text", ["cart.closed"] = "false" },
            spans.Items().Single().Attributes);
    }

    [Fact]
    public async Task Decoder_ReadsSumGaugeAndHistogramPointsWithTemporality()
    {
        await using var receiver = OtlpReceiver.Start();
        var (_, measurements) = Attached(receiver);
        var tag = Text("cart.id", "7");

        var response = await Post(receiver, "v1/metrics", MetricsRequest(
            Sum("deltas", Delta, NumberPoint(2.5, tag), IntegerPoint(3, tag)),
            Sum("totals", Cumulative, IntegerPoint(10, tag)),
            Sum("unspecified", 0, NumberPoint(4)),
            Gauge("level", NumberPoint(8, tag)),
            Histogram("delta.durations", Delta, HistogramPoint(5, tag)),
            Histogram("cumulative.durations", Cumulative, HistogramPoint(9, tag))));

        Assert.Equal(OK, response.StatusCode);
        var seven = new Dictionary<string, string> { ["cart.id"] = "7" };
        Assert.Equal(
            [
                ("deltas", 2.5, false, seven), ("deltas", 3, false, seven), ("totals", 10, true, seven),
                ("unspecified", 4, true, new Dictionary<string, string>()), ("level", 8, true, seven),
                ("delta.durations", 5, false, seven), ("cumulative.durations", 9, true, seven)
            ],
            measurements.Items().Select(m => (m.Instrument, m.Value, m.IsCumulative, (Dictionary<string, string>)m.Tags)));
        Assert.All(measurements.Items(), measurement => Assert.Null(measurement.TraceId));
    }

    [Fact]
    public async Task Decoder_SkipsFieldsItDoesNotRead()
    {
        await using var receiver = OtlpReceiver.Start();
        var (spans, measurements) = Attached(receiver);
        var unknownMetric = Metric("summary", 11, Message(summary => summary.Nested(1, [1, 2, 3])));
        var exponential = Metric("exponential", 10, Message(histogram => histogram.Nested(1, [1, 2, 3])));

        await Post(receiver, "v1/traces", TraceRequest(Span(TraceId, "b7ad6b7169203331", null, "kept")));
        await Post(receiver, "v1/metrics", MetricsRequest(unknownMetric, exponential, Sum("kept", Delta, NumberPoint(1))));

        Assert.Equal(["kept"], spans.Items().Select(span => span.Name));
        Assert.Equal(["kept"], measurements.Items().Select(measurement => measurement.Instrument));
    }

    [Fact]
    public async Task Receiver_RejectsNonProtobufContentType()
    {
        await using var receiver = OtlpReceiver.Start();
        var (spans, _) = Attached(receiver);

        var response = await Post(receiver, "v1/traces", "{}"u8.ToArray(), "application/json");

        Assert.Equal(UnsupportedMediaType, response.StatusCode);
        Assert.Contains("application/x-protobuf", await response.Content.ReadAsStringAsync(Ct));
        Assert.Empty(spans.Items());
    }

    [Fact]
    public async Task Receiver_RejectsARequestWithoutAContentType()
    {
        await using var receiver = OtlpReceiver.Start();
        using var client = new HttpClient();

        var response = await client.PostAsync(new Uri(receiver.Endpoint, "v1/metrics"), new ByteArrayContent([]), Ct);

        Assert.Equal(UnsupportedMediaType, response.StatusCode);
        Assert.Contains("has content type none", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Receiver_RejectsABodyThatIsNotProtobuf()
    {
        await using var receiver = OtlpReceiver.Start();

        var response = await Post(receiver, "v1/traces", [0x0A, 0xFF, 0xFF, 0xFF, 0x0F]);

        Assert.Equal(BadRequest, response.StatusCode);
        Assert.Contains("not a valid protobuf message", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Receiver_ListensOnItsOwnLocalPort()
    {
        await using var first = OtlpReceiver.Start();
        await using var second = OtlpReceiver.Start();

        Assert.True(first.Endpoint.IsLoopback);
        Assert.NotEqual(first.Endpoint.Port, second.Endpoint.Port);
        Assert.Equal(NotFound, (await new HttpClient().GetAsync(new Uri(first.Endpoint, "v1/other"), Ct)).StatusCode);
    }

    [Fact]
    public async Task Receiver_FeedsEverySpecificationItIsAttachedTo()
    {
        await using var receiver = OtlpReceiver.Start();
        var (firstSpans, _) = Attached(receiver);
        var (secondSpans, _) = Attached(receiver);

        await Post(receiver, "v1/traces", TraceRequest(Span(TraceId, "b7ad6b7169203331", null, "shared")));

        Assert.Single(firstSpans.Items());
        Assert.Single(secondSpans.Items());
    }

    [Fact]
    public async Task Receiver_StopsAcceptingExportsWhenDisposed()
    {
        var receiver = OtlpReceiver.Start();
        await receiver.DisposeAsync();

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => Post(receiver, "v1/traces", TraceRequest()));
        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task Collect_DoesNothingByDefault()
    {
        await using var receiver = OtlpReceiver.Start();

        var collecting = receiver.Collect(Ct);

        Assert.True(collecting.IsCompletedSuccessfully);
        await collecting;
    }

    [Fact]
    public async Task Collect_WithWaitForMetricsExport_WaitsForTheNextTwoMetricsExports_NotForTraces()
    {
        await using var receiver = OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true });
        Attached(receiver);

        var collecting = receiver.Collect(Ct);
        await Post(receiver, "v1/traces", TraceRequest());
        Assert.False(collecting.IsCompleted);
        await Post(receiver, "v1/metrics", MetricsRequest());
        Assert.False(collecting.IsCompleted);
        await Post(receiver, "v1/metrics", MetricsRequest());

        await collecting.WaitAsync(TimeSpan.FromSeconds(10), Ct);
    }

    [Fact]
    public async Task Collect_WithWaitForMetricsExport_EndsWhenCancelled()
    {
        await using var receiver = OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var collecting = receiver.Collect(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collecting);
    }

    [Fact]
    public async Task Collect_WithWaitForMetricsExport_FailsAtTheDeadlineWhenNoExportArrivesBeforeTheBaseline()
    {
        await using var receiver = OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true });
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .Telemetry(receiver)
            .Clock(new PassingClock())
            .Deadline(TimeSpan.FromSeconds(2));

        var failure = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct));

        Assert.Equal(
            "Before the When step: the telemetry source OtlpReceiver did not finish collecting within the deadline of 00:00:02. With WaitForMetricsExport it waits for two metrics exports, and they did not arrive.",
            failure.Message);
    }

    [Fact]
    public async Task ExpectMetric_WithWaitForMetricsExport_FailsAtTheDeadlineWhenNoExportArrivesAfterTheBaseline()
    {
        await using var receiver = OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true });
        var clock = new FakeTimeProvider();
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .Telemetry(receiver)
            .Clock(clock)
            .Deadline(TimeSpan.FromSeconds(2));
        var whenRan = new TaskCompletionSource();

        var then = api.Given()
            .When(Action(_ =>
            {
                whenRan.TrySetResult();
                return Task.CompletedTask;
            }))
            .Then([ExpectMetric("orders.placed", new Dictionary<string, string>(), 1)], Ct);
        while (!whenRan.Task.IsCompleted)
            await Post(receiver, "v1/metrics", MetricsRequest());
        while (!then.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(2));
            await Task.Yield();
        }

        var failure = await Assert.ThrowsAsync<AssertionException>(() => then);

        Assert.Equal(
            "When action: ExpectMetric(orders.placed, {}, 1): the telemetry source OtlpReceiver did not finish collecting within the deadline of 00:00:02. With WaitForMetricsExport it waits for two metrics exports, and they did not arrive.",
            failure.Message);
    }

    [Fact]
    public async Task CancellingThen_WhileTheBaselineWaitsForAMetricsExport_EndsWithOperationCanceledException()
    {
        await using var receiver = OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true });
        await using var api = For(() => Task.FromResult(new Uri("http://localhost"))).Telemetry(receiver);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var then = api.Given().When(Action(_ => Task.CompletedTask)).Then([], cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => then);
    }

    [Fact]
    public async Task ExpectMetric_OverOtlp_SumsDeltasAndSubtractsTheCumulativeBaseline()
    {
        await using var receiver = OtlpReceiver.Start();
        await using var api = For(() => Task.FromResult(new Uri("http://localhost"))).Telemetry(receiver);
        var tags = new Dictionary<string, string> { ["cart.id"] = "7" };
        var cart = Text("cart.id", "7");

        await api.Given(Action(_ => ExportMetrics(receiver, MetricsRequest(
                Sum("totals", Cumulative, NumberPoint(10, cart)),
                Sum("deltas", Delta, NumberPoint(100, cart)),
                Histogram("durations", Cumulative, HistogramPoint(4, cart))))))
            .When(Action(_ => ExportMetrics(receiver, MetricsRequest(
                Sum("totals", Cumulative, NumberPoint(13, cart)),
                Sum("deltas", Delta, NumberPoint(2, cart), NumberPoint(1, cart)),
                Gauge("level", NumberPoint(6, cart)),
                Histogram("durations", Cumulative, HistogramPoint(6, cart))))))
            .Then([
                ExpectMetric("totals", tags, 3),
                ExpectMetric("deltas", tags, 3),
                ExpectMetric("level", tags, 6),
                ExpectMetric("durations", tags, 2)
            ], Ct);
    }

    [Fact]
    public async Task ExpectTrace_OverOtlp_SeesTheSpansOfTheStepsTrace()
    {
        await using var receiver = OtlpReceiver.Start();
        await using var api = For(() => Task.FromResult(new Uri("http://localhost"))).Telemetry(receiver);

        await api.Given()
            .When(Action(async context =>
            {
                var traceId = context.Traceparent.Split('-')[1];
                await ExportTraces(receiver, TraceRequest(
                    Span(traceId, "00f067aa0ba902b7", null, "POST /carts/{id}/orders", Bool("cart.closed", true)),
                    Span(traceId, "b7ad6b7169203331", "00f067aa0ba902b7", "publish OrderPlaced")));
            }))
            .Then([
                ExpectTrace(Span("POST /carts/{id}/orders",
                    new Dictionary<string, string> { ["cart.closed"] = "true" }, Span("publish OrderPlaced")))
            ], Ct);
    }

    [Fact]
    public async Task ExpectTrace_WhenNoExportArrivesBeforeTheDeadline_FailsSayingNoMatchingSpansWereReceived()
    {
        await using var receiver = OtlpReceiver.Start();
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .Telemetry(receiver)
            .Deadline(TimeSpan.FromSeconds(2));

        var failure = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Action(_ => Task.CompletedTask)).Then([ExpectTrace(Span("POST /carts/{id}/orders"))], Ct));

        Assert.Contains("No span 'POST /carts/{id}/orders'", failure.Message);
        Assert.Contains("(none)", failure.Message);
    }

    private static async Task ExportTraces(OtlpReceiver receiver, byte[] request) =>
        (await Post(receiver, "v1/traces", request)).EnsureSuccessStatusCode();

    private static async Task ExportMetrics(OtlpReceiver receiver, byte[] request) =>
        (await Post(receiver, "v1/metrics", request)).EnsureSuccessStatusCode();
}

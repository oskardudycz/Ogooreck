using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests.API.Telemetry;

public class TelemetryMatchingTests
{
    private const string MessageId = "messaging.message.id";
    private const string CorrelationId = "messaging.message.correlation_id";
    private const string CausationId = "messaging.message.causation_id";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FeedingTelemetry: ITelemetrySource
    {
        public Recorded<RecordedSpan> Spans { get; private set; } = new();

        public Recorded<RecordedMeasurement> Measurements { get; private set; } = new();

        public void Attach(Recorded<RecordedSpan> spans, Recorded<RecordedMeasurement> measurements)
        {
            Spans = spans;
            Measurements = measurements;
        }

        public Task Collect(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static readonly IReadOnlyDictionary<string, string> NoAttributes = new Dictionary<string, string>();

    private readonly FeedingTelemetry telemetry = new();

    private ApiSpecification Api() =>
        For(() => Task.FromResult(new Uri("http://localhost")))
            .Telemetry(telemetry)
            .Deadline(TimeSpan.FromSeconds(1));

    private static string TraceIdOf(StepContext context) => context.Traceparent.Split('-')[1];

    private TestStep Emitting(Action<string, Recorded<RecordedSpan>, Recorded<RecordedMeasurement>> emit) =>
        Action(context =>
        {
            emit(TraceIdOf(context), telemetry.Spans, telemetry.Measurements);
            return Task.CompletedTask;
        });

    private static RecordedSpan SpanOf(string traceId, string spanId, string? parentSpanId, string name,
        params (string Key, string Value)[] attributes) =>
        new(traceId, spanId, parentSpanId, name, attributes.ToDictionary(pair => pair.Key, pair => pair.Value));

    [Fact]
    public async Task ExpectTrace_ListsSpansWithAndWithoutAParent()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            Api().Given()
                .When(Emitting((trace, spans, _) =>
                {
                    spans.Add(SpanOf(trace, "00000000000000a1", null, "root"));
                    spans.Add(SpanOf(trace, "00000000000000a2", "00000000000000a1", "child"));
                }))
                .Then([ExpectTrace(Span("missing"))], Ct));

        Assert.Contains("root (trace", error.Message);
        Assert.Contains("parent none)", error.Message);
        Assert.Contains("parent 00000000000000a1)", error.Message);
    }

    [Fact]
    public async Task ExpectTrace_ComparesTheAttributesOfEverySpanWithTheExpectedName()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            Api().Given()
                .When(Emitting((trace, spans, _) =>
                {
                    spans.Add(SpanOf(trace, "00000000000000a1", null, "handle"));
                    spans.Add(SpanOf(trace, "00000000000000a2", null, "handle", ("channel", "mobile")));
                }))
                .Then([ExpectTrace(Span("handle", new Dictionary<string, string> { ["channel"] = "web" }))], Ct));

        Assert.Contains("No span 'handle' with attributes {channel=web}, the spans of that name have [{}, {channel=mobile}]", error.Message);
    }

    [Fact]
    public Task ExpectTrace_FindsAChildBelowAParentWhileTheTestAlsoHasSpansOfAnotherTrace() =>
        Api().Given(Emitting((trace, spans, _) => spans.Add(SpanOf(trace, "00000000000000b1", null, "earlier"))))
            .When(Emitting((trace, spans, _) =>
            {
                spans.Add(SpanOf(trace, "00000000000000a1", null, "parent"));
                spans.Add(SpanOf(trace, "00000000000000a2", "00000000000000a1", "child"));
            }))
            .Then([ExpectTrace(Span("parent", Span("child")))], Ct);

    [Fact]
    public Task ExpectCorrelatedTrace_AcceptsASpanThatNamesItsOwnMessageAsItsCause() =>
        Api().Given()
            .When(Emitting((trace, spans, _) => spans.Add(SpanOf(trace, "00000000000000a1", null, "handle",
                (CorrelationId, "c1"), (MessageId, "m1"), (CausationId, "m1")))))
            .Then([ExpectCorrelatedTrace()], Ct);

    [Fact]
    public Task ExpectMetric_CountsOnlyMeasurementsWhoseTagsMatch() =>
        Api().Given()
            .When(Emitting((trace, _, measurements) =>
            {
                measurements.Add(new RecordedMeasurement("orders.placed", NoAttributes, 1, trace, false));
                measurements.Add(new RecordedMeasurement("orders.placed", new Dictionary<string, string> { ["channel"] = "mobile" }, 1, trace, false));
                measurements.Add(new RecordedMeasurement("orders.placed", new Dictionary<string, string> { ["channel"] = "web" }, 1, trace, false));
            }))
            .Then([ExpectMetric("orders.placed", new Dictionary<string, string> { ["channel"] = "web" }, 1)], Ct);
}

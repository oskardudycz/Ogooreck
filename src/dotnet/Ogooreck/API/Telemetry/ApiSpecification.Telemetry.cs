using System.Globalization;
using Ogooreck.Assertions;

namespace Ogooreck.API;

public partial class ApiSpecification
{
    private const string MessageId = "messaging.message.id";
    private const string CorrelationId = "messaging.message.correlation_id";
    private const string CausationId = "messaging.message.causation_id";

    /// <summary>
    /// Checks what the app did inside: waits until the test's finished spans contain the expected tree, and fails at the deadline listing those spans.
    /// The test's spans are those whose trace id belongs to a Given step or an attempt of the When step, so spans of parallel tests don't count.
    /// A child expected span may be anywhere below its parent in the same trace. Spans that finish after the response are waited for.
    /// </summary>
    /// <example>
    /// <code>
    /// await api.Given(AddProduct(cartId, "socks"))
    ///     .When(PlaceOrder(cartId))
    ///     .Then([ExpectTrace(Span("POST /carts/{id}/orders", Span("publish OrderPlaced")))], ct);
    /// </code>
    /// </example>
    public static Expectation ExpectTrace(ExpectedSpan root) =>
        new(async outcome =>
        {
            var call = $"ExpectTrace({root})";
            RequireTelemetry(outcome, call);

            IReadOnlyList<RecordedSpan> spans = [];
            await Waiting.Until(outcome, call,
                _ =>
                {
                    spans = outcome.Telemetry.Spans();
                    return Task.FromResult(SpanTree.MismatchOf(root, spans));
                },
                mismatch =>
                    $"{outcome.When}: {call}: {mismatch}, when the deadline of {outcome.Deadline} passed.{Environment.NewLine}Finished spans of the test:{Environment.NewLine}{SpanTree.Describe(spans)}");
        });

    /// <summary>Describes a span for <c>ExpectTrace</c> by its name, with the spans expected below it. Any attributes are allowed.</summary>
    /// <example>
    /// <code>
    /// Span("POST /carts/{id}/orders", Span("publish OrderPlaced"))
    /// </code>
    /// </example>
    public static ExpectedSpan Span(string name, params ExpectedSpan[] children) =>
        new(name, new Dictionary<string, string>(), children);

    /// <summary>
    /// Describes a span for <c>ExpectTrace</c> by its name and the attributes it must have, with the spans expected below it.
    /// Attribute values are compared as strings, as Java's <c>String.valueOf</c> writes them: <c>true</c>, <c>1.5</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// ExpectTrace(Span("POST /carts/{id}/orders", new Dictionary&lt;string, string&gt; { ["cart.closed"] = "true", ["cart.total"] = "1.5" }))
    /// </code>
    /// </example>
    public static ExpectedSpan Span(string name, IReadOnlyDictionary<string, string> attributes,
        params ExpectedSpan[] children) =>
        new(name, attributes, children);

    /// <summary>
    /// Checks that correlation ids flow through the app's handlers. It checks the test's finished spans once, after catch-up:
    /// every span in a trace that carries <c>messaging.message.correlation_id</c> carries the same one,
    /// and a span with <c>messaging.message.causation_id</c> is below the span whose <c>messaging.message.id</c> it names, in the same trace.
    /// It fails when no span carries a correlation id. Put it after an <c>ExpectTrace</c> that waits for the last span you care about.
    /// </summary>
    /// <example>
    /// <code>
    /// .Then([
    ///     ExpectTrace(Span("POST /carts/{id}/orders", Span("publish OrderPlaced", Span("handle OrderPlaced")))),
    ///     ExpectCorrelatedTrace()
    /// ], ct);
    /// </code>
    /// </example>
    public static Expectation ExpectCorrelatedTrace() =>
        new(outcome =>
        {
            const string call = "ExpectCorrelatedTrace()";
            RequireTelemetry(outcome, call);

            var spans = outcome.Telemetry.Spans();
            var mismatch = CorrelationMismatchOf(spans);
            if (mismatch is not null)
                throw new AssertionException(
                    $"{outcome.When}: {call}: {mismatch}.{Environment.NewLine}Finished spans of the test:{Environment.NewLine}{SpanTree.Describe(spans)}");
            return Task.CompletedTask;
        });

    /// <summary>
    /// Checks what the app reports: waits until the increase of metric <paramref name="name"/> since the baseline, taken before the When step,
    /// summed across series whose tags include <paramref name="tags"/>, equals <paramref name="increase"/>.
    /// It fails at once when the increase is greater, and at the deadline otherwise.
    /// </summary>
    /// <remarks>
    /// A histogram counts its measurements, not their sum. Observable instruments count their latest value minus the baseline value.
    /// Measurements recorded under the test's traces count; measurements without a trace id, such as observable instruments or work outside any request,
    /// count by tags only, so filter them by a tag unique to the test or run those tests serially.
    /// </remarks>
    /// <example>
    /// <code>
    /// await api.Given(AddProduct(cartId, "socks"))
    ///     .When(PlaceOrder(cartId))
    ///     .Then([ExpectMetric("orders.placed", new Dictionary&lt;string, string&gt; { ["channel"] = "web" }, 1)], ct);
    /// </code>
    /// </example>
    public static Expectation ExpectMetric(string name, IReadOnlyDictionary<string, string> tags, double increase) =>
        new(async outcome =>
        {
            var call = $"ExpectMetric({name}, {SpanTree.Describe(tags)}, {Format(increase)})";
            RequireTelemetry(outcome, call);

            await Waiting.Until(outcome, call,
                async ct =>
                {
                    var found = await outcome.Telemetry.Increase($"{outcome.When}: {call}", name, tags, ct);
                    if (found > increase)
                        throw new AssertionException(
                            $"{outcome.When}: {call}: expected an increase of {Format(increase)} since the baseline, but found {Format(found)}, which is more.");
                    return found == increase ? null : Format(found);
                },
                found =>
                    $"{outcome.When}: {call}: expected an increase of {Format(increase)} since the baseline, but found {found} when the deadline of {outcome.Deadline} passed.");
        });

    private static void RequireTelemetry(Outcome outcome, string call)
    {
        if (outcome.Telemetry.IsOff)
            throw new AssertionException(
                $"{outcome.When}: {call} needs telemetry, but telemetry is off for this specification; configure a source with Telemetry(...), for example TelemetrySources.InProcess().");
    }

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? CorrelationMismatchOf(IReadOnlyList<RecordedSpan> spans)
    {
        var correlated = spans.Where(span => span.Attributes.ContainsKey(CorrelationId)).ToList();
        if (correlated.Count == 0)
            return $"No span carries the attribute {CorrelationId}";

        foreach (var trace in correlated.GroupBy(span => span.TraceId))
        {
            var correlationId = trace
                .GroupBy(span => span.Attributes[CorrelationId])
                .MaxBy(group => group.Count())!
                .Key;

            foreach (var span in trace)
            {
                var spanCorrelationId = span.Attributes[CorrelationId];
                if (spanCorrelationId != correlationId)
                    return $"Span '{span.Name}' carries correlation '{spanCorrelationId}', but its trace carries '{correlationId}'";
            }
        }

        var handlersOf = spans
            .Where(span => span.Attributes.ContainsKey(MessageId))
            .GroupBy(span => span.Attributes[MessageId])
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var span in spans)
        {
            if (!span.Attributes.TryGetValue(CausationId, out var causationId)
                || span.Attributes.TryGetValue(CorrelationId, out var spanCorrelationId) && causationId == spanCorrelationId)
                continue;

            if (!handlersOf.TryGetValue(causationId, out var handlers))
                return $"Span '{span.Name}' was caused by message {causationId}, but no span handled that message";

            if (!handlers.Any(handler => ReferenceEquals(handler, span) || SpanTree.IsBelow(span, handler, spans)))
                return $"Span '{span.Name}' was caused by message {causationId}, but is not below '{handlers[0].Name}' that handled it";
        }

        return null;
    }
}

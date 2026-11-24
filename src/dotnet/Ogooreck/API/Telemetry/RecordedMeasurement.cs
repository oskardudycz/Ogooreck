namespace Ogooreck.API;

/// <summary>
/// One measurement of an instrument, as an <see cref="ITelemetrySource"/> adds it. <c>ExpectMetric</c> reads these.
/// Create one only in a custom source.
/// </summary>
/// <param name="Instrument">The instrument name that <c>ExpectMetric</c> matches, for example <c>orders.placed</c>.</param>
/// <param name="Tags">The tags, with values written as Java's <c>String.valueOf</c> does. Measurements with the same instrument and tags form a series.</param>
/// <param name="Value">The increment, or the total when <paramref name="IsCumulative"/>. For a histogram, the number of measurements.</param>
/// <param name="TraceId">The trace id the measurement was recorded under, or null when it can't be tied to a trace. Null measurements count by tags only.</param>
/// <param name="IsCumulative">True when <paramref name="Value"/> is a total, such as an observable counter or a gauge; the increase is then the latest value minus the baseline value.</param>
/// <example>
/// <code>
/// measurements.Add(new RecordedMeasurement("orders.placed", new Dictionary&lt;string, string&gt; { ["channel"] = "web" }, 1, traceId, false));
/// </code>
/// </example>
public sealed record RecordedMeasurement(string Instrument, IReadOnlyDictionary<string, string> Tags, double Value,
    string? TraceId, bool IsCumulative);

namespace Ogooreck.API;

/// <summary>
/// Where a specification gets spans and measurements for <c>ExpectTrace</c>, <c>ExpectCorrelatedTrace</c> and <c>ExpectMetric</c>.
/// Use <see cref="TelemetrySources.InProcess"/> or <see cref="TelemetrySources.Off"/>, or implement it to feed telemetry from somewhere else.
/// Pass it to <c>Telemetry(...)</c>; the specification attaches it on its first step, and the caller disposes it.
/// </summary>
/// <example>
/// A source that a test feeds itself:
/// <code>
/// public sealed class RecordingTelemetrySource: ITelemetrySource
/// {
///     public Recorded&lt;RecordedSpan&gt;? Spans { get; private set; }
///
///     public void Attach(Recorded&lt;RecordedSpan&gt; spans, Recorded&lt;RecordedMeasurement&gt; measurements) => Spans = spans;
///
///     public Task Collect(CancellationToken ct) => Task.CompletedTask;
///
///     public ValueTask DisposeAsync() => ValueTask.CompletedTask;
/// }
/// </code>
/// </example>
public interface ITelemetrySource: IAsyncDisposable
{
    /// <summary>
    /// Called once, on the first step, by the specification that uses the source. Start listening here, and add every finished span
    /// and every measurement to the recorders. Adding never blocks.
    /// </summary>
    /// <example>
    /// <code>
    /// public void Attach(Recorded&lt;RecordedSpan&gt; spans, Recorded&lt;RecordedMeasurement&gt; measurements) =&gt; Spans = spans;
    /// </code>
    /// </example>
    public void Attach(Recorded<RecordedSpan> spans, Recorded<RecordedMeasurement> measurements);

    /// <summary>
    /// Called before the When step, when Ogooreck takes the metric baseline, and before each <c>ExpectMetric</c> check.
    /// Record values that are read on demand here, such as observable instruments. Honour <paramref name="ct"/>, the token given to <c>Then</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// public Task Collect(CancellationToken ct) =&gt; Task.CompletedTask;
    /// </code>
    /// </example>
    public Task Collect(CancellationToken ct);
}

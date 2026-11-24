namespace Ogooreck.API;

/// <summary>The telemetry sources Ogooreck ships, for <c>Telemetry(...)</c>.</summary>
/// <example>
/// <code>
/// For&lt;Program&gt;().Telemetry(TelemetrySources.InProcess())
/// </code>
/// </example>
public static class TelemetrySources
{
    /// <summary>
    /// A new source that listens, from the specification's first step until its disposal, to every <see cref="System.Diagnostics.ActivitySource"/>
    /// and every <see cref="System.Diagnostics.Metrics.Meter"/> instrument in the test process. It records each measurement with the trace id of the current activity.
    /// It is the default of an in-process specification, which disposes its own; a source you create and pass to <c>Telemetry(...)</c> is yours to dispose.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var source = TelemetrySources.InProcess();
    /// await using var spec = For&lt;Program&gt;().Telemetry(source);
    /// </code>
    /// </example>
    public static ITelemetrySource InProcess() => new InProcessTelemetry();

    /// <summary>
    /// No telemetry: nothing listens, and <c>ExpectTrace</c>, <c>ExpectCorrelatedTrace</c> and <c>ExpectMetric</c> fail at once, saying that telemetry is off.
    /// Use it for a specification that never checks telemetry and shouldn't sample every activity.
    /// </summary>
    /// <example>
    /// <code>
    /// await using var spec = For&lt;Program&gt;().Telemetry(TelemetrySources.Off);
    /// </code>
    /// </example>
    public static ITelemetrySource Off { get; } = new TelemetryOff();
}

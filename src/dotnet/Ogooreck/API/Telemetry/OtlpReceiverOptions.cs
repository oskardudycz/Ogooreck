namespace Ogooreck.API;

/// <summary>Options for <see cref="OtlpReceiver.Start(OtlpReceiverOptions)"/>.</summary>
public sealed class OtlpReceiverOptions
{
    /// <summary>
    /// When true, each <see cref="ITelemetrySource.Collect"/> of the receiver waits until the next two metrics exports arrive (the one in flight may predate the latest increment), so that the metric baseline
    /// and each <c>ExpectMetric</c> check include everything the app exported up to that point. The wait is bounded by the specification's deadline.
    /// The default is false: <c>Collect</c> returns at once, because data arrives when the app exports it.
    /// </summary>
    /// <example>
    /// <code>
    /// var otlp = OtlpReceiver.Start(new OtlpReceiverOptions { WaitForMetricsExport = true });
    /// </code>
    /// </example>
    public bool WaitForMetricsExport { get; init; }
}

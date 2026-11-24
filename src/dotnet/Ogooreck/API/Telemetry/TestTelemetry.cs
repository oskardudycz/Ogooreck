using System.Diagnostics;
using Ogooreck.Assertions;

namespace Ogooreck.API;

internal sealed class TestTelemetry(SpecificationSettings settings)
{
    private readonly HashSet<string> traceIds = [];
    private int baseline;

    public bool IsOff => settings.Telemetry is TelemetryOff;

    public void Add(ActivityTraceId traceId) => traceIds.Add(traceId.ToHexString());

    public IReadOnlyList<RecordedSpan> Spans() =>
        settings.Spans.Items().Where(span => traceIds.Contains(span.TraceId)).ToList();

    public async Task TakeBaseline(CancellationToken ct)
    {
        await Collect("Before the When step", ct);
        baseline = settings.Measurements.Count();
    }

    public async Task<double> Increase(string where, string name, IReadOnlyDictionary<string, string> tags,
        CancellationToken ct)
    {
        await Collect(where, ct);
        var measurements = settings.Measurements.Items();
        var counted = measurements
            .Select((measurement, index) => (Measurement: measurement, BeforeBaseline: index < baseline))
            .Where(each => each.Measurement.Instrument == name && HasTags(each.Measurement, tags) && Counts(each.Measurement))
            .ToList();

        var delta = counted
            .Where(each => !each.Measurement.IsCumulative && !each.BeforeBaseline)
            .Sum(each => each.Measurement.Value);

        var cumulative = counted
            .Where(each => each.Measurement.IsCumulative)
            .GroupBy(each => SeriesOf(each.Measurement))
            .Sum(series =>
            {
                var before = series.LastOrDefault(each => each.BeforeBaseline).Measurement?.Value ?? 0;
                return series.Last().Measurement.Value - before;
            });

        return delta + cumulative;
    }

    private async Task Collect(string where, CancellationToken ct)
    {
        using var deadlineTimer = new CancellationTokenSource(settings.Deadline, settings.Clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct, deadlineTimer.Token);
        try
        {
            await settings.Telemetry.Collect(deadline.Token);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            var hint = settings.Telemetry is OtlpReceiver { WaitsForMetricsExport: true }
                ? " With WaitForMetricsExport it waits for two metrics exports, and they did not arrive."
                : "";
            throw new AssertionException(
                $"{where}: the telemetry source {settings.Telemetry.GetType().Name} did not finish collecting within the deadline of {settings.Deadline}.{hint}",
                exception);
        }
    }

    private bool Counts(RecordedMeasurement measurement) =>
        measurement.TraceId is null || traceIds.Contains(measurement.TraceId);

    private static bool HasTags(RecordedMeasurement measurement, IReadOnlyDictionary<string, string> tags) =>
        tags.All(tag => measurement.Tags.TryGetValue(tag.Key, out var value) && value == tag.Value);

    private static string SeriesOf(RecordedMeasurement measurement) =>
        SpanTree.Describe(measurement.Tags);
}

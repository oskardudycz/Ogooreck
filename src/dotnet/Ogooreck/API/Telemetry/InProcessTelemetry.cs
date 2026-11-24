using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Numerics;

namespace Ogooreck.API;

internal sealed class InProcessTelemetry: ITelemetrySource
{
    private ActivityListener? activities;
    private MeterListener? meters;
    private Recorded<RecordedMeasurement>? recorded;
    private Dictionary<string, RecordedMeasurement>? observed;
    private readonly Lock collecting = new();

    public void Attach(Recorded<RecordedSpan> spans, Recorded<RecordedMeasurement> measurements)
    {
        activities = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => spans.Add(SpanOf(activity))
        };
        ActivitySource.AddActivityListener(activities);

        recorded = measurements;
        meters = new MeterListener
        {
            InstrumentPublished = (instrument, listener) => listener.EnableMeasurementEvents(instrument, measurements)
        };
        meters.SetMeasurementEventCallback<byte>(Record);
        meters.SetMeasurementEventCallback<short>(Record);
        meters.SetMeasurementEventCallback<int>(Record);
        meters.SetMeasurementEventCallback<long>(Record);
        meters.SetMeasurementEventCallback<float>(Record);
        meters.SetMeasurementEventCallback<double>(Record);
        meters.SetMeasurementEventCallback<decimal>(Record);
        meters.Start();
    }

    public Task Collect(CancellationToken ct)
    {
        if (meters is null || recorded is null)
            return Task.CompletedTask;

        lock (collecting)
        {
            observed = [];
            try
            {
                meters.RecordObservableInstruments();
                foreach (var total in observed.Values)
                    recorded.Add(total);
            }
            finally
            {
                observed = null;
            }
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        activities?.Dispose();
        meters?.Dispose();
        return ValueTask.CompletedTask;
    }

    internal static string StringOf(object? value) =>
        value switch
        {
            null => "null",
            string text => text,
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => $"{value}"
        };

    private static RecordedSpan SpanOf(Activity activity) =>
        new(activity.TraceId.ToHexString(),
            activity.SpanId.ToHexString(),
            activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString(),
            activity.DisplayName,
            activity.TagObjects.ToDictionary(tag => tag.Key, tag => StringOf(tag.Value)));

    private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state) where T : struct, INumberBase<T>
    {
        var tagValues = new Dictionary<string, string>();
        foreach (var tag in tags)
            tagValues[tag.Key] = StringOf(tag.Value);

        var measurement = new RecordedMeasurement(
            instrument.Name,
            tagValues,
            instrument is Histogram<T> ? 1 : double.CreateTruncating(value),
            instrument.IsObservable ? null : Activity.Current?.TraceId.ToHexString(),
            instrument.IsObservable || instrument is Gauge<T>);

        if (!instrument.IsObservable || observed is null)
        {
            ((Recorded<RecordedMeasurement>)state!).Add(measurement);
            return;
        }

        var series = $"{instrument.Name} {SpanTree.Describe(tagValues)}";
        observed[series] = observed.TryGetValue(series, out var sum)
            ? sum with { Value = sum.Value + measurement.Value }
            : measurement;
    }
}

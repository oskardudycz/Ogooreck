using Ogooreck.API;

namespace Ogooreck.Api.Tests.Telemetry;

public sealed class RecordingTelemetrySource: ITelemetrySource
{
    public Recorded<RecordedSpan>? Spans { get; private set; }

    public Recorded<RecordedMeasurement>? Measurements { get; private set; }

    public int Collects { get; private set; }

    public int Disposals { get; private set; }

    public void Attach(Recorded<RecordedSpan> spans, Recorded<RecordedMeasurement> measurements)
    {
        Spans = spans;
        Measurements = measurements;
    }

    public Task Collect(CancellationToken ct)
    {
        Collects++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return ValueTask.CompletedTask;
    }
}

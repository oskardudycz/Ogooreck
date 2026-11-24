namespace Ogooreck.API;

internal sealed class TelemetryOff: ITelemetrySource
{
    public void Attach(Recorded<RecordedSpan> spans, Recorded<RecordedMeasurement> measurements) { }

    public Task Collect(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

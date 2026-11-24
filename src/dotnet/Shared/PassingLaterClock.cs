namespace Ogooreck.Testing;

public sealed class PassingLaterClock: TimeProvider
{
    private readonly long origin = System.GetTimestamp();
    private readonly PassingClock passing = new();
    private readonly long passingOrigin;
    private long passedAt = -1;

    public PassingLaterClock() => passingOrigin = passing.GetTimestamp();

    public void PassFromNow() => Interlocked.CompareExchange(ref passedAt, System.GetElapsedTime(origin).Ticks, -1);

    private bool Passing => Interlocked.Read(ref passedAt) >= 0;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() =>
        Passing
            ? Interlocked.Read(ref passedAt) + passing.GetTimestamp() - passingOrigin
            : System.GetElapsedTime(origin).Ticks;

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        Passing ? passing.CreateTimer(callback, state, dueTime, period) : System.CreateTimer(callback, state, dueTime, period);
}

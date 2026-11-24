namespace Ogooreck.Testing;

public sealed class PassingClock: TimeProvider
{
    private long ticks = DateTimeOffset.UnixEpoch.Ticks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);

    public override long GetTimestamp() => Interlocked.Read(ref ticks);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan by) => Interlocked.Add(ref ticks, by.Ticks);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new PassingTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    private sealed class PassingTimer(PassingClock clock, TimerCallback callback, object? state): ITimer
    {
        private int version;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            var scheduled = Interlocked.Increment(ref version);
            if (dueTime == Timeout.InfiniteTimeSpan)
                return true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (Volatile.Read(ref version) != scheduled)
                    return;
                Interlocked.Add(ref clock.ticks, dueTime.Ticks);
                callback(state);
            });
            return true;
        }

        public void Dispose() => Interlocked.Increment(ref version);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

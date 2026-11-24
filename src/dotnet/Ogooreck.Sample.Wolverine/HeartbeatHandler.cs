using Ogooreck.Sample.Wolverine.Events;

namespace Ogooreck.Sample.Wolverine;

public static class HeartbeatHandler
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(10);

    public static async Task<Heartbeat> Handle(Heartbeat heartbeat)
    {
        await Task.Delay(Interval);
        return new Heartbeat(heartbeat.Id);
    }
}

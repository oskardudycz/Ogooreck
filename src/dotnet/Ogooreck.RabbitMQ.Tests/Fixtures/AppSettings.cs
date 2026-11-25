namespace Ogooreck.RabbitMQ.Tests.Fixtures;

public sealed record AppSettings(
    QueueNames Queues,
    ushort DispatchConcurrency = 1,
    bool ShipOrders = true,
    bool AutoAck = false,
    bool ConsumeShippingOutsideTheContainer = false,
    TimeSpan? Deadline = null)
{
    public static AppSettings Shared { get; } = new(QueueNames.Shared);

    public static AppSettings Own() => new(QueueNames.Unique());
}

namespace Ogooreck.RabbitMQ.Tests.Fixtures;

public sealed record QueueNames(string Exchange, string ShippingQueue)
{
    public static QueueNames Shared { get; } = new("shop-shared", "shipping-shared");

    public static QueueNames Unique()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new QueueNames($"shop-{suffix}", $"shipping-{suffix}");
    }
}

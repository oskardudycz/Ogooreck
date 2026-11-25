using Ogooreck.Sample.Kafka.Core;
namespace Ogooreck.Kafka.Tests.Fixtures;

public sealed record AppSettings(
    string Topic,
    string ShippingGroupId,
    bool Shipping = true,
    bool BlockHandler = false,
    string? BillingGroupId = null,
    TimeSpan? Deadline = null)
{
    public static AppSettings Shared { get; } = new(SharedTopic, "shipping");

    public const string SharedTopic = "shop-events-shared";

    public static AppSettings Own(string topic) => new(topic, $"shipping-{Guid.NewGuid()}");

    public static AppSettings WithoutShipping() => Shared with { Topic = KafkaOptions.DefaultTopic, Shipping = false };
}

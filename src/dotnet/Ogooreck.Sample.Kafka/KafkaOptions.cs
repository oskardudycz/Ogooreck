namespace Ogooreck.Sample.Kafka;

public record KafkaOptions(
    string BootstrapServers,
    string Topic,
    string ShippingGroupId,
    string BillingGroupId,
    IReadOnlyDictionary<string, string> Consumer)
{
    public const string DefaultTopic = "shop-events";
}

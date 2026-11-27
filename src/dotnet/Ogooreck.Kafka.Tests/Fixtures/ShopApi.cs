using Ogooreck.API;

namespace Ogooreck.Kafka.Tests.Fixtures;

#region KafkaShopApi

public class ShopApi(KafkaBroker broker): ApiSpecification<Program>(
    factory => factory.WithWebHostBuilder(builder => builder
        .UseSetting("Kafka:BootstrapServers", broker.BootstrapServers)
        .UseSetting("Kafka:Topic", Topic)
        .UseSetting("Kafka:ShippingGroupId", "shop-shipping")
        .UseSetting("Kafka:Consumer:fetch.wait.max.ms", "10")),
    spec => spec.Use(Messaging.Kafka))
{
    public const string Topic = "shop";
}

#endregion

using Ogooreck.API;

namespace Ogooreck.RabbitMQ.Tests.Fixtures;

#region RabbitMQShopApi

public class ShopApi(RabbitMQBroker broker): ApiSpecification<Program>(
    factory => factory.WithWebHostBuilder(builder => builder
        .UseSetting("RabbitMQ:ConnectionString", broker.ConnectionString)
        .UseSetting("RabbitMQ:Exchange", "shop")
        .UseSetting("RabbitMQ:ShippingQueue", "shop-shipping")),
    spec => spec.Use(Messaging.RabbitMQ));

#endregion

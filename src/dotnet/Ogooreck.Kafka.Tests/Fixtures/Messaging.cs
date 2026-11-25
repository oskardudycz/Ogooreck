using Ogooreck.Kafka;
using Ogooreck.Sample.Kafka.Events;

namespace Ogooreck.Kafka.Tests.Fixtures;

public static class Messaging
{
    public static readonly KafkaPlugin<string, string> Kafka = KafkaPlugin.Decorating()
        .MessageTypes(typeof(OrderPlaced), typeof(CartClosed), typeof(ShipOrder), typeof(OrderPaid),
            typeof(StockChecked));
}

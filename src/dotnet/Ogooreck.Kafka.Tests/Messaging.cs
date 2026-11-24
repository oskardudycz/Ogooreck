using Ogooreck.Kafka;
using Ogooreck.Sample.Kafka.Events;

namespace Ogooreck.Kafka.Tests;

public static class Messaging
{
    public static readonly KafkaPlug<string, string> Kafka = KafkaPlug.Decorating()
        .MessageTypes(typeof(OrderPlaced), typeof(CartClosed), typeof(ShipOrder), typeof(OrderPaid),
            typeof(StockChecked));
}

using Ogooreck.RabbitMQ;
using Ogooreck.Sample.RabbitMQ.Events;

namespace Ogooreck.RabbitMQ.Tests;

public static class Messaging
{
    public static readonly RabbitMQPlug RabbitMQ =
        RabbitMQPlug.Decorating().MessageTypes(typeof(OrderPlaced), typeof(ShipOrder));
}

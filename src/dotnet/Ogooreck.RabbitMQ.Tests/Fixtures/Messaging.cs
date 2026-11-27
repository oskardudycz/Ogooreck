using Ogooreck.RabbitMQ;
using Ogooreck.Sample.RabbitMQ.Events;

namespace Ogooreck.RabbitMQ.Tests.Fixtures;

#region RabbitMQPlugin

public static class Messaging
{
    public static readonly RabbitMQPlugin RabbitMQ = RabbitMQPlugin.Decorating()
        .MessageTypes(typeof(OrderPlaced), typeof(ShipOrder));
}

#endregion

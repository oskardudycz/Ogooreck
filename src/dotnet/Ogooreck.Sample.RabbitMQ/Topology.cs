using RabbitMQ.Client;

namespace Ogooreck.Sample.RabbitMQ;

public static class Topology
{
    public static async Task Declare(IConnection connection, RabbitMQOptions options)
    {
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true);
        await channel.QueueDeclareAsync(options.ShippingQueue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(options.ShippingQueue, options.Exchange, "OrderPlaced");
    }
}

namespace Ogooreck.Sample.RabbitMQ;

public record RabbitMQOptions(
    string ConnectionString,
    string Exchange,
    string ShippingQueue,
    ushort DispatchConcurrency,
    bool ShipOrders,
    bool AutoAck,
    bool ConsumeShippingOutsideTheContainer)
{
    public static RabbitMQOptions From(IConfiguration configuration) =>
        new(
            configuration["RabbitMQ:ConnectionString"] ?? "amqp://guest:guest@localhost:5672",
            configuration["RabbitMQ:Exchange"] ?? "shop",
            configuration["RabbitMQ:ShippingQueue"] ?? "shipping",
            configuration.GetValue("RabbitMQ:DispatchConcurrency", (ushort)1),
            configuration.GetValue("RabbitMQ:ShipOrders", true),
            configuration.GetValue("RabbitMQ:AutoAck", false),
            configuration.GetValue("RabbitMQ:ConsumeShippingOutsideTheContainer", false));
}

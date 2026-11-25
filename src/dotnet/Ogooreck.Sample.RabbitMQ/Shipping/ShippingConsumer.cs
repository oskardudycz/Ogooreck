using System.Text;
using System.Text.Json;
using Ogooreck.Sample.RabbitMQ.Core;
using Ogooreck.Sample.RabbitMQ.Events;
using Ogooreck.Sample.RabbitMQ.Orders;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Ogooreck.Sample.RabbitMQ.Shipping;

public sealed class ShippingConsumer(
    IConnection connection,
    EventPublisher events,
    OrderStatuses orders,
    ShippingFaults faults,
    RabbitMQOptions options): IHostedService
{
    private readonly CancellationTokenSource stopping = new();
    private IChannel? channel;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += Handle;
        await channel.BasicConsumeAsync(options.ShippingQueue, options.AutoAck, consumer, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await stopping.CancelAsync();
        await channel!.DisposeAsync();
    }

    private async Task Handle(object sender, BasicDeliverEventArgs delivery)
    {
        orders.Handling();
        var headers = delivery.BasicProperties.Headers;
        if (headers?["messageType"] is not byte[] type || Encoding.UTF8.GetString(type) != typeof(OrderPlaced).FullName)
        {
            await Settle(() => channel!.BasicRejectAsync(delivery.DeliveryTag, false));
            return;
        }

        var messageId = Text(headers, "messageId");
        var placed = JsonSerializer.Deserialize<OrderPlaced>(delivery.Body.Span, JsonSerializerOptions.Web)!;
        if (faults.Blocks(placed.OrderId))
        {
            faults.BlockedTheHandler();
            await Task.Delay(Timeout.Infinite, stopping.Token);
        }

        await faults.ShippingOf(placed.OrderId).WaitAsync(stopping.Token);
        if (faults.FirstTryOf(placed.OrderId) is { } firstTry)
        {
            await Settle(() => firstTry == FirstTry.Nack
                ? channel!.BasicNackAsync(delivery.DeliveryTag, false, true)
                : channel!.BasicRejectAsync(delivery.DeliveryTag, true));
            return;
        }

        await events.Publish(new ShipOrder(placed.OrderId), Text(headers, "traceparent"),
            Text(headers, "correlationId"), messageId);
        orders.Ship(placed.OrderId);
        await Settle(() => channel!.BasicAckAsync(delivery.DeliveryTag, false));
    }

    private async ValueTask Settle(Func<ValueTask> settle)
    {
        if (!options.AutoAck)
            await settle();
    }

    private static string Text(IDictionary<string, object?> headers, string name) =>
        Encoding.UTF8.GetString((byte[])headers[name]!);
}

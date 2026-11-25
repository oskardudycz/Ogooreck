using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Ogooreck.Sample.Kafka.Core;
using Ogooreck.Sample.Kafka.Events;

namespace Ogooreck.Sample.Kafka.Shipping;

public sealed class ShippingService(
    IConsumer<string, string> consumer,
    EventPublisher events,
    ShippedOrders shippedOrders,
    KafkaOptions options,
    ShippingFaults faults,
    IConfiguration configuration): BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        consumer.Subscribe(options.Topic);
        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => Run(stoppingToken), stoppingToken);

    private async Task Run(CancellationToken stoppingToken)
    {
        try
        {
            var blocked = configuration.GetValue<bool>("Shipping:BlockHandler");
            while (true)
            {
                var result = consumer.Consume(stoppingToken);
                if (HeaderOf(result, "messageType") != typeof(OrderPlaced).FullName)
                    continue;

                if (blocked)
                {
                    faults.BlockedTheHandler();
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }

                var placed = JsonSerializer.Deserialize<OrderPlaced>(result.Message.Value, JsonSerializerOptions.Web)!;
                await faults.ShippingOf(placed.CartId).WaitAsync(stoppingToken);
                await Handle(result, placed, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task Handle(ConsumeResult<string, string> result, OrderPlaced placed,
        CancellationToken stoppingToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (faults.Fails(placed.CartId, attempt))
                    throw new InvalidOperationException("The first try fails.");
                await Ship(result, placed);
                return;
            }
            catch (InvalidOperationException) when (attempt < 3)
            {
            }
        }
    }

    private async Task Ship(ConsumeResult<string, string> result, OrderPlaced placed)
    {
        var messageId = HeaderOf(result, "messageId")!;
        await events.Publish(result.Message.Key, new ShipOrder(placed.CartId), HeaderOf(result, "traceparent")!,
            HeaderOf(result, "correlationId"), messageId);
        shippedOrders.MarkShipped(placed.CartId);
    }

    private static string? HeaderOf(ConsumeResult<string, string> result, string name) =>
        result.Message.Headers.TryGetLastBytes(name, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
}

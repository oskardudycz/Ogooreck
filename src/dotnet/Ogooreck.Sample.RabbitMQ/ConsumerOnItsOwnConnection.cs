using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Ogooreck.Sample.RabbitMQ;

public sealed class ConsumerOnItsOwnConnection(string connectionString, string queue, int priority): IHostedService
{
    private IConnection? connection;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        connection = await new ConnectionFactory { Uri = new Uri(connectionString) }
            .CreateConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) =>
            channel.BasicAckAsync(delivery.DeliveryTag, false).AsTask();
        await channel.BasicConsumeAsync(queue, false, string.Empty, false, false,
            new Dictionary<string, object?> { ["x-priority"] = priority }, consumer, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Interlocked.Exchange(ref connection, null)?.CloseAsync(cancellationToken) ?? Task.CompletedTask;
}

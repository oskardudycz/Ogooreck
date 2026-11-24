using System.Diagnostics;
using System.Text.Json;
using RabbitMQ.Client;

namespace Ogooreck.Sample.RabbitMQ;

public sealed class EventPublisher(IConnection connection, RabbitMQOptions options)
{
    public async Task<string> Publish(object message, string traceparent, string? correlationId = null,
        string? causationId = null, bool toAMissingExchange = false)
    {
        var messageId = Guid.NewGuid().ToString();
        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = traceparent,
            ["messageType"] = message.GetType().FullName,
            ["messageId"] = messageId,
            ["correlationId"] = correlationId ?? messageId
        };
        if (causationId is not null)
            headers["causationId"] = causationId;

        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true));
        var exchange = new CachedString(toAMissingExchange ? $"{options.Exchange}-missing" : options.Exchange);
        await channel.BasicPublishAsync(exchange, new CachedString(message.GetType().Name), false,
            new BasicProperties { Headers = headers },
            JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), JsonSerializerOptions.Web));
        return messageId;
    }

    public static string CurrentTraceparent() =>
        Activity.Current?.Id ?? throw new InvalidOperationException("The request has no trace context.");
}

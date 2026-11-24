using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;

namespace Ogooreck.Sample.Kafka;

public sealed class EventPublisher(IProducer<string, string> producer, KafkaOptions options)
{
    public async Task<string> Publish(string key, object message, string traceparent, string? correlationId = null,
        string? causationId = null)
    {
        var messageId = Guid.NewGuid().ToString();
        var headers = new Headers
        {
            { "traceparent", Encoding.UTF8.GetBytes(traceparent) },
            { "messageType", Encoding.UTF8.GetBytes(message.GetType().FullName!) },
            { "messageId", Encoding.UTF8.GetBytes(messageId) },
            { "correlationId", Encoding.UTF8.GetBytes(correlationId ?? messageId) }
        };
        if (causationId is not null)
            headers.Add("causationId", Encoding.UTF8.GetBytes(causationId));

        await producer.ProduceAsync(options.Topic,
            new Message<string, string>
            {
                Key = key,
                Value = JsonSerializer.Serialize(message, message.GetType(), JsonSerializerOptions.Web),
                Headers = headers
            });
        return messageId;
    }

    public static string CurrentTraceparent() =>
        Activity.Current?.Id ?? throw new InvalidOperationException("The request has no trace context.");
}

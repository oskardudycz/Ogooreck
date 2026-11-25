using System.Text;
using System.Text.Json;
using Ogooreck.API;

namespace Ogooreck.Kafka;

internal sealed record KafkaReading<TKey, TValue>(
    MessageReader Reader,
    Func<KafkaMessage<TKey, TValue>, Observed>? Custom)
{
    public static KafkaReading<TKey, TValue> Default { get; } = new(new MessageReader(), null);

    public KafkaReading<TKey, TValue> With(Func<MessageReader, MessageReader> change) =>
        this with { Reader = change(Reader) };

    public Observed Read(KafkaMessage<TKey, TValue> recorded)
    {
        if (Custom is not null)
            return Custom(recorded);

        var headers = recorded.Message.Headers;
        return Reader.Read(
            name => headers is not null && headers.TryGetLastBytes(name, out var bytes)
                ? Encoding.UTF8.GetString(bytes)
                : null,
            Body(recorded.Message.Value));
    }

    private static ReadOnlyMemory<byte> Body(TValue value) =>
        value switch
        {
            string text => Encoding.UTF8.GetBytes(text),
            byte[] bytes => bytes,
            _ => throw new InvalidOperationException(
                $"The Kafka plugin reads message values of type string or byte[], but this one is {typeof(TValue).Name}. Give the plugin a Read(...) function that turns it into an Observed.")
        };
}

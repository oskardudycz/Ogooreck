using System.Collections.Concurrent;
using Ogooreck.API;

namespace Ogooreck.Kafka;

internal sealed class KafkaRegistry<TKey, TValue>
{
    public Recorded<KafkaMessage<TKey, TValue>> Published { get; } = new();

    public ConcurrentDictionary<TrackedConsumer<TKey, TValue>, byte> Consumers { get; } = new();
}

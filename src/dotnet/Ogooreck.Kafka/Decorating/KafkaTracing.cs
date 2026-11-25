using System.Diagnostics;

namespace Ogooreck.Kafka;

internal static class KafkaTracing
{
    public static readonly ActivitySource Source = new("Ogooreck.Kafka");
}

using System.Diagnostics;

namespace Ogooreck.Kafka;

internal static class KafkaTracing
{
    public static readonly ActivitySource Source = new("Ogooreck.Kafka");

    static KafkaTracing() => ActivitySource.AddActivityListener(new ActivityListener
    {
        ShouldListenTo = source => source == Source,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
    });
}

using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;

namespace Ogooreck.Kafka.Tests;

public sealed class ExtraProducerPlug<TValue>(KafkaBroker broker): IPlug
{
    public void Plug(PlugRegistration registration) =>
        registration.Services(services =>
            services.AddSingleton<IProducer<string, TValue>>(_ =>
                new ProducerBuilder<string, TValue>(new ProducerConfig { BootstrapServers = broker.BootstrapServers }).Build()));
}

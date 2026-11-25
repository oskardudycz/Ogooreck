using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Kafka.Tests.Fixtures;

namespace Ogooreck.Kafka.Tests.Decorating;

public sealed class ExtraProducerPlugin<TValue>(KafkaBroker broker): ISpecificationPlugin
{
    public void Register(SpecificationPluginRegistration registration) =>
        registration.Services(services =>
            services.AddSingleton<IProducer<string, TValue>>(_ =>
                new ProducerBuilder<string, TValue>(new ProducerConfig { BootstrapServers = broker.BootstrapServers }).Build()));
}

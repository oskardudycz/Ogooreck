using Confluent.Kafka;

namespace Ogooreck.Sample.Kafka.Core;

public static class Configuration
{
    public static IServiceCollection AddKafka(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new KafkaOptions(
            configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            configuration["Kafka:Topic"] ?? KafkaOptions.DefaultTopic,
            configuration["Kafka:ShippingGroupId"] ?? "shipping",
            configuration["Kafka:BillingGroupId"] ?? "billing",
            configuration.GetSection("Kafka:Consumer").Get<Dictionary<string, string>>() ?? []);
        return services
            .AddSingleton(options)
            .AddSingleton<IProducer<string, string>>(_ =>
                new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = options.BootstrapServers }).Build())
            .AddSingleton<IConsumer<string, string>>(_ =>
                new ConsumerBuilder<string, string>(new ConsumerConfig(new Dictionary<string, string>(options.Consumer))
                {
                    BootstrapServers = options.BootstrapServers,
                    GroupId = options.ShippingGroupId
                }).Build())
            .AddSingleton<EventPublisher>();
    }
}

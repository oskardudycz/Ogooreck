using Confluent.Kafka;
using Ogooreck.Sample.Kafka.Core;

namespace Ogooreck.Sample.Kafka.Billing;

public sealed class BillingService(KafkaOptions options): BackgroundService
{
    private IConsumer<string, string>? consumer;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        consumer = new ConsumerBuilder<string, string>(new ConsumerConfig(new Dictionary<string, string>(options.Consumer))
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.BillingGroupId,
            ClientId = "billing-worker"
        }).Build();
        consumer.Subscribe(options.Topic);
        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() =>
        {
            try
            {
                while (true)
                    consumer!.Consume(stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                consumer!.Close();
            }
        }, stoppingToken);
}

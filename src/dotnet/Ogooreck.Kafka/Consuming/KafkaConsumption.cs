using Confluent.Kafka;
using Ogooreck.API;

namespace Ogooreck.Kafka;

internal sealed class KafkaConsumption(Func<Task<string>> bootstrapServers, string[] topics): IAsyncDisposable
{
    private readonly CancellationTokenSource stopping = new();
    private readonly Lock gate = new();
    private Task? started;
    private Task consuming = Task.CompletedTask;

    public Recorded<KafkaMessage<string, string>> Published { get; } = new();

    public Task Start(TimeSpan deadline)
    {
        lock (gate)
            return started ??= StartConsumer(deadline);
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        await consuming;
    }

    private async Task StartConsumer(TimeSpan deadline)
    {
        var servers = await bootstrapServers();
        var assigned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = servers,
            GroupId = $"ogooreck-{Guid.NewGuid()}",
            EnableAutoCommit = false
        })
            .SetPartitionsAssignedHandler((assigning, partitions) =>
            {
                var ends = partitions.Select(partition =>
                    new TopicPartitionOffset(partition, assigning.QueryWatermarkOffsets(partition, deadline).High)).ToList();
                assigned.TrySetResult();
                return ends;
            })
            .Build();
        consumer.Subscribe(topics);
        consuming = Task.Run(() => Consume(consumer));
        await assigned.Task.WaitAsync(deadline);
    }

    private void Consume(IConsumer<string, string> consumer)
    {
        using (consumer)
        {
            try
            {
                while (true)
                {
                    var result = consumer.Consume(stopping.Token);
                    Published.Add(new KafkaMessage<string, string>(result.Topic, result.Message));
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                consumer.Close();
            }
        }
    }
}

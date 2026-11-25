using Ogooreck.API;
using Ogooreck.Assertions;
using RabbitMQ.Client;

namespace Ogooreck.RabbitMQ;

internal sealed class RabbitMQCatchUp(HostState host, SpecificationPluginRegistration registration)
{
    private const double DiagnosisShareOfDeadline = 0.9;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    public async Task Run(CancellationToken ct)
    {
        if (host.Connection is not { } connection)
            return;

        var consumed = host.ConsumedQueues;
        var failures = new List<string>();
        foreach (var queue in consumed)
        {
            var consumers = await ConsumersOf(connection, queue, ct);
            var decorated = host.DecoratedConsumersOn(queue);
            if (consumers > decorated)
                failures.Add(
                    $"Queue {queue} has {consumers} consumers, but Ogooreck decorates {decorated}, so another consumer can take the probe. Resolve the consumer from the app's service provider, so that the plugin wraps it, or configure the RabbitMQ plugin with .CatchingUpWith(...) and your own wait.");
        }

        if (failures.Count > 0)
            throw new AssertionException(string.Join(Environment.NewLine, failures));

        var probes = await PublishProbes(connection, consumed, ct);
        await WaitFor(probes, ct);
    }

    private static async Task<int> ConsumersOf(IConnection connection, string queue, CancellationToken ct)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        return (int)(await channel.QueueDeclarePassiveAsync(queue, ct)).ConsumerCount;
    }

    private static async Task<Dictionary<string, string>> PublishProbes(IConnection connection,
        IEnumerable<string> queues, CancellationToken ct)
    {
        var probes = new Dictionary<string, string>();
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        foreach (var queue in queues)
        {
            var probe = Guid.NewGuid().ToString();
            var properties = new BasicProperties
            {
                Headers = new Dictionary<string, object?> { [ConsumerProxy.ProbeHeader] = probe }
            };
            await channel.BasicPublishAsync(string.Empty, queue, false, properties, ReadOnlyMemory<byte>.Empty, ct);
            probes[probe] = queue;
        }

        return probes;
    }

    private async Task WaitFor(Dictionary<string, string> probes, CancellationToken ct)
    {
        var limit = registration.Deadline * DiagnosisShareOfDeadline;
        var started = registration.Clock.GetTimestamp();
        while (true)
        {
            var waiting = probes.Where(probe => !host.ProbeCounts(probe.Key)).Select(probe => probe.Value).ToList();
            if (waiting.Count == 0)
                return;
            if (registration.Clock.GetElapsedTime(started) >= limit)
                throw new AssertionException(
                    $"RabbitMQ did not catch up within {limit}. Queues not caught up: {string.Join(", ", waiting)}. Handlers running: {host.Running}. Requeues pending: {host.PendingRequeues}.");
            await Task.Delay(PollInterval, registration.Clock, ct);
        }
    }
}

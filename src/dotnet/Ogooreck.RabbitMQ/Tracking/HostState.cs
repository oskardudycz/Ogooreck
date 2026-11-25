using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Ogooreck.API;
using RabbitMQ.Client;

namespace Ogooreck.RabbitMQ;

internal sealed class HostState(Recorded<RabbitMQMessage> published)
{
    private readonly Lock gate = new();
    private readonly ConcurrentDictionary<string, ProbeArrival> probes = new();
    private readonly Dictionary<string, int> decoratedConsumers = [];
    private int running;
    private readonly List<MessageKey> pendingRequeues = [];

    public IConnection? Connection { get; private set; }

    public int Running
    {
        get
        {
            lock (gate)
                return running;
        }
    }

    public int PendingRequeues
    {
        get
        {
            lock (gate)
                return pendingRequeues.Count;
        }
    }

    public IReadOnlyCollection<string> ConsumedQueues
    {
        get
        {
            lock (gate)
                return [.. decoratedConsumers.Keys];
        }
    }

    public IConnection Decorate(IConnection inner)
    {
        lock (gate)
            Connection = inner;
        return ConnectionProxy.Create(inner, this);
    }

    public IChannel Decorate(IChannel inner)
    {
        var recording = CallRecorder.Wrap(inner, nameof(IChannel.BasicPublishAsync), Published);
        return ChannelProxy.Create(recording, new ChannelState(inner), this);
    }

    public int DecoratedConsumersOn(string queue)
    {
        lock (gate)
            return decoratedConsumers.GetValueOrDefault(queue);
    }

    public void Consuming(string queue)
    {
        lock (gate)
            decoratedConsumers[queue] = decoratedConsumers.GetValueOrDefault(queue) + 1;
    }

    public void HandlerStarted(MessageKey message, bool redelivered)
    {
        lock (gate)
        {
            running++;
            if (redelivered)
                pendingRequeues.Remove(message);
        }
    }

    public void HandlerDone()
    {
        lock (gate)
            running--;
    }

    public void RequeuePending(IEnumerable<MessageKey> messages)
    {
        lock (gate)
            pendingRequeues.AddRange(messages);
    }

    public void RequeueFailed(IEnumerable<MessageKey> messages)
    {
        lock (gate)
            foreach (var message in messages)
                pendingRequeues.Remove(message);
    }

    public void ProbeArrived(string probe, ProbeArrival arrival) => probes[probe] = arrival;

    public bool ProbeCounts(string probe) =>
        probes.TryGetValue(probe, out var arrival)
        && arrival.Channel.EveryTagBelowIsDone(arrival.DeliveryTag)
        && Running == 0
        && PendingRequeues == 0;

    public static string? HeaderOf(IReadOnlyBasicProperties properties, string name) =>
        properties.Headers is { } headers && headers.TryGetValue(name, out var value)
            ? value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    private void Published(object?[] arguments) =>
        published.Add(new RabbitMQMessage(NameOf(arguments[0]), NameOf(arguments[1]),
            (IReadOnlyBasicProperties)arguments[3]!, ((ReadOnlyMemory<byte>)arguments[4]!).ToArray()));

    private static string NameOf(object? name) => name is CachedString cached ? cached.Value : (string)name!;
}

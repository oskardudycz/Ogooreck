using RabbitMQ.Client;

namespace Ogooreck.RabbitMQ;

internal sealed class ChannelState(IChannel inner)
{
    private readonly Lock gate = new();
    private readonly HashSet<ulong> doneAhead = [];
    private readonly SortedDictionary<ulong, MessageKey> unsettled = [];
    private ulong doneThrough;

    public IChannel Inner { get; } = inner;

    public void Done(ulong deliveryTag)
    {
        lock (gate)
        {
            doneAhead.Add(deliveryTag);
            while (doneAhead.Remove(doneThrough + 1))
                doneThrough++;
        }
    }

    public bool EveryTagBelowIsDone(ulong deliveryTag)
    {
        lock (gate)
            return doneThrough >= deliveryTag - 1;
    }

    public void Unsettled(ulong deliveryTag, MessageKey message)
    {
        lock (gate)
            unsettled[deliveryTag] = message;
    }

    public IReadOnlyList<MessageKey> Settle(ulong deliveryTag, bool multiple)
    {
        lock (gate)
        {
            var tags = unsettled.Keys.Where(tag => multiple ? tag <= deliveryTag : tag == deliveryTag).ToList();
            var messages = tags.Select(tag => unsettled[tag]).ToList();
            foreach (var tag in tags)
                unsettled.Remove(tag);
            return messages;
        }
    }
}

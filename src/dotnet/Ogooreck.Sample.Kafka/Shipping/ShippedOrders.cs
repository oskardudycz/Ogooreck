using System.Collections.Concurrent;

namespace Ogooreck.Sample.Kafka.Shipping;

public sealed class ShippedOrders
{
    private readonly ConcurrentDictionary<Guid, bool> shipped = new();

    public void MarkShipped(Guid cartId) => shipped[cartId] = true;

    public bool IsShipped(Guid cartId) => shipped.ContainsKey(cartId);
}

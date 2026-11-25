using System.Collections.Concurrent;

namespace Ogooreck.Sample.Wolverine.Orders.Holding;

public class OrderHolds
{
    private const int HoldsAllowed = 2;

    private readonly ConcurrentDictionary<Guid, int> holds = new();

    public bool TryHold(Guid orderId) => holds.AddOrUpdate(orderId, 1, (_, count) => count + 1) <= HoldsAllowed;
}

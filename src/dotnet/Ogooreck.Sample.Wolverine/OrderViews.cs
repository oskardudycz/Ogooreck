using System.Collections.Concurrent;

namespace Ogooreck.Sample.Wolverine;

public class OrderViews
{
    private const int ReadsUntilUpToDate = 3;

    private readonly ConcurrentDictionary<Guid, bool> placed = new();
    private readonly ConcurrentDictionary<Guid, int> reads = new();

    public void Placed(Guid orderId) => placed[orderId] = true;

    public OrderView Read(Guid orderId)
    {
        var read = reads.AddOrUpdate(orderId, 1, (_, count) => count + 1);
        return new OrderView(orderId, placed.ContainsKey(orderId) && read >= ReadsUntilUpToDate ? "Placed" : "Pending");
    }
}

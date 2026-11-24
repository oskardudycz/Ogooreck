using System.Collections.Concurrent;

namespace Ogooreck.Sample.RabbitMQ;

public sealed class Orders
{
    private readonly ConcurrentDictionary<Guid, string> statuses = new();
    private int handled;

    public int Handled => handled;

    public int ShippedCount => statuses.Count(order => order.Value == "Shipped");

    public void Placed(Guid orderId) => statuses[orderId] = "Placed";

    public void Handling() => Interlocked.Increment(ref handled);

    public void Ship(Guid orderId) => statuses[orderId] = "Shipped";

    public string? StatusOf(Guid orderId) => statuses.GetValueOrDefault(orderId);
}

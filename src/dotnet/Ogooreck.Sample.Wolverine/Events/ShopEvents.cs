namespace Ogooreck.Sample.Wolverine.Events;

public record ProductAdded(Guid CartId, string Product);

public record CartClosed(Guid CartId);

public record CartExpired(Guid CartId);

public record LateArrived(Guid CartId);

public record OrderPlaced(Guid CartId)
{
    public decimal Total { get; init; }
    public IReadOnlyList<string> Products { get; init; } = [];
}

public record OrderViewed(Guid OrderId);

public record OrderPaid(Guid OrderId);

public record ShipOrder(Guid CartId);

public record StockChecked(string Product);

public record Heartbeat(Guid Id);

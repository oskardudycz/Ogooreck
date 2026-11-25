namespace Ogooreck.Sample.Kafka.Events;

public record OrderPlaced(Guid CartId);

public record CartClosed(Guid CartId);

public record ShipOrder(Guid CartId);

public record OrderPaid(Guid OrderId);

public record StockChecked(string Product);

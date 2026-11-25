namespace Ogooreck.Sample.RabbitMQ.Events;

public record OrderPlaced(Guid OrderId);

public record ShipOrder(Guid OrderId);

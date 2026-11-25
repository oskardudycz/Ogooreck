namespace Ogooreck.Sample.Marten.Events;

public record ProductAdded(Guid CartId, string Product);

public record OrderPlaced(Guid CartId);

public record OrderShipped(Guid CartId);

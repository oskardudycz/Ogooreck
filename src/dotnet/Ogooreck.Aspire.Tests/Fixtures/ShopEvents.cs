namespace Ogooreck.Aspire.Tests.Fixtures;

public sealed record OrderPlaced(Guid CartId, double Total);

public sealed record ShipOrder(Guid CartId);

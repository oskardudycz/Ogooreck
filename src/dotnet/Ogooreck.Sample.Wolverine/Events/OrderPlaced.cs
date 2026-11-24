namespace Ogooreck.Sample.Wolverine.Events;

public record OrderPlaced(Guid CartId)
{
    public decimal Total { get; init; }

    public IReadOnlyList<string> Products { get; init; } = [];
}

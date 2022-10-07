namespace Ogooreck.Sample.BusinessLogic.Aggregates.StateBased.Core;

public abstract class Aggregate: Aggregate<Guid>
{
}

public abstract class Aggregate<T> where T : notnull
{
    public T Id { get; protected set; } = default!;

    public int Version { get; protected set; }
}

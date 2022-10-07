namespace Ogooreck.Sample.BusinessLogic.Aggregates.EventSourced.Core;

public abstract class Aggregate: Aggregate<Guid>
{
}

public abstract class Aggregate<T> where T : notnull
{
    public T Id { get; protected set; } = default!;

    public int Version { get; protected set; }

    [NonSerialized] private readonly Queue<object> uncommittedEvents = new();

    public virtual void When(object @event) { }

    public object[] DequeueUncommittedEvents()
    {
        var dequeuedEvents = uncommittedEvents.ToArray();

        uncommittedEvents.Clear();

        return dequeuedEvents;
    }

    protected void Enqueue(object @event)
    {
        uncommittedEvents.Enqueue(@event);
    }
}

using System.Threading.Channels;

namespace Ogooreck.API;

/// <summary>
/// A pipe from a messaging tool or event store to Ogooreck. User code calls <see cref="Add"/>; Ogooreck reads the items.
/// Register it with <c>Recording(...)</c>.
/// </summary>
/// <example>
/// <code>
/// public static readonly Recorded&lt;Envelope&gt; PublishedMessages = new();
/// </code>
/// </example>
public sealed class Recorded<T>
{
    private readonly Channel<T> channel = Channel.CreateUnbounded<T>();
    private readonly List<T> items = [];
    private readonly Lock gate = new();

    /// <summary>Adds an item. It never blocks and never throws.</summary>
    /// <example>
    /// <code>
    /// Messaging.Wolverine.Published.Add(envelope);
    /// </code>
    /// </example>
    public void Add(T item) => channel.Writer.TryWrite(item);

    internal IReadOnlyList<T> Items()
    {
        lock (gate)
        {
            Drain();
            return [.. items];
        }
    }

    internal int Count()
    {
        lock (gate)
        {
            Drain();
            return items.Count;
        }
    }

    private void Drain()
    {
        while (channel.Reader.TryRead(out var item))
            items.Add(item);
    }
}

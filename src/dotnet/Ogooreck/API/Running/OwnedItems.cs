namespace Ogooreck.API;

internal sealed class OwnedItems<T>
{
    private readonly List<(object? Owner, string? Name, T Item)> entries = [];

    public T[] Items => [.. entries.Select(entry => entry.Item)];

    public IEnumerable<(string? Name, T Item)> Named => entries.Select(entry => (entry.Name, entry.Item));

    public void Add(T item) => entries.Add((null, null, item));

    public void CopyTo(OwnedItems<T> other) => other.entries.AddRange(entries);

    public Slot Reserve(object owner, string name)
    {
        var position = entries.FindIndex(entry => ReferenceEquals(entry.Owner, owner));
        entries.RemoveAll(entry => ReferenceEquals(entry.Owner, owner));
        return new Slot(this, owner, name, position);
    }

    public sealed class Slot(OwnedItems<T> items, object owner, string name, int position)
    {
        public void Add(T item)
        {
            if (position < 0)
                items.entries.Add((owner, name, item));
            else
                items.entries.Insert(position++, (owner, name, item));
        }
    }
}

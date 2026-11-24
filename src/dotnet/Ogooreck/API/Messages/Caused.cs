namespace Ogooreck.API;

/// <summary>
/// A message or event that <c>ExpectCausation</c> looks for, with the messages it should have caused.
/// Build it with <c>Sent</c>, <c>Published</c> or <c>Appended</c>.
/// </summary>
/// <example>
/// <code>
/// Published(new OrderPlaced(cartId), Sent(new ShipOrder(cartId)))
/// </code>
/// </example>
public sealed class Caused
{
    internal Caused(object? recorded, MessageWording wording, object message, IReadOnlyList<Caused> children)
    {
        Recorded = recorded;
        Wording = wording;
        Message = message;
        Children = children;
    }

    internal object? Recorded { get; }

    internal MessageWording Wording { get; }

    internal object Message { get; }

    internal IReadOnlyList<Caused> Children { get; }
}

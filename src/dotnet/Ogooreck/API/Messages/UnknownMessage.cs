namespace Ogooreck.API;

/// <summary>
/// What a <see cref="MessageReader"/> returns as the message when its type header is missing or matches none of the listed types.
/// An expectation fails on it only when it is one of the When step's messages; the messages of other steps and tests are ignored like any other message of theirs.
/// </summary>
/// <example>
/// <code>
/// new UnknownMessage("OrderShipped", [typeof(OrderPlaced)])
/// </code>
/// </example>
public sealed record UnknownMessage(string? TypeHeader, IReadOnlyList<Type> ListedTypes)
{
    /// <summary>The name of the header that held the type. The failure message names it.</summary>
    /// <example>
    /// <code>
    /// new UnknownMessage(null, []) { HeaderName = "message-type" }
    /// </code>
    /// </example>
    public string HeaderName { get; init; } = "messageType";
}

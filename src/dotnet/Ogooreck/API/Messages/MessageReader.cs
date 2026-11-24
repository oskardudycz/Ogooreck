using System.Text.Json;

namespace Ogooreck.API;

/// <summary>
/// Turns a broker message's headers and JSON body into an <see cref="Observed"/>, for apps that follow the recommended headers (see <see cref="MessageHeaders"/>).
/// It finds the message's .NET type from the type header, so list the types or give a lookup. Each option returns a new reader.
/// </summary>
/// <example>
/// <code>
/// var reader = new MessageReader().MessageTypes(typeof(OrderPlaced), typeof(ShipOrder));
/// Observed observed = reader.Read(name =&gt; message.Headers[name], message.Body);
/// </code>
/// </example>
public sealed class MessageReader
{
    private readonly MessageHeaders headers;
    private readonly JsonSerializerOptions json;
    private readonly Func<string, Type?> lookup;
    private readonly IReadOnlyList<Type> listed;

    /// <summary>Creates a reader with the recommended header names, web JSON options and no known types.</summary>
    /// <example>
    /// <code>
    /// new MessageReader()
    /// </code>
    /// </example>
    public MessageReader() : this(new MessageHeaders(), JsonSerializerOptions.Web, _ => null, []) { }

    private MessageReader(MessageHeaders headers, JsonSerializerOptions json, Func<string, Type?> lookup,
        IReadOnlyList<Type> listed)
    {
        this.headers = headers;
        this.json = json;
        this.lookup = lookup;
        this.listed = listed;
    }

    /// <summary>Lists the message types. The type header's value matches a listed type's <c>FullName</c> first, then its <c>Name</c>.</summary>
    /// <example>
    /// <code>
    /// new MessageReader().MessageTypes(typeof(OrderPlaced), typeof(ShipOrder))
    /// </code>
    /// </example>
    public MessageReader MessageTypes(params Type[] types) =>
        new(headers, json,
            name => types.FirstOrDefault(type => type.FullName == name) ?? types.FirstOrDefault(type => type.Name == name),
            types);

    /// <summary>Gives the reader's own lookup from the type header's value to a type, returning null for an unknown one.</summary>
    /// <example>
    /// <code>
    /// new MessageReader().MessageTypes(name =&gt; Type.GetType($"Shop.Events.{name}"))
    /// </code>
    /// </example>
    public MessageReader MessageTypes(Func<string, Type?> lookupType) => new(headers, json, lookupType, []);

    /// <summary>Sets the app's own header names.</summary>
    /// <example>
    /// <code>
    /// new MessageReader().Headers(new MessageHeaders { MessageType = "message-type" })
    /// </code>
    /// </example>
    public MessageReader Headers(MessageHeaders names) => new(names, json, lookup, listed);

    /// <summary>Sets the JSON options that read the body. The default is <see cref="JsonSerializerOptions.Web"/>.</summary>
    /// <example>
    /// <code>
    /// new MessageReader().Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
    /// </code>
    /// </example>
    public MessageReader Json(JsonSerializerOptions options) => new(headers, options, lookup, listed);

    /// <summary>
    /// Reads a message from a header lookup, with the headers as UTF-8 text, and the body. It takes the <c>traceparent</c> and the ids before the type.
    /// A message whose type header is missing or matches no type becomes an <see cref="UnknownMessage"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// reader.Read(name =&gt; headers.TryGetValue(name, out var value) ? value : null, body)
    /// </code>
    /// </example>
    public Observed Read(Func<string, string?> header, ReadOnlyMemory<byte> body)
    {
        var typeHeader = header(headers.MessageType);
        var type = typeHeader is null ? null : lookup(typeHeader);
        var message = type is null
            ? new UnknownMessage(typeHeader, listed) { HeaderName = headers.MessageType }
            : (object)JsonSerializer.Deserialize(body.Span, type, json)!;
        return new Observed(message, header(headers.Traceparent), header(headers.MessageId),
            header(headers.CorrelationId), header(headers.CausationId));
    }
}

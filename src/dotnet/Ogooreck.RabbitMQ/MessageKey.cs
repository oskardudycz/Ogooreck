using System.Security.Cryptography;

namespace Ogooreck.RabbitMQ;

internal sealed record MessageKey(string RoutingKey, string BodyHash)
{
    public static MessageKey Of(string routingKey, ReadOnlyMemory<byte> body) =>
        new(routingKey, Convert.ToHexString(SHA256.HashData(body.Span)));
}

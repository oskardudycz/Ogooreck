using Ogooreck.API;

namespace Ogooreck.RabbitMQ;

internal sealed record PlugSettings
{
    public MessageReader Reader { get; init; } = new();

    public Func<RabbitMQMessage, Observed>? Read { get; init; }

    public Func<CancellationToken, Task>? CatchUp { get; init; }
}

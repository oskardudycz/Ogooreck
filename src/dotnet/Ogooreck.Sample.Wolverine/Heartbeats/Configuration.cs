using Ogooreck.Sample.Wolverine.Events;
using Wolverine;

namespace Ogooreck.Sample.Wolverine.Heartbeats;

public static class Configuration
{
    public static IEndpointRouteBuilder MapHeartbeats(this IEndpointRouteBuilder app)
    {
        app.MapPost("/heartbeats/{id:guid}", async (Guid id, IMessageBus bus) =>
        {
            await bus.PublishAsync(new Heartbeat(id));
            return Results.NoContent();
        });

        return app;
    }
}

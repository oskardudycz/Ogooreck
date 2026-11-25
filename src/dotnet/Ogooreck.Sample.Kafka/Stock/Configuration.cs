using Ogooreck.Sample.Kafka.Core;
using Ogooreck.Sample.Kafka.Events;

namespace Ogooreck.Sample.Kafka.Stock;

public static class Configuration
{
    public static IEndpointRouteBuilder MapStock(this IEndpointRouteBuilder app)
    {
        app.MapPost("/stock/{product}/checks", async (string product, EventPublisher events) =>
        {
            await events.Publish(product, new StockChecked(product), EventPublisher.CurrentTraceparent());
            return Results.NoContent();
        });

        return app;
    }
}

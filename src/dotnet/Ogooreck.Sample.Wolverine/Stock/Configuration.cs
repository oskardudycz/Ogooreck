using Ogooreck.Sample.Wolverine.Events;
using Wolverine;

namespace Ogooreck.Sample.Wolverine.Stock;

public static class Configuration
{
    public static IEndpointRouteBuilder MapStock(this IEndpointRouteBuilder app)
    {
        app.MapPost("/stock/{product}/checks", async (string product, IMessageBus bus) =>
        {
            await bus.PublishAsync(new StockChecked(product));
            return Results.NoContent();
        });

        return app;
    }
}

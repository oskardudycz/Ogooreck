using Marten;
using Ogooreck.Sample.Marten.Events;
using static Ogooreck.Sample.Marten.Core.Tracing;

namespace Ogooreck.Sample.Marten.Orders;

public static class Configuration
{
    public static IEndpointRouteBuilder MapOrders(this IEndpointRouteBuilder app)
    {
        app.MapPost("/carts/{cartId:guid}/orders", async (Guid cartId, IDocumentSession session) =>
        {
            session.SetHeader("traceparent", CurrentTraceparent());
            session.Events.Append(cartId, new OrderPlaced(cartId));
            await session.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapPost("/carts/{cartId:guid}/shipment", async (Guid cartId, IDocumentStore store) =>
        {
            var traceparent = CurrentTraceparent();
            var correlationId = Guid.NewGuid().ToString();

            await using var placing = store.LightweightSession();
            placing.SetHeader("traceparent", traceparent);
            placing.CorrelationId = correlationId;
            var placed = placing.Events.Append(cartId, new OrderPlaced(cartId)).Events[0];
            await placing.SaveChangesAsync();

            await using var shipping = store.LightweightSession();
            shipping.SetHeader("traceparent", traceparent);
            shipping.CorrelationId = correlationId;
            shipping.CausationId = placed.Id.ToString();
            shipping.Events.Append(cartId, new OrderShipped(cartId));
            await shipping.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}

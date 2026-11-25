using Ogooreck.Sample.Kafka.Core;
using Ogooreck.Sample.Kafka.Events;
using Ogooreck.Sample.Kafka.Shipping;

namespace Ogooreck.Sample.Kafka.Carts;

public static class Configuration
{
    public static IEndpointRouteBuilder MapCarts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/carts/{cartId:guid}",
            (Guid cartId, ShippedOrders shippedOrders) => Results.Ok(new { cartId, shipped = shippedOrders.IsShipped(cartId) }));

        app.MapPost("/carts/{cartId:guid}/placed-orders", async (Guid cartId, EventPublisher events) =>
        {
            await events.Publish($"{cartId}", new OrderPlaced(cartId), EventPublisher.CurrentTraceparent());
            return Results.NoContent();
        });

        app.MapPost("/carts/{cartId:guid}/checkout", async (Guid cartId, EventPublisher events) =>
        {
            var traceparent = EventPublisher.CurrentTraceparent();
            await events.Publish($"{cartId}", new OrderPlaced(cartId), traceparent);
            await events.Publish($"{cartId}", new CartClosed(cartId), traceparent);
            await events.Publish($"{cartId}", new ShipOrder(cartId), traceparent);
            return Results.NoContent();
        });

        app.MapPost("/carts/{cartId:guid}/orders", async (Guid cartId, EventPublisher events, IHostApplicationLifetime lifetime) =>
        {
            var traceparent = EventPublisher.CurrentTraceparent();
            await events.Publish($"{cartId}", new OrderPlaced(cartId), traceparent);
            _ = HeldMessages.PublishWhenReleased(cartId, lifetime.ApplicationStopping,
                () => events.Publish($"{cartId}", new CartClosed(cartId), traceparent));
            return Results.NoContent();
        });

        app.MapPost("/carts/{cartId:guid}/shipping", async (Guid cartId, EventPublisher events, IHostApplicationLifetime lifetime) =>
        {
            var traceparent = EventPublisher.CurrentTraceparent();
            var placed = await events.Publish($"{cartId}", new OrderPlaced(cartId), traceparent);
            _ = HeldMessages.PublishWhenReleased(cartId, lifetime.ApplicationStopping,
                () => events.Publish($"{cartId}", new ShipOrder(cartId), traceparent, correlationId: placed, causationId: placed));
            return Results.NoContent();
        });

        return app;
    }
}

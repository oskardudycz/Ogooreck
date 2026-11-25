using Ogooreck.Sample.RabbitMQ.Core;
using Ogooreck.Sample.RabbitMQ.Events;
using Ogooreck.Sample.RabbitMQ.Shipping;

namespace Ogooreck.Sample.RabbitMQ.Orders;

public record PlaceOrder(Guid OrderId);

public static class Configuration
{
    public static IServiceCollection AddOrders(this IServiceCollection services) =>
        services.AddSingleton<OrderStatuses>();

    public static IEndpointRouteBuilder MapOrders(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", async (PlaceOrder order, EventPublisher events, OrderStatuses orders, ShippingFaults faults) =>
        {
            orders.Placed(order.OrderId);
            await events.Publish(new OrderPlaced(order.OrderId), EventPublisher.CurrentTraceparent(),
                toAMissingExchange: faults.PublishesToAMissingExchange(order.OrderId));
            return Results.Accepted();
        });

        app.MapGet("/orders/{orderId:guid}", (Guid orderId, OrderStatuses orders) =>
            orders.StatusOf(orderId) is { } status ? Results.Ok(new { status }) : Results.NotFound());

        return app;
    }
}

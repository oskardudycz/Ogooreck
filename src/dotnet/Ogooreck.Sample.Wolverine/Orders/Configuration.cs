using Ogooreck.Sample.Wolverine.Carts;
using Ogooreck.Sample.Wolverine.Events;
using Ogooreck.Sample.Wolverine.Orders.GettingOrder;
using Ogooreck.Sample.Wolverine.Orders.Holding;
using Wolverine;

namespace Ogooreck.Sample.Wolverine.Orders;

public static class Configuration
{
    public static IServiceCollection AddOrders(this IServiceCollection services) =>
        services.AddSingleton<OrderViews>().AddSingleton<OrderHolds>();

    public static IEndpointRouteBuilder MapOrders(this IEndpointRouteBuilder app)
    {
        app.MapPost("/carts/{cartId:guid}/orders", async (Guid cartId, CartStore carts, OrderViews orders, IMessageBus bus) =>
        {
            var products = carts.Products(cartId);
            if (products.Count == 0)
                return Results.Problem(title: "Cart is empty", statusCode: StatusCodes.Status400BadRequest);

            await bus.PublishAsync(CartStore.OrderFor(cartId, products));
            orders.Placed(cartId);
            return Results.Created($"/orders/{cartId}", null);
        });

        app.MapGet("/orders/{id}", async (Guid id, OrderViews orders, IMessageBus bus) =>
        {
            var view = orders.Read(id);
            await bus.PublishAsync(new OrderViewed(id));
            return Results.Ok(view);
        });

        app.MapPost("/orders/{id}/holds", (Guid id, OrderHolds holds) =>
            holds.TryHold(id)
                ? Results.Ok()
                : Results.Problem(title: "Hold limit reached", statusCode: StatusCodes.Status409Conflict));

        return app;
    }
}

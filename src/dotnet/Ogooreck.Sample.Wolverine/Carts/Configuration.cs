using Ogooreck.Sample.Wolverine.Carts.GettingCart;
using Ogooreck.Sample.Wolverine.Events;
using Wolverine;

namespace Ogooreck.Sample.Wolverine.Carts;

public record AddProductRequest(string Product);

public static class Configuration
{
    public static IServiceCollection AddCarts(this IServiceCollection services) =>
        services.AddSingleton<CartStore>().AddSingleton<CartViews>();

    public static IEndpointRouteBuilder MapCarts(this IEndpointRouteBuilder app)
    {
        app.MapPost("/carts/{cartId:guid}/products",
            async (Guid cartId, AddProductRequest request, CartStore carts, CartViews views, IMessageBus bus) =>
            {
                carts.Add(cartId, request.Product);
                views.Changing();
                await bus.PublishAsync(new ProductAdded(cartId, request.Product));
                return Results.NoContent();
            });

        app.MapGet("/carts/{cartId:guid}", (Guid cartId, CartViews views) => views.Get(cartId));

        app.MapPost("/carts/{cartId:guid}/checkout", async (Guid cartId, CartStore carts, IMessageBus bus) =>
        {
            await bus.PublishAsync(CartStore.OrderFor(cartId, carts.Products(cartId)));
            await bus.PublishAsync(new CartClosed(cartId));
            return Results.NoContent();
        });

        return app;
    }
}

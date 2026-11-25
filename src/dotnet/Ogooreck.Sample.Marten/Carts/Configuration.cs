using Marten;
using Ogooreck.Sample.Marten.Carts.GettingCart;
using Ogooreck.Sample.Marten.Events;
using static Ogooreck.Sample.Marten.Core.Tracing;

namespace Ogooreck.Sample.Marten.Carts;

public record AddProductRequest(string Product);

public static class Configuration
{
    public static IEndpointRouteBuilder MapCarts(this IEndpointRouteBuilder app)
    {
        app.MapPost("/carts/{cartId:guid}/products", async (Guid cartId, AddProductRequest request, IDocumentSession session) =>
        {
            session.SetHeader("traceparent", CurrentTraceparent());
            session.Events.Append(cartId, new ProductAdded(cartId, request.Product));
            await session.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapGet("/carts/{cartId:guid}", async (Guid cartId, IQuerySession session) =>
            await session.LoadAsync<CartView>(cartId) is { } cart ? Results.Ok(cart) : Results.NotFound());

        app.MapPost("/carts/{cartId:guid}/imports", async (Guid cartId, AddProductRequest request, IDocumentSession session) =>
        {
            session.Events.Append(cartId, new ProductAdded(cartId, request.Product));
            await session.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}

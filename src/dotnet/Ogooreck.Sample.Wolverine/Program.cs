using Ogooreck.Sample.Wolverine;
using Ogooreck.Sample.Wolverine.Events;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine(options =>
{
    options.Publish(rule => rule.MessagesFromNamespaceContaining<OrderPlaced>().ToLocalQueue("events"));
    options.LocalQueue("events").Sequential();
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Carts>();
builder.Services.AddSingleton<CartViews>();
builder.Services.AddSingleton<LateArrivals>();
builder.Services.AddSingleton<OrderViews>();
builder.Services.AddSingleton<OrderHolds>();

var app = builder.Build();

var instance = Guid.NewGuid();

app.MapGet("/instance", () => instance);

app.MapPost("/carts/{cartId:guid}/products",
    async (Guid cartId, AddProductRequest request, Carts carts, CartViews views, IMessageBus bus) =>
    {
        carts.Add(cartId, request.Product);
        views.Changing();
        await bus.PublishAsync(new ProductAdded(cartId, request.Product));
        return Results.NoContent();
    });

app.MapGet("/carts/{cartId:guid}", (Guid cartId, CartViews views) => views.Get(cartId));

app.MapPost("/carts/{cartId:guid}/orders", async (Guid cartId, Carts carts, OrderViews orders, IMessageBus bus) =>
{
    var products = carts.Products(cartId);
    if (products.Count == 0)
        return Results.Problem(title: "Cart is empty", statusCode: StatusCodes.Status400BadRequest);

    await bus.PublishAsync(Carts.OrderFor(cartId, products));
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

app.MapPost("/carts/{cartId:guid}/checkout", async (Guid cartId, Carts carts, IMessageBus bus) =>
{
    await bus.PublishAsync(Carts.OrderFor(cartId, carts.Products(cartId)));
    await bus.PublishAsync(new CartClosed(cartId));
    return Results.NoContent();
});

app.MapPost("/stock/{product}/checks", async (string product, IMessageBus bus) =>
{
    await bus.PublishAsync(new StockChecked(product));
    return Results.NoContent();
});

app.MapPost("/heartbeats/{id:guid}", async (Guid id, IMessageBus bus) =>
{
    await bus.PublishAsync(new Heartbeat(id));
    return Results.NoContent();
});

app.MapPost("/late-arrivals/{cartId:guid}", (Guid cartId, LateArrivals lateArrivals) =>
{
    lateArrivals.Hold(cartId);
    return Results.NoContent();
});

app.MapPost("/late-arrivals/{cartId:guid}/release", async (Guid cartId, LateArrivals lateArrivals) =>
{
    await lateArrivals.Release(cartId);
    return Results.NoContent();
});

app.Run();

public partial class Program { }

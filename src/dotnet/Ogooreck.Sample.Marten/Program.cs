using System.Diagnostics;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Ogooreck.Sample.Marten;
using Ogooreck.Sample.Marten.Events;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Marten")
                       ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";
var schema = builder.Configuration["Marten:Schema"] ?? "shop";
var asyncDaemon = builder.Configuration.GetValue("Marten:AsyncDaemon", true);

var marten = builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    options.DatabaseSchemaName = schema;
    options.Events.MetadataConfig.HeadersEnabled = true;
    options.Events.MetadataConfig.CorrelationIdEnabled = true;
    options.Events.MetadataConfig.CausationIdEnabled = true;
    options.Projections.Add<CartViewProjection>(ProjectionLifecycle.Async);
}).ApplyAllDatabaseChangesOnStartup().UseLightweightSessions();
if (asyncDaemon)
    marten.AddAsyncDaemon(DaemonMode.Solo);

var app = builder.Build();

app.MapPost("/carts/{cartId:guid}/products", async (Guid cartId, AddProductRequest request, IDocumentSession session) =>
{
    session.SetHeader("traceparent", CurrentTraceparent());
    session.Events.Append(cartId, new ProductAdded(cartId, request.Product));
    await session.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/carts/{cartId:guid}", async (Guid cartId, IQuerySession session) =>
    await session.LoadAsync<CartView>(cartId) is { } cart ? Results.Ok(cart) : Results.NotFound());

app.MapPost("/carts/{cartId:guid}/orders", async (Guid cartId, IDocumentSession session) =>
{
    session.SetHeader("traceparent", CurrentTraceparent());
    session.Events.Append(cartId, new OrderPlaced(cartId));
    await session.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/carts/{cartId:guid}/imports", async (Guid cartId, AddProductRequest request, IDocumentSession session) =>
{
    session.Events.Append(cartId, new ProductAdded(cartId, request.Product));
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

app.Run();

static string CurrentTraceparent() =>
    Activity.Current?.Id ?? throw new InvalidOperationException("The request has no trace context.");

public partial class Program { }

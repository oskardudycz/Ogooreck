using Ogooreck.Sample.RabbitMQ;
using Ogooreck.Sample.RabbitMQ.Events;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

var options = RabbitMQOptions.From(builder.Configuration);
var connection = await new ConnectionFactory
{
    Uri = new Uri(options.ConnectionString),
    ConsumerDispatchConcurrency = options.DispatchConcurrency
}.CreateConnectionAsync();
await Topology.Declare(connection, options);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IConnection>(_ => connection);
builder.Services.AddSingleton<Orders>();
builder.Services.AddSingleton<ShippingFaults>();
builder.Services.AddSingleton<EventPublisher>();
if (options.ShipOrders)
    builder.Services.AddHostedService<ShippingConsumer>();
if (options.ConsumeShippingOutsideTheContainer)
    builder.Services.AddHostedService(_ =>
        new ConsumerOnItsOwnConnection(options.ConnectionString, options.ShippingQueue, -10));

var app = builder.Build();

app.MapPost("/orders", async (PlaceOrder order, EventPublisher events, Orders orders, ShippingFaults faults) =>
{
    orders.Placed(order.OrderId);
    await events.Publish(new OrderPlaced(order.OrderId), EventPublisher.CurrentTraceparent(),
        toAMissingExchange: faults.PublishesToAMissingExchange(order.OrderId));
    return Results.Accepted();
});

app.MapGet("/orders/{orderId:guid}", (Guid orderId, Orders orders) =>
    orders.StatusOf(orderId) is { } status ? Results.Ok(new { status }) : Results.NotFound());

app.MapGet("/shipping", (Orders orders) => Results.Ok(new { handled = orders.Handled, shipped = orders.ShippedCount }));

app.Run();

public record PlaceOrder(Guid OrderId);

public partial class Program { }

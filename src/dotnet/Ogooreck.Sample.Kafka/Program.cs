using System.Collections.Concurrent;
using Confluent.Kafka;
using Ogooreck.Sample.Kafka;
using Ogooreck.Sample.Kafka.Events;

var builder = WebApplication.CreateBuilder(args);

var options = new KafkaOptions(
    builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
    builder.Configuration["Kafka:Topic"] ?? KafkaOptions.DefaultTopic,
    builder.Configuration["Kafka:ShippingGroupId"] ?? "shipping",
    builder.Configuration["Kafka:BillingGroupId"] ?? "billing",
    builder.Configuration.GetSection("Kafka:Consumer").Get<Dictionary<string, string>>() ?? []);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = options.BootstrapServers }).Build());
builder.Services.AddSingleton<IConsumer<string, string>>(_ =>
    new ConsumerBuilder<string, string>(new ConsumerConfig(new Dictionary<string, string>(options.Consumer))
    {
        BootstrapServers = options.BootstrapServers,
        GroupId = options.ShippingGroupId
    }).Build());
builder.Services.AddSingleton<EventPublisher>();
builder.Services.AddSingleton<ShippedOrders>();
builder.Services.AddSingleton<ShippingFaults>();
if (builder.Configuration.GetValue("Shipping:Enabled", true))
    builder.Services.AddHostedService<ShippingService>();
if (builder.Configuration.GetValue<bool>("Billing:Enabled"))
    builder.Services.AddHostedService<BillingService>();

var app = builder.Build();

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
    _ = PublishWhenReleased(HeldMessages.Until(cartId), lifetime.ApplicationStopping,
        () => events.Publish($"{cartId}", new CartClosed(cartId), traceparent));
    return Results.NoContent();
});

app.MapPost("/carts/{cartId:guid}/shipping", async (Guid cartId, EventPublisher events, IHostApplicationLifetime lifetime) =>
{
    var traceparent = EventPublisher.CurrentTraceparent();
    var placed = await events.Publish($"{cartId}", new OrderPlaced(cartId), traceparent);
    _ = PublishWhenReleased(HeldMessages.Until(cartId), lifetime.ApplicationStopping,
        () => events.Publish($"{cartId}", new ShipOrder(cartId), traceparent, correlationId: placed, causationId: placed));
    return Results.NoContent();
});

app.MapPost("/stock/{product}/checks", async (string product, EventPublisher events) =>
{
    await events.Publish(product, new StockChecked(product), EventPublisher.CurrentTraceparent());
    return Results.NoContent();
});

app.Run();

static async Task PublishWhenReleased(Task released, CancellationToken stopping, Func<Task> publish)
{
    try
    {
        await released.WaitAsync(stopping);
        await publish();
    }
    catch (OperationCanceledException) { }
}

public static class HeldMessages
{
    private static readonly ConcurrentDictionary<Guid, TaskCompletionSource> Held = new();

    public static void Release(Guid cartId) => Of(cartId).TrySetResult();

    internal static Task Until(Guid cartId) => Of(cartId).Task;

    private static TaskCompletionSource Of(Guid cartId) =>
        Held.GetOrAdd(cartId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}

public partial class Program { }

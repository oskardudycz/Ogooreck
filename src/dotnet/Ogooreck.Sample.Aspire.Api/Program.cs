using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddSource(Shop.Name))
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddMeter(Shop.Name))
    .UseOtlpExporter();

var kafkaServers = builder.Configuration.GetConnectionString("kafka");
if (kafkaServers is not null)
    builder.Services.AddSingleton<IProducer<string, string>>(services =>
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Kafka");
        return new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = kafkaServers,
            LogConnectionClose = false
        })
            .SetLogHandler((_, message) => logger.LogDebug("{Facility}: {Message}", message.Facility, message.Message))
            .SetErrorHandler((_, error) => logger.LogWarning("{Reason}", error.Reason))
            .Build();
    });

var app = builder.Build();

if (kafkaServers is not null)
    await Shop.CreateOrdersTopic(kafkaServers);

var carts = new ConcurrentDictionary<Guid, List<string>>();

app.MapGet("/echo", (HttpContext context) => Results.Ok(new
{
    port = context.Connection.LocalPort,
    traceparent = context.Request.Headers["traceparent"].ToString()
}));

app.MapPost("/carts/{id}/products", (Guid id, ProductRequest request) =>
{
    carts.GetOrAdd(id, _ => []).Add(request.Product);
    Shop.ProductsAdded.Add(1, new KeyValuePair<string, object?>("cart.id", id.ToString()));
    return Results.Ok();
});

app.MapPost("/carts/{id}/orders", async (Guid id, IServiceProvider services) =>
{
    var products = carts.GetValueOrDefault(id) ?? [];
    var total = products.Count * 1.5;
    var activity = Activity.Current;
    activity?.SetTag("cart.id", id.ToString());
    activity?.SetTag("cart.closed", true);
    activity?.SetTag("cart.total", total);

    Shop.OrdersPlaced.Add(1, new KeyValuePair<string, object?>("cart.id", id.ToString()));

    var producer = services.GetService<IProducer<string, string>>();
    if (producer is not null)
    {
        var placedId = Guid.NewGuid().ToString();
        await producer.ProduceAsync(Shop.OrdersTopic, Shop.Message(id, "OrderPlaced",
            $$"""{"cartId":"{{id}}","total":{{total.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}""",
            placedId, placedId, null, activity));
        await producer.ProduceAsync(Shop.OrdersTopic, Shop.Message(id, "ShipOrder", $$"""{"cartId":"{{id}}"}""",
            Guid.NewGuid().ToString(), placedId, placedId, activity));
    }

    return Results.Created($"/carts/{id}/orders", new { cartId = id, total });
});

app.Run();

public sealed record ProductRequest(string Product);

public static class Shop
{
    public const string Name = "Ogooreck.Sample.Aspire.Api";
    public const string OrdersTopic = "orders";

    public static Message<string, string> Message(Guid cartId, string type, string body, string messageId,
        string correlationId, string? causationId, Activity? activity)
    {
        var message = new Message<string, string>
        {
            Key = cartId.ToString(),
            Value = body,
            Headers = new Headers
            {
                { "message-type", System.Text.Encoding.UTF8.GetBytes(type) },
                { "message-id", System.Text.Encoding.UTF8.GetBytes(messageId) },
                { "correlation-id", System.Text.Encoding.UTF8.GetBytes(correlationId) }
            }
        };
        if (causationId is not null)
            message.Headers.Add("causation-id", System.Text.Encoding.UTF8.GetBytes(causationId));
        if (activity is not null)
            message.Headers.Add("traceparent", System.Text.Encoding.UTF8.GetBytes(activity.Id!));
        return message;
    }

    public static async Task CreateOrdersTopic(string bootstrapServers)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        try
        {
            await admin.CreateTopicsAsync([new TopicSpecification { Name = OrdersTopic, NumPartitions = 1 }]);
        }
        catch (CreateTopicsException exception) when (exception.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
    }

    private static readonly Meter Meter = new(Name);

    public static readonly Counter<long> ProductsAdded = Meter.CreateCounter<long>("cart.products.added");
    public static readonly Counter<long> OrdersPlaced = Meter.CreateCounter<long>("orders.placed");
}

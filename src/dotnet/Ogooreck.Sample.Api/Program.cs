using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json.Serialization;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEndpointsApiExplorer();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(Channel.CreateUnbounded<string>());
builder.Services.AddHostedService<CartExpiryWorker>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<CartStatus>());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<OrderStatus>());
});

var app = builder.Build();

var tracing = new ActivitySource("Ogooreck.Sample.Api");
var meter = app.Services.GetRequiredService<IMeterFactory>().Create("Ogooreck.Sample.Api");
var ordersPlaced = meter.CreateCounter<int>("orders.placed");

app.Use(async (context, next) =>
{
    var request = Activity.Current;
    await next(context);
    if (request is not null && context.GetEndpoint() is RouteEndpoint endpoint)
        request.DisplayName = $"{context.Request.Method} {endpoint.RoutePattern.RawText}";
});

var products = new Dictionary<Guid, Product>();

// Get Products
app.MapGet("/api/products", (string? filter, int? page, int? pageSize) =>
    products.Values
        .Where(p => string.IsNullOrWhiteSpace(filter) || p.Name.Contains(filter) || p.Sku.Contains(filter))
        .Skip(page ?? 0)
        .Take(pageSize ?? 10)
        .ToList()
);


// Get Product Details by Id
app.MapGet("/api/products/{id}", (Guid id) =>
    products.TryGetValue(id, out var product) ? Results.Ok(product) : Results.NotFound()
);

// Register new product
app.MapPost("/api/products/", (RegisterProductRequest request) =>
{
    var productId = Guid.NewGuid();
    var (sku, name) = request;

    if (sku == null || name == null)
        return Results.BadRequest();

    products.Add(productId, new Product(
        productId,
        sku,
        name
    ));

    return Results.Created($"/api/products/{productId}", productId);
});

var instance = Guid.NewGuid();
var carts = new ConcurrentDictionary<string, Cart>();
var orders = new ConcurrentDictionary<string, Order>();
var lastCartId = 0;
var cartNotFound = new { title = "Cart not found" };

meter.CreateObservableCounter("carts.open", () => carts.Values.Count(cart => cart.Status == CartStatus.Open));

app.MapGet("/instance", () => instance);

app.MapGet("/traceparent", (HttpRequest request) => request.Headers["traceparent"].ToString());

app.MapGet("/headers", (HttpRequest request) =>
    request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString()));

app.MapGet("/slow", async (CancellationToken ct) =>
{
    await Task.Delay(Timeout.Infinite, ct);
    return Results.Ok();
});

app.MapGet("/stream", async (HttpResponse response, CancellationToken ct) =>
{
    response.ContentType = "text/plain";
    await response.WriteAsync("first chunk", ct);
    await response.Body.FlushAsync(ct);
    await Task.Delay(Timeout.Infinite, ct);
});

app.MapGet("/failure", () => Results.StatusCode(StatusCodes.Status500InternalServerError));

app.MapPost("/drafts", () => Results.Json(new { draft = true }, statusCode: StatusCodes.Status201Created));

app.MapPost("/carts", () =>
{
    var id = Interlocked.Increment(ref lastCartId);
    carts[id.ToString()] = Cart.Empty(id.ToString());
    return Results.Created($"/carts/{id}", new CartCreated(id));
});

app.MapPost("/carts/{id}/products", (string id, AddProductRequest request) =>
{
    var cart = carts.GetOrAdd(id, Cart.Empty);

    if (cart.Status != CartStatus.Open)
        return Results.Problem(title: "Cart already closed", statusCode: StatusCodes.Status409Conflict);

    var updated = cart with { Products = [.. cart.Products, request.Product], Version = cart.Version + 1 };
    carts[id] = updated;
    return Results.Ok(updated);
});

app.MapPut("/carts/{id}/confirmation", (string id, HttpRequest request) =>
{
    if (!carts.TryGetValue(id, out var cart))
        return Results.NotFound(cartNotFound);
    if (!MatchesVersion(request, cart))
        return Results.StatusCode(StatusCodes.Status412PreconditionFailed);

    carts[id] = cart with { Status = CartStatus.Confirmed, Version = cart.Version + 1 };
    return Results.NoContent();
});

app.MapDelete("/carts/{id}/products/{product}", (string id, string product, HttpRequest request) =>
{
    if (!carts.TryGetValue(id, out var cart))
        return Results.NotFound(cartNotFound);
    if (!MatchesVersion(request, cart))
        return Results.StatusCode(StatusCodes.Status412PreconditionFailed);

    carts[id] = cart with { Products = [.. cart.Products.Where(p => p != product)], Version = cart.Version + 1 };
    return Results.NoContent();
});

app.MapGet("/carts/{id}", (string id, HttpResponse response) =>
{
    if (!carts.TryGetValue(id, out var cart))
        return Results.NotFound(cartNotFound);

    response.Headers.ETag = ETagOf(cart);
    response.Headers["X-Version"] = cart.Version.ToString();
    return Results.Ok(cart);
});

app.MapGet("/carts/{id}/summary", (string id) => Results.Redirect($"/carts/{id}"));

app.MapPost("/carts/{id}/orders", (string id, TimeProvider time, HttpRequest request) =>
{
    if (!carts.TryGetValue(id, out var cart))
        return Results.NotFound(cartNotFound);
    if (cart.Status != CartStatus.Open)
        return Results.Problem(title: "Cart already closed", statusCode: StatusCodes.Status409Conflict);

    var order = PlaceOrder(id, time)!;

    var correlationId = request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    Activity.Current?
        .SetTag("cart.id", id)
        .SetTag("cart.closed", true)
        .SetTag("cart.total", (double)order.Total)
        .SetTag("messaging.message.correlation_id", correlationId);
    ordersPlaced.Add(1, new KeyValuePair<string, object?>("channel", "web"), new KeyValuePair<string, object?>("cart.id", id));
    PublishOrderPlaced(id, correlationId);

    return Results.Created($"/orders/{order.Id}", order);
});

app.MapPost("/carts/{id}/expiry", (string id, Channel<string> expiries) =>
{
    expiries.Writer.TryWrite(id);
    return Results.Accepted();
});

app.MapPost("/carts/{id}/checkout", (string id, TimeProvider time, HttpResponse response) =>
{
    if (PlaceOrder(id, time) is not { } order)
        return Results.NotFound(cartNotFound);

    response.Headers.Location = $"/orders/{order.Id};jsessionid=1?x=1";
    return Results.StatusCode(StatusCodes.Status303SeeOther);
});

app.MapGet("/orders/{id}", (string id) =>
    orders.TryGetValue(id, out var order) ? Results.Ok(order) : Results.NotFound());

app.MapGet("/orders/{id}/status", (string id) =>
    orders.TryGetValue(id, out var order) ? Results.Text(order.Status.ToString()) : Results.NotFound());

app.MapGet("/orders/{id}/export", (string id) =>
    orders.TryGetValue(id, out var order)
        ? Results.Text($"{{\"order_status\":\"{order.Status}\"}}", "application/json")
        : Results.NotFound());

app.MapGet("/orders/{id}/page", (string id) =>
    orders.ContainsKey(id)
        ? Results.Content("<html><body><h1>Order placed</h1></body></html>", "text/html")
        : Results.NotFound());

app.Run();

void PublishOrderPlaced(string cartId, string correlationId)
{
    var messageId = Guid.NewGuid().ToString();
    using var publish = tracing.StartActivity("publish OrderPlaced", ActivityKind.Producer)?
        .SetTag("messaging.message.id", messageId)
        .SetTag("messaging.message.correlation_id", correlationId);
    var publishContext = publish?.Context ?? default;

    _ = Task.Run(async () =>
    {
        using var handle = tracing.StartActivity("handle OrderPlaced", ActivityKind.Consumer, publishContext)?
            .SetTag("messaging.message.correlation_id", correlationId)
            .SetTag("messaging.message.causation_id", messageId);
        await OrderHandling.Of(cartId);
    });
}

static string ETagOf(Cart cart) => $"W/\"{cart.Version}\"";

static bool MatchesVersion(HttpRequest request, Cart cart) =>
    request.Headers.IfMatch.Count == 0 || request.Headers.IfMatch.Contains(ETagOf(cart));

Order? PlaceOrder(string cartId, TimeProvider time)
{
    if (!carts.TryGetValue(cartId, out var cart))
        return null;

    var order = new Order(cartId, OrderStatus.Placed, 0.75m * cart.Products.Length, time.GetUtcNow(),
        new OrderCart(cart.Products.Select(product => new OrderLine(product)).ToArray()));
    orders[cartId] = order;
    return order;
}

public record RegisterProductRequest(
    string? SKU,
    string? Name
);

public record Product(
    Guid Id,
    string Sku,
    string Name
);

public record AddProductRequest(string Product);

public record CartCreated(int Id);

public record Cart(string Id, string[] Products, CartStatus Status, int Version)
{
    public static Cart Empty(string id) => new(id, [], CartStatus.Open, 0);
}

public enum CartStatus
{
    Open,
    Confirmed
}

public class CartExpiryWorker(Channel<string> expiries, IMeterFactory meters): BackgroundService
{
    private readonly Counter<int> cartsExpired = meters.Create("Ogooreck.Sample.Api").CreateCounter<int>("carts.expired");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Activity.Current = null;
        await foreach (var cartId in expiries.Reader.ReadAllAsync(stoppingToken))
            cartsExpired.Add(1, new KeyValuePair<string, object?>("cart.id", cartId));
    }
}

public record Order(string Id, OrderStatus Status, decimal Total, DateTimeOffset PlacedAt, OrderCart Cart);

public record OrderCart(OrderLine[] Lines);

public record OrderLine(string Product);

public enum OrderStatus
{
    Placed
}

public static class OrderHandling
{
    private static readonly ConcurrentDictionary<string, TaskCompletionSource> Held = new();

    public static void Hold(Guid cartId) =>
        Held[cartId.ToString()] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public static void Release(Guid cartId)
    {
        if (Held.TryRemove(cartId.ToString(), out var held))
            held.SetResult();
    }

    internal static Task Of(string cartId) => Held.TryGetValue(cartId, out var held) ? held.Task : Task.CompletedTask;
}

public partial class Program { }

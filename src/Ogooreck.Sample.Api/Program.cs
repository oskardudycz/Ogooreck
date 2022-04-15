var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEndpointsApiExplorer();

var app = builder.Build();

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

app.Run();

public record RegisterProductRequest(
    string? SKU,
    string? Name
);

public record Product(
    Guid Id,
    string Sku,
    string Name
);

public partial class Program { }

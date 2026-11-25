using Ogooreck.Sample.RabbitMQ.Core;
using Ogooreck.Sample.RabbitMQ.Orders;
using Ogooreck.Sample.RabbitMQ.Shipping;

var builder = WebApplication.CreateBuilder(args);

var options = await builder.Services.AddRabbitMQ(builder.Configuration);
builder.Services.AddOrders();
builder.Services.AddShipping(options);

var app = builder.Build();

app.MapOrders().MapShipping();

app.Run();

public partial class Program { }

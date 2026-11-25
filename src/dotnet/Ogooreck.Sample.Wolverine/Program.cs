using Ogooreck.Sample.Wolverine.Carts;
using Ogooreck.Sample.Wolverine.Events;
using Ogooreck.Sample.Wolverine.Heartbeats;
using Ogooreck.Sample.Wolverine.LateArrivals;
using Ogooreck.Sample.Wolverine.Orders;
using Ogooreck.Sample.Wolverine.Stock;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine(options =>
{
    options.Publish(rule => rule.MessagesFromNamespaceContaining<OrderPlaced>().ToLocalQueue("events"));
    options.LocalQueue("events").Sequential();
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddCarts().AddOrders().AddLateArrivals();

var app = builder.Build();

var instance = Guid.NewGuid();

app.MapGet("/instance", () => instance);

app.MapCarts().MapOrders().MapStock().MapHeartbeats().MapLateArrivals();

app.Run();

public partial class Program { }

using Ogooreck.Sample.Marten.Carts;
using Ogooreck.Sample.Marten.Core;
using Ogooreck.Sample.Marten.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEventStore(builder.Configuration);

var app = builder.Build();

app.MapCarts().MapOrders();

app.Run();

public partial class Program { }

using Ogooreck.Sample.Kafka.Billing;
using Ogooreck.Sample.Kafka.Carts;
using Ogooreck.Sample.Kafka.Core;
using Ogooreck.Sample.Kafka.Shipping;
using Ogooreck.Sample.Kafka.Stock;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddKafka(builder.Configuration)
    .AddShipping(builder.Configuration)
    .AddBilling(builder.Configuration);

var app = builder.Build();

app.MapCarts().MapStock();

app.Run();

public partial class Program { }

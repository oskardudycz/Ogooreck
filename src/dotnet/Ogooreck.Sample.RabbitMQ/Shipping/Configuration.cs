using Ogooreck.Sample.RabbitMQ.Core;
using Ogooreck.Sample.RabbitMQ.Orders;

namespace Ogooreck.Sample.RabbitMQ.Shipping;

public static class Configuration
{
    public static IServiceCollection AddShipping(this IServiceCollection services, RabbitMQOptions options)
    {
        services.AddSingleton<ShippingFaults>();
        if (options.ShipOrders)
            services.AddHostedService<ShippingConsumer>();
        if (options.ConsumeShippingOutsideTheContainer)
            services.AddHostedService(_ =>
                new ConsumerOnItsOwnConnection(options.ConnectionString, options.ShippingQueue, -10));
        return services;
    }

    public static IEndpointRouteBuilder MapShipping(this IEndpointRouteBuilder app)
    {
        app.MapGet("/shipping", (OrderStatuses orders) => Results.Ok(new { handled = orders.Handled, shipped = orders.ShippedCount }));
        return app;
    }
}

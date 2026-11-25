namespace Ogooreck.Sample.Kafka.Shipping;

public static class Configuration
{
    public static IServiceCollection AddShipping(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ShippedOrders>();
        services.AddSingleton<ShippingFaults>();
        if (configuration.GetValue("Shipping:Enabled", true))
            services.AddHostedService<ShippingService>();
        return services;
    }
}

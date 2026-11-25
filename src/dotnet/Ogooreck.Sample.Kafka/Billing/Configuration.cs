namespace Ogooreck.Sample.Kafka.Billing;

public static class Configuration
{
    public static IServiceCollection AddBilling(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("Billing:Enabled"))
            services.AddHostedService<BillingService>();
        return services;
    }
}

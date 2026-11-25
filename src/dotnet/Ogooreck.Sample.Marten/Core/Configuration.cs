using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Ogooreck.Sample.Marten.Carts.GettingCart;

namespace Ogooreck.Sample.Marten.Core;

public static class Configuration
{
    public static IServiceCollection AddEventStore(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Marten")
                               ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";
        var schema = configuration["Marten:Schema"] ?? "shop";
        var asyncDaemon = configuration.GetValue("Marten:AsyncDaemon", true);

        var marten = services.AddMarten(options =>
        {
            options.Connection(connectionString);
            options.DatabaseSchemaName = schema;
            options.Events.MetadataConfig.HeadersEnabled = true;
            options.Events.MetadataConfig.CorrelationIdEnabled = true;
            options.Events.MetadataConfig.CausationIdEnabled = true;
            options.Projections.Add<CartViewProjection>(ProjectionLifecycle.Async);
        }).ApplyAllDatabaseChangesOnStartup().UseLightweightSessions();
        if (asyncDaemon)
            marten.AddAsyncDaemon(DaemonMode.Solo);
        return services;
    }
}

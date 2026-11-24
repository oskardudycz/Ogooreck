using Microsoft.AspNetCore.Mvc.Testing;
using Ogooreck.API;

namespace Ogooreck.Marten.Tests;

public class MartenApi: ApiSpecification<Program>
{
    private MartenApi(PostgresDatabase database, bool asyncDaemon, IPlug plug) : base(
        HostFor(database.ConnectionString, asyncDaemon), spec => spec.Use(plug))
    { }

    public MartenApi(PostgresDatabase database) : this(database, asyncDaemon: true, EventStore.Marten) { }

    public static MartenApi WithoutCatchUp(PostgresDatabase database) =>
        new(database, asyncDaemon: false, EventStore.Marten.WithoutCatchUp());

    public static MartenApi WithStalledProjections(PostgresDatabase database) =>
        new(database, asyncDaemon: false, EventStore.Marten);

    private static Func<WebApplicationFactory<Program>, WebApplicationFactory<Program>> HostFor(string connectionString,
        bool asyncDaemon)
    {
        var schema = $"shop_{Guid.NewGuid():N}";
        return factory => factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Marten", connectionString);
            builder.UseSetting("Marten:Schema", schema);
            builder.UseSetting("Marten:AsyncDaemon", asyncDaemon.ToString());
        });
    }
}

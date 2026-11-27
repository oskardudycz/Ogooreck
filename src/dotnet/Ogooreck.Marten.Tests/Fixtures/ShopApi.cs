using Ogooreck.API;

namespace Ogooreck.Marten.Tests.Fixtures;

#region MartenShopApi

public class ShopApi(PostgresDatabase database): ApiSpecification<Program>(
    factory => factory.WithWebHostBuilder(builder => builder
        .UseSetting("ConnectionStrings:Marten", database.ConnectionString)
        .UseSetting("Marten:Schema", $"shop_{Guid.NewGuid():N}")),
    spec => spec.Use(EventStore.Marten));

#endregion

using Testcontainers.PostgreSql;
using Xunit;

namespace Ogooreck.Marten.Tests.Fixtures;

public sealed class PostgresDatabase: IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18.7-alpine").Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}

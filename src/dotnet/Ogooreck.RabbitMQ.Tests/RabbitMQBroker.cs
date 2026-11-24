using Testcontainers.RabbitMq;
using Xunit;

namespace Ogooreck.RabbitMQ.Tests;

public sealed class RabbitMQBroker: IAsyncLifetime
{
    private readonly RabbitMqContainer container = new RabbitMqBuilder("rabbitmq:4.4.0").Build();

    public RabbitMQApi App { get; private set; } = null!;

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        App = new RabbitMQApi(this, AppSettings.Shared);
    }

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        await container.DisposeAsync();
    }
}

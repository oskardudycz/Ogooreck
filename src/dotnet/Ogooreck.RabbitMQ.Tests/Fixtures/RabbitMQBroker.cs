using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Ogooreck.RabbitMQ.Tests.Fixtures;

public sealed class RabbitMQBroker: IAsyncLifetime
{
    private readonly RabbitMqContainer container = new RabbitMqBuilder("rabbitmq:4.4.0").Build();

    public RabbitMQApi App { get; private set; } = null!;

    public string ConnectionString => container.GetConnectionString();

    public async Task UntilEmpty(string queue, CancellationToken ct)
    {
        await using var connection =
            await new ConnectionFactory { Uri = new Uri(ConnectionString) }.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        while (await channel.MessageCountAsync(queue, ct) > 0)
            await Task.Delay(TimeSpan.FromMilliseconds(10), ct);
    }

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

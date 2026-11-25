using RabbitMQ.Client;

namespace Ogooreck.Sample.RabbitMQ.Core;

public static class Configuration
{
    public static async Task<RabbitMQOptions> AddRabbitMQ(this IServiceCollection services, IConfiguration configuration)
    {
        var options = RabbitMQOptions.From(configuration);
        var connection = await new ConnectionFactory
        {
            Uri = new Uri(options.ConnectionString),
            ConsumerDispatchConcurrency = options.DispatchConcurrency
        }.CreateConnectionAsync();
        await Topology.Declare(connection, options);

        services.AddSingleton(options);
        services.AddSingleton<IConnection>(_ => connection);
        services.AddSingleton<EventPublisher>();
        return options;
    }
}

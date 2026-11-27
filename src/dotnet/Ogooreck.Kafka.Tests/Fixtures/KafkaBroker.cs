using System.Net;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Ogooreck.API;
using Ogooreck.Kafka;
using Ogooreck.Sample.Kafka.Core;
using Ogooreck.Sample.Kafka.Events;
using Ogooreck.Sample.Kafka.Shipping;
using Testcontainers.Kafka;
using Xunit;

namespace Ogooreck.Kafka.Tests.Fixtures;

public sealed class KafkaBroker: IAsyncLifetime
{
    private readonly KafkaContainer container = new KafkaBuilder("confluentinc/cp-kafka:8.4.1")
        .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
        .Build();

    public KafkaBroker() =>
        Consuming = KafkaPlugin.Consuming(() => Task.FromResult(BootstrapServers), [KafkaOptions.DefaultTopic])
            .MessageTypes(typeof(OrderPlaced), typeof(CartClosed), typeof(ShipOrder), typeof(OrderPaid),
                typeof(StockChecked));

    public KafkaConsumingPlugin Consuming { get; }

    public ShippingFaults Faults { get; } = new();

    public KafkaApi App { get; private set; } = null!;

    public AppServicesPlugin PublishOnlyServices { get; } = new();

    public KafkaApi PublishOnly { get; private set; } = null!;

    public string BootstrapServers => container.GetBootstrapAddress();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        await NewTopic(KafkaOptions.DefaultTopic);
        await NewTopic(AppSettings.SharedTopic);
        await NewTopic(ShopApi.Topic);
        App = new KafkaApi(this, AppSettings.Shared, Messaging.Kafka, new SharedFaultsPlugin(Faults));
        PublishOnly = new KafkaApi(this, AppSettings.WithoutShipping(), Messaging.Kafka, Consuming, PublishOnlyServices);
        await StartUntracedConsumersBeforeAnythingTraces();
        await StartConsumersOf(PublishOnly);
    }

    private async Task StartUntracedConsumersBeforeAnythingTraces()
    {
        await using var untraced = new KafkaApi(this, AppSettings.Own(KafkaOptions.DefaultTopic), Messaging.Kafka);
        await StartConsumersOf(untraced.Telemetry(TelemetrySources.Off));
    }

    private static Task StartConsumersOf(ApiSpecification<Program> app) =>
        app.Given()
            .When(ShopRequests.GetCart(Guid.NewGuid()))
            .Then([ApiSpecification.ExpectResponse(HttpStatusCode.OK)], TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        await PublishOnly.DisposeAsync();
        await Consuming.DisposeAsync();
        await container.DisposeAsync();
    }

    public Task StopAnswering() => container.PauseAsync();

    public Task AnswerAgain() => container.UnpauseAsync();

    public async Task ProduceOlderOrders(string topic)
    {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = BootstrapServers })
            .Build();
        for (var index = 0; index < 9; index++)
        {
            var older = Guid.NewGuid();
            await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = $"{older}",
                Value = JsonSerializer.Serialize(new OrderPlaced(older), JsonSerializerOptions.Web),
                Headers = new Headers
                {
                    { "messageType", Encoding.UTF8.GetBytes(typeof(OrderPlaced).FullName!) },
                    { "traceparent", Encoding.UTF8.GetBytes("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01") }
                }
            });
        }
    }

    public async Task<string> NewTopic(string? name = null)
    {
        var topic = name ?? $"shop-events-{Guid.NewGuid()}";
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();
        await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 3, ReplicationFactor = 1 }]);
        return topic;
    }

    public async Task<bool> HasConsumerOf(string topic)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();
        var listing = await admin.ListConsumerGroupsAsync();
        var described = await admin.DescribeConsumerGroupsAsync(listing.Valid.Select(group => group.GroupId));
        return described.ConsumerGroupDescriptions.Any(group => group.Members.Any(member =>
            member.Assignment.TopicPartitions.Any(partition => partition.Topic == topic)));
    }
}

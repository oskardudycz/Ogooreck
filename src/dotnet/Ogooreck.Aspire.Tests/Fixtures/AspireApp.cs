using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Ogooreck.API;
using Ogooreck.Kafka;
using Xunit;

namespace Ogooreck.Aspire.Tests.Fixtures;

public sealed class AspireApp(OtlpReceiver otlp, bool withKafka): IAsyncDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);

    private readonly Lazy<Task<DistributedApplication>> started = new(() => Start(otlp, withKafka));

    private KafkaConsumingPlugin? kafka;

    public KafkaConsumingPlugin Kafka => LazyInitializer.EnsureInitialized(ref kafka, ConsumingOrders);

    public async Task<Uri> Api() => (await started.Value).GetEndpoint("api");

    public async ValueTask DisposeAsync()
    {
        if (kafka is not null)
            await kafka.DisposeAsync();
        if (started.IsValueCreated)
            await (await started.Value).DisposeAsync();
    }

    private KafkaConsumingPlugin ConsumingOrders() => KafkaPlugin
        .Consuming(async () => (await (await started.Value).GetConnectionStringAsync("kafka"))!, ["orders"])
        .MessageTypes(typeof(OrderPlaced), typeof(ShipOrder))
        .Headers(new MessageHeaders
        {
            MessageType = "message-type",
            MessageId = "message-id",
            CorrelationId = "correlation-id",
            CausationId = "causation-id"
        });

    private static async Task<DistributedApplication> Start(OtlpReceiver otlp, bool withKafka)
    {
        Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
        var ct = TestContext.Current.CancellationToken;
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Ogooreck_Sample_Aspire_AppHost>(
            [$"--OtlpEndpoint={otlp.Endpoint}", $"--WithKafka={withKafka.ToString().ToLowerInvariant()}"], ct);
        var app = await builder.BuildAsync(ct);
        await app.StartAsync(ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", ct).WaitAsync(StartTimeout, ct);
        return app;
    }
}

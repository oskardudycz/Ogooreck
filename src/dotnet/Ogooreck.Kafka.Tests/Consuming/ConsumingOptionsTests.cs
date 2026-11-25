using System.Net;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Kafka;
using Ogooreck.Kafka.Tests.Fixtures;
using Ogooreck.Sample.Kafka.Core;
using Ogooreck.Sample.Kafka.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Kafka.Tests.Consuming;

public class ConsumingOptionsTests(KafkaBroker broker)
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Consuming_ReadsWithTheOptionsOfThePlugin()
    {
        await using var kafka = KafkaPlugin.Consuming(() => Task.FromResult(broker.BootstrapServers), [KafkaOptions.DefaultTopic])
            .MessageTypes(name => name == typeof(OrderPlaced).FullName ? typeof(OrderPlaced) : null)
            .Headers(new MessageHeaders())
            .Json(JsonSerializerOptions.Web);
        await using var shop = broker.PublishOnly.With(spec => spec.Use(kafka));

        await shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task Consuming_WithAReadFunction_ReadsWithIt()
    {
        await using var kafka = KafkaPlugin.Consuming(() => Task.FromResult(broker.BootstrapServers), [KafkaOptions.DefaultTopic])
            .Read(recorded => new Observed(new OrderPlaced(Guid.TryParse(recorded.Message.Key, out var id) ? id : Guid.Empty),
                recorded.Message.Headers?.TryGetLastBytes("traceparent", out var traceparent) == true ? Encoding.UTF8.GetString(traceparent) : null));
        await using var shop = broker.PublishOnly.With(spec => spec.Use(kafka));

        await shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task Consuming_DisposedByTheCaller_StopsItsConsumer()
    {
        var topic = await broker.NewTopic();
        var kafka = KafkaPlugin.Consuming(() => Task.FromResult(broker.BootstrapServers), [topic]);
        await using var shop = broker.PublishOnly.With(spec => spec.Use(kafka));
        await shop.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct);
        Assert.True(await broker.HasConsumerOf(topic));

        await kafka.DisposeAsync();

        Assert.False(await broker.HasConsumerOf(topic));
    }
}

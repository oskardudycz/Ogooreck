using System.Net;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Kafka;
using Ogooreck.Sample.Kafka;
using Ogooreck.Sample.Kafka.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.ShopRequests;

namespace Ogooreck.Kafka.Tests;

public class PlugOptionsTests(KafkaBroker broker)
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Decorating_ReadsWithTheOptionsOfThePlug()
    {
        var kafka = Messaging.Kafka
            .MessageTypes(name => name == typeof(OrderPlaced).FullName ? typeof(OrderPlaced) : null)
            .Headers(new MessageHeaders())
            .Json(JsonSerializerOptions.Web);
        await using var shop = broker.PublishOnly.With(spec => spec.Use(kafka));

        await shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task Decorating_WithAReadFunction_ReadsWithIt()
    {
        var kafka = Messaging.Kafka
            .Read(recorded => new Observed(new OrderPlaced(Guid.TryParse(recorded.Message.Key, out var id) ? id : Guid.Empty),
                recorded.Message.Headers?.TryGetLastBytes("traceparent", out var traceparent) == true ? Encoding.UTF8.GetString(traceparent) : null));
        await using var shop = broker.PublishOnly.With(spec => spec.Use(kafka));

        await shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task Decorating_ByteArrayValues_AreReadAsTheirJson()
    {
        var kafka = KafkaPlug.Decorating<string, byte[]>().MessageTypes(typeof(OrderPlaced));
        var services = new AppServicesPlug();
        await using var shop = new KafkaApi(broker, AppSettings.WithoutShipping(), new ExtraProducerPlug<byte[]>(broker),
            kafka, services);

        await shop.Given()
            .When(Request(async (_, step) =>
            {
                var headers = new Headers
                {
                    { "messageType", Encoding.UTF8.GetBytes(typeof(OrderPlaced).FullName!) },
                    { "traceparent", Encoding.UTF8.GetBytes(step.Traceparent) }
                };
                await services.Services.GetRequiredService<IProducer<string, byte[]>>().ProduceAsync(
                    KafkaOptions.DefaultTopic,
                    new Message<string, byte[]>
                    {
                        Key = $"{cartId}",
                        Value = JsonSerializer.SerializeToUtf8Bytes(new OrderPlaced(cartId), JsonSerializerOptions.Web),
                        Headers = headers
                    }, ct);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }))
            .Then([ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task Decorating_ValuesOfAnotherType_NeedAReadFunction()
    {
        var kafka = KafkaPlug.Decorating<string, int>();
        var services = new AppServicesPlug();
        await using var shop = new KafkaApi(broker, AppSettings.WithoutShipping(), new ExtraProducerPlug<int>(broker),
            kafka, services);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => shop.Given()
            .When(Request(async (_, step) =>
            {
                await services.Services.GetRequiredService<IProducer<string, int>>().ProduceAsync(
                    KafkaOptions.DefaultTopic,
                    new Message<string, int>
                    {
                        Key = $"{cartId}",
                        Value = 42,
                        Headers = new Headers { { "traceparent", Encoding.UTF8.GetBytes(step.Traceparent) } }
                    }, ct);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }))
            .Then([ExpectNothingPublishedIn(kafka.Published)], ct));

        Assert.Contains("message values of type string or byte[]", error.ToString());
    }

    [Fact]
    public async Task Decorating_ProducingToAPartition_RecordsTheMessage()
    {
        var kafka = Messaging.Kafka.MessageTypes(typeof(OrderPlaced));
        var services = broker.PublishOnlyServices;
        await using var shop = broker.PublishOnly.With(spec => spec.Use(kafka));

        await shop.Given()
            .When(Request(async (_, step) =>
            {
                await services.Services.GetRequiredService<IProducer<string, string>>().ProduceAsync(
                    new TopicPartition(KafkaOptions.DefaultTopic, 0),
                    new Message<string, string>
                    {
                        Key = $"{cartId}",
                        Value = JsonSerializer.Serialize(new OrderPlaced(cartId), JsonSerializerOptions.Web),
                        Headers = new Headers
                        {
                            { "messageType", Encoding.UTF8.GetBytes(typeof(OrderPlaced).FullName!) },
                            { "traceparent", Encoding.UTF8.GetBytes(step.Traceparent) }
                        }
                    }, ct);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }))
            .Then([ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task Decorating_AMessageWithoutHeaders_IsNotPartOfTheStep()
    {
        var kafka = Messaging.Kafka.MessageTypes(typeof(OrderPlaced));
        var services = broker.PublishOnlyServices;
        await using var shop = broker.PublishOnly.With(spec => spec.Use(kafka));

        var error = await Assert.ThrowsAsync<AssertionException>(() => shop.Given()
            .When(Request(async _ =>
            {
                await services.Services.GetRequiredService<IProducer<string, string>>().ProduceAsync(
                    KafkaOptions.DefaultTopic, new Message<string, string> { Key = $"{cartId}", Value = "{}" }, ct);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }))
            .Then([ExpectPublishedIn(kafka.Published, new OrderPlaced(cartId))], ct));

        Assert.Contains("the When step published 0", error.Message);
    }

    [Fact]
    public async Task Consuming_ReadsWithTheOptionsOfThePlug()
    {
        await using var kafka = KafkaPlug.Consuming(() => Task.FromResult(broker.BootstrapServers), [KafkaOptions.DefaultTopic])
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
        await using var kafka = KafkaPlug.Consuming(() => Task.FromResult(broker.BootstrapServers), [KafkaOptions.DefaultTopic])
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
        var kafka = KafkaPlug.Consuming(() => Task.FromResult(broker.BootstrapServers), [topic]);
        await using var shop = broker.PublishOnly.With(spec => spec.Use(kafka));
        await shop.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct);
        Assert.True(await broker.HasConsumerOf(topic));

        await kafka.DisposeAsync();

        Assert.False(await broker.HasConsumerOf(topic));
    }
}

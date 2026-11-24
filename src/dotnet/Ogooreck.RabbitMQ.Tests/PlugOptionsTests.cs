using System.Text.Json;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.RabbitMQ;
using Ogooreck.Sample.RabbitMQ.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.RabbitMQ.Tests.ShopRequests;

namespace Ogooreck.RabbitMQ.Tests;

public class PlugOptionsTests(RabbitMQBroker broker)
{
    private readonly Guid orderId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task MessageTypesFromALookup_AndJsonOptions_ExactCheckPasses()
    {
        var plug = Messaging.RabbitMQ
            .MessageTypes(name => name switch
            {
                "Ogooreck.Sample.RabbitMQ.Events.OrderPlaced" => typeof(OrderPlaced),
                "Ogooreck.Sample.RabbitMQ.Events.ShipOrder" => typeof(ShipOrder),
                _ => null
            })
            .Json(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await using var shop = broker.App.With(spec => spec.Use(plug));

        await shop.Given()
            .When(PlaceOrder(orderId))
            .Then([ExpectPublishedIn(plug.Published, new OrderPlaced(orderId), new ShipOrder(orderId))], ct);
    }

    [Fact]
    public async Task OtherHeaderNames_FailTheStepNamingTheHeader()
    {
        var plug = Messaging.RabbitMQ.Headers(new MessageHeaders { MessageType = "message-type" });
        await using var shop = broker.App.With(spec => spec.Use(plug));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given().When(PlaceOrder(orderId))
                .Then([ExpectPublishedIn(plug.Published, new OrderPlaced(orderId))], ct));

        Assert.Contains("its message-type header is missing", error.Message);
    }

    [Fact]
    public async Task ReadFunction_ReplacesTheHeaderReader()
    {
        var plug = Messaging.RabbitMQ.Read(message => new Observed(
            JsonSerializer.Deserialize<OrderPlaced>(message.Body.Span, JsonSerializerOptions.Web)!,
            message.Properties.Headers?["traceparent"] as string));
        await using var shop = broker.App.With(spec => spec.Use(plug));

        await shop.Given()
            .When(PlaceOrder(orderId))
            .Then([ExpectPublishedIn(plug.Published, new OrderPlaced(orderId), new OrderPlaced(orderId))], ct);
    }

    [Fact]
    public async Task CatchingUpWith_ReplacesTheProbeAndTheCheck()
    {
        var catchUps = 0;
        var plug = Messaging.RabbitMQ.CatchingUpWith(_ =>
        {
            catchUps++;
            return Task.CompletedTask;
        });
        await using var shop = new RabbitMQApi(broker,
            AppSettings.Own() with { ConsumeShippingOutsideTheContainer = true }, plug);

        await shop.Given()
            .When(GetOrder(orderId))
            .Then([ExpectResponse(NotFound)], ct);

        Assert.Equal(1, catchUps);
    }
}

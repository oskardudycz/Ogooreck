using Ogooreck.Sample.RabbitMQ.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.RabbitMQ.Tests.ShopRequests;

namespace Ogooreck.RabbitMQ.Tests;

public class ShippingTests(RabbitMQBroker broker)
{
    private readonly Guid orderId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExactCheckPasses_AfterTheConsumerShipsTheOrder()
    {
        await broker.App.Given()
            .When(PlaceOrder(orderId))
            .Then([ExpectPublished(new OrderPlaced(orderId), new ShipOrder(orderId))], ct);
    }

    [Fact]
    public async Task TheNextStep_SeesTheOrderShipped_WithoutEventually()
    {
        await broker.App.Given(PlaceOrder(orderId))
            .When(GetOrder(orderId))
            .Then([ExpectResponse(OK, Body(new { status = "Shipped" }))], ct);
    }

    [Fact]
    public async Task RecommendedHeaders_AreReadWithoutAReadFunction()
    {
        await broker.App.Given()
            .When(PlaceOrder(orderId))
            .Then([
                ExpectCausation(Published(new OrderPlaced(orderId),
                    Sent(new ShipOrder(orderId))))
            ], ct);
    }

    [Fact]
    public async Task NackWithRequeueOnTheFirstTry_ExactCheckStillPasses()
    {
        broker.App.Faults.NackTheFirstTryOf(orderId);

        await broker.App.Given()
            .When(PlaceOrder(orderId))
            .Then([ExpectPublished(new OrderPlaced(orderId), new ShipOrder(orderId))], ct);
    }

    [Fact]
    public async Task RejectWithRequeueOnTheFirstTry_ExactCheckStillPasses()
    {
        broker.App.Faults.RejectTheFirstTryOf(orderId);

        await broker.App.Given()
            .When(PlaceOrder(orderId))
            .Then([ExpectPublished(new OrderPlaced(orderId), new ShipOrder(orderId))], ct);
    }

    [Fact]
    public async Task SlowHandler_ExactCheckStillPasses()
    {
        broker.App.Faults.HoldShippingOf(orderId);
        await using var shop = broker.App.With(spec => spec.CatchUp(async (step, token) =>
        {
            await step(token);
            broker.App.Faults.ReleaseShippingOf(orderId);
        }));

        await shop.Given()
            .When(PlaceOrder(orderId))
            .Then([ExpectPublished(new OrderPlaced(orderId), new ShipOrder(orderId))], ct);
    }

    [Fact]
    public async Task PublishToAMissingExchange_FailsTheRequestAndRecordsNothing()
    {
        broker.App.Faults.PublishToAMissingExchangeFor(orderId);

        await broker.App.Given()
            .When(PlaceOrder(orderId))
            .Then([
                ExpectResponse(InternalServerError),
                ExpectNothingPublished()
            ], ct);
    }
}

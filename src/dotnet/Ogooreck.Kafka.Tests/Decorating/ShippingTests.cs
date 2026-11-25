using Ogooreck.Kafka.Tests.Fixtures;
using Ogooreck.Sample.Kafka.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Kafka.Tests.Decorating;

public class ShippingTests(KafkaBroker broker)
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExactCheckPasses_AfterTheConsumerShipsTheOrder()
    {
        await broker.App.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublished(new OrderPlaced(cartId), new ShipOrder(cartId))], ct);
    }

    [Fact]
    public async Task TheNextStep_SeesTheOrderShipped_WithoutEventually()
    {
        await broker.App.Given(PlaceOrderToShip(cartId))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { cartId, shipped = true }))], ct);
    }

    [Fact]
    public async Task RetryInsideTheLoop_ExactCheckStillPasses()
    {
        broker.Faults.FailFirstTryOf(cartId);

        await broker.App.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublished(new OrderPlaced(cartId), new ShipOrder(cartId))], ct);
        await broker.App.Given()
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { cartId, shipped = true }))], ct);
    }

    [Fact]
    public async Task SlowHandlerWithQueuedMessages_ExactCheckStillPasses()
    {
        broker.Faults.HoldShippingOf(cartId);
        await using var shop = broker.App.With(spec => spec.CatchUp(async (step, token) =>
        {
            await step(token);
            broker.Faults.ReleaseShippingOf(cartId);
        }));

        await shop.Given()
            .When(Request(async client =>
            {
                await client.PostAsync($"/carts/{cartId}/placed-orders", null, ct);
                return await client.PostAsync($"/carts/{cartId}/placed-orders", null, ct);
            }))
            .Then([
                ExpectPublished(new OrderPlaced(cartId), new OrderPlaced(cartId),
                    new ShipOrder(cartId), new ShipOrder(cartId))
            ], ct);
    }
}

using Ogooreck.Kafka.Tests.Fixtures;
using Ogooreck.Sample.Kafka.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Kafka.Tests.Shipping;

public class ShippingApiTests(ShopApi shop): IClassFixture<ShopApi>
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    #region KafkaCatchUp

    [Fact]
    public Task PlacingAnOrder_ShipsIt() =>
        shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([
                ExpectPublished(new OrderPlaced(cartId), new ShipOrder(cartId)),
                ExpectCausation(Published(new OrderPlaced(cartId),
                    Sent(new ShipOrder(cartId))))
            ], ct);

    [Fact]
    public Task AfterPlacingAnOrder_TheCartShowsItShipped() =>
        shop.Given(PlaceOrderToShip(cartId))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { cartId, shipped = true }))], ct);

    #endregion
}

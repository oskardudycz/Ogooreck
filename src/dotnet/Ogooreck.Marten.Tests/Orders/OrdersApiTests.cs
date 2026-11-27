using Ogooreck.Marten.Tests.Fixtures;
using Ogooreck.Sample.Marten.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Marten.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Marten.Tests.Orders;

public class OrdersApiTests(ShopApi shop): IClassFixture<ShopApi>
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    #region MartenExpectAppended

    [Fact]
    public Task PlacingAnOrder_AppendsOrderPlaced() =>
        shop.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectAppended(new OrderPlaced(cartId))], ct);

    [Fact]
    public Task ShippingAnOrder_AppendsOrderShipped_CausedByOrderPlaced() =>
        shop.Given()
            .When(ShipOrder(cartId))
            .Then([
                ExpectCausation(Appended(new OrderPlaced(cartId),
                    Appended(new OrderShipped(cartId))))
            ], ct);

    #endregion

    #region MartenProjectionCatchUp

    [Fact]
    public Task AddingAProduct_ShowsItInTheCartProjection() =>
        shop.Given(AddProduct(cartId, "socks"))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { products = new[] { "socks" } }))],
                ct);

    #endregion
}

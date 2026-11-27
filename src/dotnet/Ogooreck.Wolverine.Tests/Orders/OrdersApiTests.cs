using Ogooreck.Sample.Wolverine.Events;
using Ogooreck.Sample.Wolverine.Orders.Paying;
using Ogooreck.Wolverine.Tests.Fixtures;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Wolverine.Tests.Orders;

public class OrdersApiTests(ShopApi api): IClassFixture<ShopApi>
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    #region WolverineExpectPublished

    [Fact]
    public Task PlacingAnOrder_PublishesOrderPlaced() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([
                ExpectResponse(Created),
                ExpectPublished(
                    new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })
            ], ct);

    [Fact]
    public Task CheckingOut_PlacesTheOrderAndClosesTheCart() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(CheckOut(cartId))
            .Then([
                ExpectPublished(
                    new OrderPlaced(cartId) { Total = 5, Products = ["socks"] },
                    new CartClosed(cartId))
            ], ct);

    [Fact]
    public Task ViewingACart_PublishesNothing() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK), ExpectNothingPublished()], ct);

    #endregion

    #region WolverinePublish

    [Fact]
    public Task ConfirmedPayment_MarksTheOrderPaid() =>
        api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
            .When(Publish(new PaymentConfirmed(cartId)))
            .Then([ExpectPublished(new OrderPaid(cartId))], ct);

    #endregion

    #region WolverineAction

    [Fact]
    public async Task AnAbandonedCart_ExpiresAfterADay()
    {
        await using var shop = new ShopApi();

        await shop.Given(AddProduct(cartId, "socks"))
            .When(Action(_ =>
            {
                shop.AppClock.Advance(TimeSpan.FromDays(1));
                return Task.CompletedTask;
            }))
            .Then([ExpectPublished(new CartExpired(cartId))], ct);
    }

    #endregion

    #region WolverineEventually

    [Fact]
    public Task PlacedOrder_EventuallyShowsAsPlaced() =>
        api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
            .When(Eventually(GetOrder(cartId)))
            .Then([ExpectResponse(OK, Body(new { status = "Placed" }))], ct);

    #endregion
}

using Ogooreck.RabbitMQ.Tests.Fixtures;
using Ogooreck.Sample.RabbitMQ.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.RabbitMQ.Tests.Fixtures.ShopRequests;

namespace Ogooreck.RabbitMQ.Tests.Shipping;

public class ShippingApiTests(ShopApi shop): IClassFixture<ShopApi>
{
    private readonly Guid orderId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    #region RabbitMQCatchUp

    [Fact]
    public Task PlacingAnOrder_ShipsIt() =>
        shop.Given()
            .When(PlaceOrder(orderId))
            .Then([
                ExpectPublished(new OrderPlaced(orderId), new ShipOrder(orderId)),
                ExpectCausation(Published(new OrderPlaced(orderId),
                    Sent(new ShipOrder(orderId))))
            ], ct);

    [Fact]
    public Task AfterPlacingAnOrder_TheOrderIsShipped() =>
        shop.Given(PlaceOrder(orderId))
            .When(GetOrder(orderId))
            .Then([ExpectResponse(OK, Body(new { status = "Shipped" }))], ct);

    #endregion
}

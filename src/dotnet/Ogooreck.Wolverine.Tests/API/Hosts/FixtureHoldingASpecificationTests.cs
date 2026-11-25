using Ogooreck.Sample.Wolverine.Events;
using Xunit;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Wolverine.Tests.API.Hosts;

public class FixtureHoldingASpecificationTests(ShopFixture shop): IClassFixture<ShopFixture>
{
    [Fact]
    public Task FixtureHoldingASpecification_BehavesLikeASubclass()
    {
        var cartId = Guid.NewGuid();

        return shop.Spec.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectPublished(new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })],
                TestContext.Current.CancellationToken);
    }
}

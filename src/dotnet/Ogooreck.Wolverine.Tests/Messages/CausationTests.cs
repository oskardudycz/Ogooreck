using Ogooreck.Sample.Wolverine.Events;
using Xunit;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.ShopRequests;

namespace Ogooreck.Wolverine.Tests.Messages;

public class CausationTests(AppApi api)
{
    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public async Task ExpectCausation_PassesForAMessageCausedByAnotherWithTheSameCorrelation()
    {
        ShipOnOrderPlaced.ShipsOrdersOf(cartId);

        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([
                ExpectCausation(Published(new OrderPlaced(cartId) { Total = 5, Products = ["socks"] },
                    Sent(new ShipOrder(cartId))))
            ], TestContext.Current.CancellationToken);
    }
}

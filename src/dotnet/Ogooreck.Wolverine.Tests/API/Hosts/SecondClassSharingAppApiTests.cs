using Ogooreck.Sample.Wolverine.Events;
using Ogooreck.Wolverine.Tests.Fixtures;
using Xunit;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Wolverine.Tests.API.Hosts;

public class SecondClassSharingAppApiTests(AppApi api): IClassFixture<AppApi>
{
    [Fact]
    public async Task TwoTestClasses_ShareTheRecordingConfiguredInAppApi()
    {
        var cartId = Guid.NewGuid();

        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectPublished(new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })],
                TestContext.Current.CancellationToken);

        Assert.Equal(1, api.ConfigurationRuns);
    }
}

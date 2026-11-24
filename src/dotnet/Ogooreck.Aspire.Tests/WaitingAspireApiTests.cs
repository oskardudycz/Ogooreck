using Ogooreck.API;
using Xunit;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Aspire.Tests.ShopRequests;

namespace Ogooreck.Aspire.Tests;

[Collection("Aspire apps")]
public class WaitingAspireApiTests(WaitingAspireApi api)
{
    [Fact]
    public async Task WaitForMetricsExport_MakesTheBaselineIncludeTheGivenStepsIncrement()
    {
        var cartId = Guid.NewGuid();
        var tags = new Dictionary<string, string> { ["cart.id"] = cartId.ToString() };

        await api.Given(AddProduct(cartId, "socks"))
            .When(AddProduct(cartId, "shoes"))
            .Then([ExpectMetric("cart.products.added", tags, 1)], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PlacingAnOrder_ExportsTheRequestDurationHistogramWithItsCount()
    {
        var cartId = Guid.NewGuid();
        var route = new Dictionary<string, string> { ["http.route"] = "/carts/{id}/orders" };

        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectMetric("http.server.request.duration", route, 1)], TestContext.Current.CancellationToken);
    }
}

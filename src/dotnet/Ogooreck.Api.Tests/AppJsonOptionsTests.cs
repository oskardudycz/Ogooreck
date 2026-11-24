using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests;

public class AppJsonOptionsTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    [Fact]
    public async Task Body_MatchesEnumSentAsString_UsingTheAppsJsonOptions()
    {
        var cartId = Guid.NewGuid();

        await api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
            .When(GetOrder(cartId))
            .Then([ExpectResponse(OK,
                Body(new { status = OrderStatus.Placed }),
                Satisfying(async response =>
                    Assert.Contains("\"status\":\"Placed\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))))], TestContext.Current.CancellationToken);
    }
}

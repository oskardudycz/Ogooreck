using System.Collections.Concurrent;
using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests;

public class OneHostPerClassTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private static readonly ConcurrentBag<string> HostInstances = [];

    [Fact]
    public Task GetCartAfterAddingProduct_ReturnsTheProduct() => GetCartAfterAddingProduct_UsesTheClassHost();

    [Fact]
    public Task GetCartAfterAddingProduct_ReturnsTheProduct_WithTheSameHost() =>
        GetCartAfterAddingProduct_UsesTheClassHost();

    private async Task GetCartAfterAddingProduct_UsesTheClassHost()
    {
        var cartId = Guid.NewGuid();

        await api.Given(AddProduct(cartId, "socks"))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { products = new[] { "socks" } }))], TestContext.Current.CancellationToken);
        await api.Given()
            .When(Request(client => client.GetAsync("/instance", TestContext.Current.CancellationToken)))
            .Then([ExpectResponse(OK,
                Satisfying(async response => HostInstances.Add(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))))], TestContext.Current.CancellationToken);

        Assert.Single(HostInstances.Distinct());
    }
}

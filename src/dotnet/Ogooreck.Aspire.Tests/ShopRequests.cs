using System.Net.Http.Json;
using Ogooreck.API;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Aspire.Tests;

public static class ShopRequests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TestStep AddProduct(Guid cartId, string product) =>
        Request(client => client.PostAsJsonAsync($"/carts/{cartId}/products", new { product }, Ct));

    public static TestStep PlaceOrder(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/orders", null, Ct));

    public static TestStep Echo() => Request(client => client.GetAsync("/echo", Ct));
}

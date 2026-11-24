using System.Net.Http.Json;
using Ogooreck.API;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Marten.Tests;

public static class ShopRequests
{
    public static TestStep AddProduct(Guid cartId, string product) =>
        Request(client => client.PostAsJsonAsync($"/carts/{cartId}/products", new { product },
            TestContext.Current.CancellationToken));

    public static TestStep GetCart(Guid cartId) =>
        Request(client => client.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken));

    public static TestStep PlaceOrder(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/orders", null, TestContext.Current.CancellationToken));

    public static TestStep ShipOrder(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/shipment", null, TestContext.Current.CancellationToken));
}

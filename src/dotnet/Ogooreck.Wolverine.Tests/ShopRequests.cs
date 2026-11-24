using System.Net.Http.Json;
using Ogooreck.API;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Wolverine.Tests;

public static class ShopRequests
{
    public static TestStep AddProduct(Guid cartId, string product) =>
        Request(client => client.PostAsJsonAsync($"/carts/{cartId}/products", new { product },
            TestContext.Current.CancellationToken));

    public static TestStep GetCart(Guid cartId) =>
        Request(client => client.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken));

    public static TestStep PlaceOrder(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/orders", null, TestContext.Current.CancellationToken));

    public static TestStep CheckOut(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/checkout", null, TestContext.Current.CancellationToken));

    public static TestStep CheckStock(string product) =>
        Request(client => client.PostAsync($"/stock/{product}/checks", null, TestContext.Current.CancellationToken));

    public static TestStep StartHeartbeat(Guid id) =>
        Request(client => client.PostAsync($"/heartbeats/{id}", null, TestContext.Current.CancellationToken));

    public static TestStep HoldLateArrival(Guid cartId) =>
        Request(client => client.PostAsync($"/late-arrivals/{cartId}", null, TestContext.Current.CancellationToken));

    public static TestStep GetOrder(Guid orderId) =>
        Request(client => client.GetAsync($"/orders/{orderId}", TestContext.Current.CancellationToken));

    public static TestStep GetInstance() =>
        Request(client => client.GetAsync("/instance", TestContext.Current.CancellationToken));
}

using System.Net.Http.Json;
using Ogooreck.API;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests;

public static class ShopRequests
{
    public static TestStep CreateCart() =>
        Request(client => client.PostAsync("/carts", null, TestContext.Current.CancellationToken));

    public static TestStep AddProduct(Guid cartId, string product) =>
        Request(client => client.PostAsJsonAsync($"/carts/{cartId}/products", new { product }, TestContext.Current.CancellationToken));

    public static TestStep ConfirmCart(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/confirmation", null, TestContext.Current.CancellationToken));

    public static TestStep GetCart(Guid cartId) =>
        Request(client => client.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken));

    public static TestStep PlaceOrder(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/orders", null, TestContext.Current.CancellationToken));

    public static TestStep PlaceOrder(Guid cartId, string correlationId) =>
        Request(client =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/carts/{cartId}/orders");
            request.Headers.Add("X-Correlation-Id", correlationId);
            return client.SendAsync(request, TestContext.Current.CancellationToken);
        });

    public static TestStep ExpireCart(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/expiry", null, TestContext.Current.CancellationToken));

    public static TestStep GetOrderExport(Guid orderId) =>
        Request(client => client.GetAsync($"/orders/{orderId}/export", TestContext.Current.CancellationToken));

    public static TestStep CheckOut(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/checkout", null, TestContext.Current.CancellationToken));

    public static TestStep GetOrder(Guid orderId) =>
        Request(client => client.GetAsync($"/orders/{orderId}", TestContext.Current.CancellationToken));

    public static TestStep GetOrderPage(Guid orderId) =>
        Request(client => client.GetAsync($"/orders/{orderId}/page", TestContext.Current.CancellationToken));

    public static TestStep GetTraceparent() =>
        Request(client => client.GetAsync("/traceparent", TestContext.Current.CancellationToken));
}

using Ogooreck.API;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Kafka.Tests.Fixtures;

public static class ShopRequests
{
    public static TestStep GetCart(Guid cartId) =>
        Request(client => client.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken));

    public static TestStep CheckOut(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/checkout", null, TestContext.Current.CancellationToken));

    public static TestStep PlaceOrderToShip(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/placed-orders", null, TestContext.Current.CancellationToken));

    public static TestStep PlaceOrder(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/orders", null, TestContext.Current.CancellationToken));

    public static TestStep ShipOrder(Guid cartId) =>
        Request(client => client.PostAsync($"/carts/{cartId}/shipping", null, TestContext.Current.CancellationToken));

    public static TestStep CheckStock(string product) =>
        Request(client => client.PostAsync($"/stock/{product}/checks", null, TestContext.Current.CancellationToken));
}

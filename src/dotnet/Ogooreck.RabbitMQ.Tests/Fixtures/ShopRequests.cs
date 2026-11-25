using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using RabbitMQ.Client;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.RabbitMQ.Tests.Fixtures;

public static class ShopRequests
{
    public static TestStep PlaceOrder(Guid orderId) =>
        Request(client => client.PostAsJsonAsync("/orders", new { orderId }, TestContext.Current.CancellationToken));

    public static TestStep PlaceOrders(IReadOnlyList<Guid> orderIds) =>
        Request(async client =>
        {
            HttpResponseMessage? last = null;
            foreach (var orderId in orderIds)
                last = await client.PostAsJsonAsync("/orders", new { orderId }, TestContext.Current.CancellationToken);
            return last!;
        });

    public static TestStep GetOrder(Guid orderId) =>
        Request(client => client.GetAsync($"/orders/{orderId}", TestContext.Current.CancellationToken));

    public static TestStep PublishToTheDefaultExchange(RabbitMQApi shop, string routingKey) =>
        Request(async client =>
        {
            var connection = shop.Services.Provider.GetRequiredService<IConnection>();
            await using var channel = await connection.CreateChannelAsync(
                cancellationToken: TestContext.Current.CancellationToken);
            await channel.BasicPublishAsync(string.Empty, routingKey, Encoding.UTF8.GetBytes("{}"),
                TestContext.Current.CancellationToken);
            return await client.GetAsync("/shipping", TestContext.Current.CancellationToken);
        });
}

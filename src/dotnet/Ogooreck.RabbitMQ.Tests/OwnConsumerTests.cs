using Ogooreck.Assertions;
using Ogooreck.RabbitMQ.Tests.Fixtures;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.RabbitMQ.Tests.Fixtures.ShopRequests;

namespace Ogooreck.RabbitMQ.Tests;

public class OwnConsumerTests(RabbitMQBroker broker)
{
    private readonly Guid orderId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SecondConsumerOutsideDI_FailsNamingTheQueueAndCounts()
    {
        await using var shop = new RabbitMQApi(broker,
            AppSettings.Own() with { ConsumeShippingOutsideTheContainer = true });

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given().When(GetOrder(orderId)).Then([ExpectResponse(NotFound)], ct));

        Assert.Contains(
            $"Queue {shop.Queues.ShippingQueue} has 2 consumers, but Ogooreck decorates 1, so another consumer can take the probe. Resolve the consumer from the app's service provider",
            error.Message);
        Assert.Contains(".CatchingUpWith(...)", error.Message);
    }
}

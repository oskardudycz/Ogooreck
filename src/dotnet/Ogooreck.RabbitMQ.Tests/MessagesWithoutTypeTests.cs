using Ogooreck.RabbitMQ.Tests.Fixtures;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.RabbitMQ.Tests.Fixtures.ShopRequests;

namespace Ogooreck.RabbitMQ.Tests;

public class MessagesWithoutTypeTests(RabbitMQBroker broker)
{
    private readonly Guid orderId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task MessageWithoutHeaders_IsRejectedByTheApp_AndCatchUpEnds()
    {
        await broker.App.Given(PublishToTheDefaultExchange(broker.App, broker.App.Queues.ShippingQueue))
            .When(GetOrder(orderId))
            .Then([ExpectResponse(NotFound), ExpectNothingPublishedIn(Messaging.RabbitMQ.Published)], ct);
    }
}

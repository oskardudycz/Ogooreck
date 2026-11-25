using Ogooreck.Kafka.Tests.Fixtures;
using Ogooreck.Sample.Kafka.Carts;
using Ogooreck.Sample.Kafka.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Kafka.Tests.Consuming;

public class CausationTests(KafkaBroker broker)
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExpectCausation_WithoutCatchUp_PassesOnceTheCausedMessageArrives()
    {
        var shop = broker.PublishOnly;

        await shop.Given()
            .When(ShipOrder(cartId))
            .Then([
                ExpectResponse(NoContent, Satisfying(_ =>
                {
                    HeldMessages.Release(cartId);
                    return Task.CompletedTask;
                })),
                ExpectCausation(PublishedIn(broker.Consuming.Published, new OrderPlaced(cartId),
                    SentIn(broker.Consuming.Published, new ShipOrder(cartId))))
            ], ct);
    }
}

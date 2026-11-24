using Ogooreck.Assertions;
using Ogooreck.Sample.Kafka;
using Ogooreck.Sample.Kafka.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.ShopRequests;

namespace Ogooreck.Kafka.Tests;

public class BlockedHandlerTests(KafkaBroker broker)
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task BlockedHandler_FailsCatchUpAtTheDeadline_NamingTopicPartitionPositionAndEndOffset()
    {
        var app = AppSettings.Own(await broker.NewTopic());
        await using (var warmUp = new KafkaApi(broker, app, Messaging.Kafka))
            await warmUp.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct);
        app = app with { BlockHandler = true, Deadline = TimeSpan.FromSeconds(2) };
        var faults = new ShippingFaults();
        await using var shop = new KafkaApi(broker, app, Messaging.Kafka, new SharedFaultsPlug(faults));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given()
                .When(Request(async client =>
                {
                    var response = await client.PostAsync($"/carts/{cartId}/placed-orders", null, ct);
                    await faults.HandlerBlocked.WaitAsync(ct);
                    return response;
                }))
                .Then([ExpectPublished(new OrderPlaced(cartId))], ct));

        Assert.Contains($"topics [{app.Topic}]", error.Message);
        Assert.Matches(@"partition \d+ position \w+, end offset 1", error.Message);
    }
}

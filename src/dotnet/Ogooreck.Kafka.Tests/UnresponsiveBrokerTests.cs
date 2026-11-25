using Ogooreck.Assertions;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.ShopRequests;

namespace Ogooreck.Kafka.Tests;

public class UnresponsiveBrokerTests
{
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CatchUp_WhenTheBrokerStopsAnswering_FailsSayingTheBrokerDidNotAnswer()
    {
        await using var broker = new KafkaBroker();
        await broker.InitializeAsync();
        await broker.App.Given().When(GetCart(Guid.NewGuid())).Then([ExpectResponse(OK)], ct);
        await using var shop = broker.App.With(spec => spec.Deadline(TimeSpan.FromSeconds(2)));

        try
        {
            var error = await Assert.ThrowsAsync<AssertionException>(() =>
                shop.Given().When(Action(_ => broker.StopAnswering())).Then([], ct));

            Assert.Contains("The broker did not answer within 1.8 seconds.", error.Message);
        }
        finally
        {
            await broker.AnswerAgain();
        }
    }
}

using System.Text;
using Confluent.Kafka;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Kafka.Tests.Fixtures;
using Ogooreck.Sample.Kafka.Carts;
using Ogooreck.Sample.Kafka.Core;
using Ogooreck.Sample.Kafka.Events;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Kafka.Tests.Consuming;

public class PublishedMessagesTests(KafkaBroker broker)
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_PassesAsSoonAsTheExpectedMessageArrives_WhileALaterOneIsHeld()
    {
        var shop = broker.PublishOnly;

        await shop.Given()
            .When(PlaceOrder(cartId))
            .Then([ExpectPublishedIn(broker.Consuming.Published, new OrderPlaced(cartId))], ct);

        HeldMessages.Release(cartId);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_MatchesASubsequenceWithGapsBetweenMessages()
    {
        var shop = broker.PublishOnly;

        await shop.Given()
            .When(CheckOut(cartId))
            .Then([ExpectPublishedIn(broker.Consuming.Published, new OrderPlaced(cartId), new ShipOrder(cartId))], ct);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_FailsAtTheDeadline_WhenTheOrderDiffers()
    {
        var clock = new PassingLaterClock();
        await using var shop = broker.PublishOnly.With(spec => spec.Clock(clock).Deadline(TimeSpan.FromSeconds(2)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given()
                .When(CheckOut(cartId))
                .Then([
                    ExpectPublishedIn(broker.Consuming.Published, new OrderPlaced(cartId), new CartClosed(cartId), new ShipOrder(cartId)),
                    LetTheDeadlinePass(clock),
                    ExpectPublishedIn(broker.Consuming.Published, new CartClosed(cartId), new OrderPlaced(cartId))
                ], ct));

        Assert.Contains("1 of 2 expected messages arrived in order; message 2 (OrderPlaced) did not arrive", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_FailsAtTheDeadlineListingTheSeenMessages()
    {
        var clock = new PassingLaterClock();
        await using var shop = broker.PublishOnly.With(spec => spec.Clock(clock).Deadline(TimeSpan.FromSeconds(2)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given()
                .When(CheckOut(cartId))
                .Then([
                    ExpectPublishedIn(broker.Consuming.Published, new OrderPlaced(cartId), new CartClosed(cartId), new ShipOrder(cartId)),
                    LetTheDeadlinePass(clock),
                    ExpectPublishedIn(broker.Consuming.Published, new OrderPaid(cartId))
                ], ct));

        Assert.Contains("message 1 (OrderPaid) did not arrive", error.Message);
        Assert.Matches(@"Published by the When step:\s+1\. OrderPlaced \{[^\n]*\}\s+2\. CartClosed \{[^\n]*\}\s+3\. ShipOrder \{", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_WhenNothingWasPublished_FailsAtTheDeadlineListingNone()
    {
        await using var shop = broker.PublishOnly.With(spec => spec.Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(2)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given()
                .When(GetCart(cartId))
                .Then([ExpectPublishedIn(broker.Consuming.Published, new OrderPlaced(cartId))], ct));

        Assert.Matches(@"Published by the When step:\s+\(none\)", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_IgnoresAnotherTestsMessageOnTheSameTopic()
    {
        var testA = broker.PublishOnly;
        await using var testB = broker.PublishOnly.With(spec => spec.Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(2)));
        var product = $"socks-{cartId}";

        await testA.Given()
            .When(CheckStock(product))
            .Then([ExpectPublishedIn(broker.Consuming.Published, new StockChecked(product))], ct);
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            testB.Given()
                .When(GetCart(cartId))
                .Then([ExpectPublishedIn(broker.Consuming.Published, new StockChecked(product))], ct));
        Assert.Contains("0 of 1 expected messages arrived in order; message 1 (StockChecked) did not arrive", error.Message);
        Assert.Matches(@"Published by the When step:\s+\(none\)", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_IgnoresMessagesWithAnotherTraceId()
    {
        var clock = new PassingLaterClock();
        await using var shop = broker.PublishOnly.With(spec => spec.Clock(clock).Deadline(TimeSpan.FromSeconds(2)));
        var product = $"socks-{cartId}";

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given(Action(_ =>
                    PublishWithTraceparent(product, new OrderPlaced(cartId), "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01")))
                .When(CheckStock(product))
                .Then([
                    ExpectPublishedIn(broker.Consuming.Published, new StockChecked(product)),
                    LetTheDeadlinePass(clock),
                    ExpectPublishedIn(broker.Consuming.Published, new OrderPlaced(cartId))
                ], ct));

        Assert.Matches(@"Published by the When step:\s+1\. StockChecked \{[^\n]*\}\s*$", error.Message);
    }

    [Fact]
    public async Task Then_Cancelled_EndsWithOperationCanceledException()
    {
        var shop = broker.PublishOnly;
        using var cancelling = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var running = shop.Given()
            .When(Request(async client =>
            {
                var response = await client.GetAsync($"/carts/{cartId}", ct);
                await cancelling.CancelAsync();
                return response;
            }))
            .Then([ExpectPublishedIn(broker.Consuming.Published, new OrderPaid(cartId))], cancelling.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => running);
    }

    private static Expectation LetTheDeadlinePass(PassingLaterClock clock) =>
        ExpectResponse(NoContent, Satisfying(_ =>
        {
            clock.PassFromNow();
            return Task.CompletedTask;
        }));

    private async Task PublishWithTraceparent(string key, object message, string traceparent)
    {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = broker.BootstrapServers
        }).Build();
        await producer.ProduceAsync(KafkaOptions.DefaultTopic, new Message<string, string>
        {
            Key = key,
            Value = System.Text.Json.JsonSerializer.Serialize(message, message.GetType(), System.Text.Json.JsonSerializerOptions.Web),
            Headers = new Headers
            {
                { "traceparent", Encoding.UTF8.GetBytes(traceparent) },
                { "messageType", Encoding.UTF8.GetBytes(message.GetType().FullName!) }
            }
        }, ct);
    }
}

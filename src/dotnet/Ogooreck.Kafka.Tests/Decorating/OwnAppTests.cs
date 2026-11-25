using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Ogooreck.Assertions;
using Ogooreck.Kafka;
using Ogooreck.Kafka.Tests.Fixtures;
using Ogooreck.Sample.Kafka.Core;
using Ogooreck.Sample.Kafka.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Kafka.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Kafka.Tests.Decorating;

public class OwnAppTests(KafkaBroker broker)
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Decorating_RecordsTheProducedMessage()
    {
        await broker.PublishOnly.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task Decorating_ProducerOnlyApp_Starts()
    {
        await using var shop = new KafkaApi(broker, AppSettings.WithoutShipping(),
            new WithoutServicePlugin<IConsumer<string, string>>(), Messaging.Kafka);

        await shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task Decorating_ConsumerOnlyApp_Starts()
    {
        await using var shop = new KafkaApi(broker, AppSettings.WithoutShipping(),
            new WithoutServicePlugin<IProducer<string, string>>(), new WithoutServicePlugin<EventPublisher>(), Messaging.Kafka);

        await shop.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct);
    }

    [Fact]
    public async Task Decorating_TracesItsWaitsInsideTheStartAndTheCatchUp()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Ogooreck" or "Ogooreck.Kafka",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue
        };
        ActivitySource.AddActivityListener(listener);
        await using var shop = new KafkaApi(broker, AppSettings.Own(await broker.NewTopic()), Messaging.Kafka);

        await shop.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct);

        var run = stopped.Single(span => span.DisplayName == $"When request GET /carts/{cartId}");
        var names = stopped.Where(span => span.TraceId == run.TraceId).ToDictionary(span => span.SpanId, span => span.DisplayName);
        Assert.Equal(
        [
            "start plugin KafkaPlugin`2 < Kafka wait for the consumers' assignment and fetching",
            "start plugin KafkaPlugin`2 < Kafka read the end offsets",
            "start plugin KafkaPlugin`2 < Kafka check the consumer groups for other members",
            "catch-up < Kafka check the consumer groups for other members",
            "catch-up < Kafka read the end offsets",
            "catch-up < Kafka wait for the consumers to catch up"
        ], stopped.Where(span => span.TraceId == run.TraceId && span.Source.Name == "Ogooreck.Kafka" && span.Parent?.Source.Name == "Ogooreck")
            .OrderBy(span => span.StartTimeUtc).Select(span => $"{names[span.ParentSpanId]} < {span.DisplayName}").Take(6));
        Assert.Contains("has no partitions assigned",
            stopped.Single(span => span.TraceId == run.TraceId && span.DisplayName == "Kafka wait for the consumers' assignment and fetching").Events.First().Name);
    }

    [Fact]
    public async Task Decorating_TracesEachBrokerRequestInsideTheWaitThatMadeIt()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Ogooreck" or "Ogooreck.Kafka",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue
        };
        ActivitySource.AddActivityListener(listener);
        await using var shop = new KafkaApi(broker, AppSettings.Own(await broker.NewTopic()), Messaging.Kafka);

        await shop.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct);

        var run = stopped.Single(span => span.DisplayName == $"When request GET /carts/{cartId}");
        var requests = stopped.Where(span => span.TraceId == run.TraceId && span.Parent?.Source.Name == "Ogooreck.Kafka")
            .Select(span => $"{span.Parent!.DisplayName} < {span.DisplayName}").ToHashSet();
        Assert.Superset(new HashSet<string>
        {
            "Kafka wait for the consumers' assignment and fetching < Kafka ListConsumerGroups",
            "Kafka wait for the consumers' assignment and fetching < Kafka DescribeConsumerGroups",
            "Kafka read the end offsets < Kafka ListOffsets",
            "Kafka check the consumer groups for other members < Kafka ListConsumerGroups",
            "Kafka check the consumer groups for other members < Kafka DescribeConsumerGroups"
        }, requests);
    }

    [Fact]
    public async Task Decorating_AppWithNeitherProducerNorConsumer_FailsNamingBothTypes()
    {
        await using var shop = new KafkaApi(broker, AppSettings.WithoutShipping(),
            new WithoutServicePlugin<IProducer<string, string>>(), new WithoutServicePlugin<IConsumer<string, string>>(),
            new WithoutServicePlugin<EventPublisher>(), Messaging.Kafka);

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            shop.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct));

        Assert.Contains(typeof(IProducer<string, string>).ToString(), error.ToString());
        Assert.Contains(typeof(IConsumer<string, string>).ToString(), error.ToString());
    }

    [Fact]
    public async Task Decorating_FailedProduce_RecordsNothing()
    {
        var app = AppSettings.WithoutShipping() with { Topic = "not a valid topic name" };
        await using var shop = new KafkaApi(broker, app, Messaging.Kafka);

        await shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectResponse(InternalServerError), ExpectNothingPublishedIn(Messaging.Kafka.Published)], ct);
    }

    [Fact]
    public async Task FirstStepWaitsForTheConsumerAssignment_WithOlderMessagesOnTheTopic()
    {
        var app = AppSettings.Own(await broker.NewTopic());
        await broker.ProduceOlderOrders(app.Topic);
        await using var shop = new KafkaApi(broker, app, Messaging.Kafka);

        await shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId), new ShipOrder(cartId))], ct);
        await shop.Given()
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { cartId, shipped = true }))], ct);
    }

    [Fact]
    public async Task AnotherConsumerInTheShippingGroup_NotResolvedFromDI_FailsTheFirstStep_NamingTheGroupAndTheMember()
    {
        var shipping = AppSettings.Own(await broker.NewTopic());
        var app = shipping with { BillingGroupId = shipping.ShippingGroupId };
        await using var shop = new KafkaApi(broker, app, Messaging.Kafka);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct));

        Assert.Contains($"Consumer group {app.ShippingGroupId} has a member (client billing-worker, member id ", error.Message);
        Assert.Contains("Resolve that consumer from DI", error.Message);
        Assert.Contains(".CatchingUpWith(...)", error.Message);
    }

    [Fact]
    public async Task AConsumerOutsideDI_InAGroupOfItsOwn_DoesNotFailTheStep()
    {
        var app = AppSettings.Own(await broker.NewTopic()) with { BillingGroupId = $"billing-{Guid.NewGuid()}" };
        await using var shop = new KafkaApi(broker, app, Messaging.Kafka);

        await shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId), new ShipOrder(cartId))], ct);
    }

    [Fact]
    public async Task CatchingUpWith_ReplacesTheConsumerCatchUpAndTheCheck()
    {
        var shipping = AppSettings.Own(await broker.NewTopic());
        var app = shipping with { BillingGroupId = shipping.ShippingGroupId };
        var waits = 0;
        await using var shop = new KafkaApi(broker, app,
            Messaging.Kafka.CatchingUpWith(_ =>
            {
                Interlocked.Increment(ref waits);
                return Task.CompletedTask;
            }));

        await shop.Given().When(GetCart(cartId)).Then([ExpectResponse(OK)], ct);

        Assert.Equal(1, waits);
    }

    [Fact]
    public async Task CatchingUpWith_CalledTwice_RunsBothWaitsInOrder()
    {
        var waits = new List<string>();
        await using var shop = broker.PublishOnly.With(spec => spec.Use(Messaging.Kafka
                .CatchingUpWith(_ =>
                {
                    lock (waits)
                        waits.Add("first");
                    return Task.CompletedTask;
                })
                .CatchingUpWith(_ =>
                {
                    lock (waits)
                        waits.Add("second");
                    return Task.CompletedTask;
                })));

        await shop.Given()
            .When(PlaceOrderToShip(cartId))
            .Then([ExpectPublishedIn(Messaging.Kafka.Published, new OrderPlaced(cartId))], ct);

        Assert.NotEmpty(waits);
        Assert.All(waits.Chunk(2), round => Assert.Equal(["first", "second"], round));
    }
}

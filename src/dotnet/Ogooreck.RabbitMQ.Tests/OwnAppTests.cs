using Microsoft.Extensions.DependencyInjection;
using Ogooreck.Assertions;
using Ogooreck.Sample.RabbitMQ;
using Ogooreck.Sample.RabbitMQ.Events;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.RabbitMQ.Tests.ShopRequests;

namespace Ogooreck.RabbitMQ.Tests;

public class OwnAppTests(RabbitMQBroker broker)
{
    private readonly Guid orderId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConcurrencyFour_AllTwentyOrdersAreShipped_WhenCatchUpEnds()
    {
        var orderIds = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList();
        await using var app = new RabbitMQApi(broker, AppSettings.Own() with { DispatchConcurrency = 4 });
        foreach (var id in orderIds)
            app.Faults.HoldShippingOf(id);
        await using var shop = app.With(spec => spec.CatchUp(async (step, token) =>
        {
            await step(token);
            foreach (var id in orderIds)
                app.Faults.ReleaseShippingOf(id);
        }));

        await shop.Given()
            .When(PlaceOrders(orderIds))
            .Then([ExpectResponse(Accepted)], ct);

        Assert.Equal(20, app.Services.Provider.GetRequiredService<Orders>().ShippedCount);
    }

    [Fact]
    public async Task Probe_NeverReachesTheAppsHandler()
    {
        var orderIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        await using var shop = new RabbitMQApi(broker, AppSettings.Own());

        await shop.Given()
            .When(PlaceOrders(orderIds))
            .Then([ExpectResponse(Accepted)], ct);

        Assert.Equal(3, shop.Services.Provider.GetRequiredService<Orders>().Handled);
    }

    [Fact]
    public async Task BlockedHandler_FailsCatchUpAtTheDeadline_NamingTheQueue()
    {
        var clock = new PassingLaterClock();
        await using var app = new RabbitMQApi(broker, AppSettings.Own() with { Deadline = TimeSpan.FromSeconds(2) });
        app.Faults.BlockTheHandlerOf(orderId);
        await using var shop = app.With(spec => spec.Clock(clock).CatchUp(async (step, token) =>
        {
            await step(token);
            await app.Faults.HandlerBlocked.WaitAsync(token);
            clock.PassFromNow();
        }));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given().When(PlaceOrder(orderId)).Then([], ct));

        Assert.Contains($"Queues not caught up: {app.Queues.ShippingQueue}. Handlers running: 1. Requeues pending: 0.",
            error.Message);
    }

    [Fact]
    public async Task AppThatOnlyPublishes_RecordsTheMessage_AndCatchUpReturnsAtOnce()
    {
        await using var shop = new RabbitMQApi(broker,
            AppSettings.Own() with { ShipOrders = false, Deadline = TimeSpan.FromSeconds(2) });

        await shop.Given(Attempt(GetOrder(orderId)))
            .When(PlaceOrder(orderId))
            .Then([ExpectPublished(new OrderPlaced(orderId))], ct);
    }

    [Fact]
    public async Task ConsumerWithAutoAck_ExactCheckStillPasses()
    {
        await using var shop = new RabbitMQApi(broker, AppSettings.Own() with { AutoAck = true });

        await shop.Given()
            .When(PlaceOrder(orderId))
            .Then([ExpectPublished(new OrderPlaced(orderId), new ShipOrder(orderId))], ct);
    }
}

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Sample.Wolverine.Carts.Expiring;
using Ogooreck.Sample.Wolverine.Events;
using Ogooreck.Sample.Wolverine.Orders.Paying;
using Ogooreck.Wolverine.Tests.Fixtures;
using Wolverine;
using Xunit;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Wolverine.Tests.API.Steps;

public class ActionAndPublishTests(AppApi api, BareApp app)
{
    private const string W3CTraceparent = "^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$";

    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public async Task ActionAdvancingAFakeClock_ExpiresTheCartAndPublishesCartExpired()
    {
        await using var shop = new AppApi();

        await shop.Given(AddProduct(cartId, "socks"))
            .When(Action(_ =>
            {
                shop.AppClock.Advance(TimeSpan.FromDays(1));
                return Task.CompletedTask;
            }))
            .Then([ExpectPublished(new CartExpired(cartId))], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ActionContext_ExposesAValidTraceparentUniquePerStep()
    {
        var traceparents = new List<string>();

        await api.Given(Action(ctx =>
            {
                traceparents.Add(ctx.Traceparent);
                return Task.CompletedTask;
            }))
            .When(Action(ctx =>
            {
                traceparents.Add(ctx.Traceparent);
                return Task.CompletedTask;
            }))
            .Then([], TestContext.Current.CancellationToken);

        Assert.All(traceparents, traceparent => Assert.Matches(W3CTraceparent, traceparent));
        Assert.NotEqual(traceparents[0].Split('-')[1], traceparents[1].Split('-')[1]);
    }

    [Fact]
    public Task MessageFromAnActionActivityParentedOnTheStepTraceparent_IsAttributedToTheWhenStep() =>
        api.Given()
            .When(Action(async ctx =>
            {
                using var activity = new Activity("expire cart").SetParentId(ctx.Traceparent).Start();
                using var scope = api.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new ExpireCart(cartId));
            }))
            .Then([ExpectPublished(new CartExpired(cartId))], TestContext.Current.CancellationToken);

    [Fact]
    public Task PublishStep_PutsAMessageOnTheBusCarryingTheTraceparent_AndTheAppReacts()
    {
        var orderId = Guid.NewGuid();

        return api.Given()
            .When(Publish(new PaymentConfirmed(orderId)))
            .Then([ExpectPublished(new OrderPaid(orderId))], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishWithoutAPublishFunction_FailsSayingPublishingNeedsAPublishFunction()
    {
        var spec = app;

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Publish(new PaymentConfirmed(Guid.NewGuid()))).Then([], TestContext.Current.CancellationToken));

        Assert.Equal(
            "When publish: Publish(...) needs a publish function, but none is configured; configure it with Publishing((request, ct) => ...).",
            error.Message);
    }

    [Fact]
    public async Task PublishStep_InGiven_RunsBeforeTheWhenStepWithItsOwnTraceId()
    {
        var events = new List<string>();
        await using var spec = app.With(setup => setup.Publishing((request, _) =>
        {
            events.Add($"published with {request.Traceparent}");
            return Task.CompletedTask;
        }));

        await spec.Given(Publish("payment"))
            .When(Action(ctx =>
            {
                events.Add($"When started with {ctx.Traceparent}");
                return Task.CompletedTask;
            }))
            .Then([], TestContext.Current.CancellationToken);

        Assert.Equal(2, events.Count);
        Assert.StartsWith("published with ", events[0]);
        Assert.StartsWith("When started with ", events[1]);
        Assert.NotEqual(events[0].Split('-')[1], events[1].Split('-')[1]);
    }

    [Fact]
    public async Task PublishStep_PassesAllMessagesInOrder()
    {
        PublishRequest? published = null;
        await using var spec = app.With(setup => setup.Publishing((request, _) =>
        {
            published = request;
            return Task.CompletedTask;
        }));

        await spec.Given().When(Publish("a", "b", "c")).Then([], TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b", "c"], published!.Messages);
        Assert.Matches(W3CTraceparent, published.Traceparent);
    }

    [Fact]
    public async Task PublishFunctionThatThrows_FailsTheTestWithItsException()
    {
        await using var spec = app.With(setup => setup
            .Publishing((_, _) => throw new TimeZoneNotFoundException("the bus is down")));

        var error = await Assert.ThrowsAsync<TimeZoneNotFoundException>(() =>
            spec.Given().When(Publish("payment")).Then([], TestContext.Current.CancellationToken));

        Assert.Equal("the bus is down", error.Message);
    }

    [Fact]
    public async Task ActionThatThrows_FailsTheTestWithItsException_AndLaterStepsDoNotRun()
    {
        var laterStepRan = false;
        var spec = app;

        var error = await Assert.ThrowsAsync<TimeZoneNotFoundException>(() =>
            spec.Given(Action(_ => throw new TimeZoneNotFoundException("the job failed")))
                .When(Action(_ =>
                {
                    laterStepRan = true;
                    return Task.CompletedTask;
                }))
                .Then([], TestContext.Current.CancellationToken));

        Assert.Equal("the job failed", error.Message);
        Assert.False(laterStepRan);
    }
}

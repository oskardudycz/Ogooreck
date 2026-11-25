using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Sample.Wolverine.Carts.GettingCart;
using Ogooreck.Testing;
using Ogooreck.Wolverine.Tests.Fixtures;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.Fixtures.ShopRequests;

namespace Ogooreck.Wolverine.Tests.API.Running;

public class CatchUpTests(AppApi api, BareApp app)
{
    private static readonly TimeSpan TwoSeconds = TimeSpan.FromSeconds(2);

    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public async Task CatchUpFunction_MakesTheNextStepSeeTheProjection_WithoutEventually()
    {
        var requests = 0;
        await using var spec = app.With(setup => setup
            .CatchUp(ct => app.Services.GetRequiredService<CartViews>().WaitForNonStaleDataAsync(ct)));

        await spec.Given(AddProduct(cartId, "socks"))
            .When(Request(client =>
            {
                requests++;
                return client.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken);
            }))
            .Then([ExpectResponse(OK, Body(new { products = new[] { "socks" } }))], TestContext.Current.CancellationToken);

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task CatchUp_RepeatsUntilARoundAddsNoItems_BeforeTheNextStep()
    {
        var chain = new Recorded<string>();
        var events = new List<string>();
        var calls = 0;
        await using var spec = app.With(setup => setup
            .Recording(chain, link => new Observed(link, null), _ =>
            {
                calls++;
                events.Add($"catch-up {calls}");
                if (calls <= 3)
                    chain.Add($"follow-up of link {calls}");
                return Task.CompletedTask;
            }));

        await spec.Given(Action(_ =>
            {
                chain.Add("link 1");
                return Task.CompletedTask;
            }))
            .When(Action(_ =>
            {
                events.Add("When started");
                return Task.CompletedTask;
            }))
            .Then([], TestContext.Current.CancellationToken);

        Assert.Equal(["catch-up 1", "catch-up 2", "catch-up 3", "catch-up 4", "When started", "catch-up 5"], events);
    }

    [Fact]
    public async Task AppThatKeepsPublishing_FailsAtTheDeadlineSayingSo()
    {
        await using var spec = AppApi.WithDeadline(TwoSeconds);
        spec.Clock(new PassingClock());
        var heartbeatId = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(StartHeartbeat(heartbeatId))
                .Then([ExpectNothingPublished()], TestContext.Current.CancellationToken));

        Assert.StartsWith(
            $"The application kept producing messages for longer than 00:00:02 after the When request POST /heartbeats/{heartbeatId}.",
            error.Message);
    }

    [Fact]
    public async Task NoCatchUpFunction_RunsNextStepWithoutDelay()
    {
        await using var spec = app.With(setup => setup.Clock(new FakeTimeProvider()));

        await spec.Given(Action(_ => Task.CompletedTask))
            .When(Action(_ => Task.CompletedTask))
            .Then([], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CatchUp_WithoutRecorders_RunsTheStandaloneCatchUpFunctionOncePerStepWhenNothingChanges()
    {
        var calls = 0;
        await using var spec = app.With(setup => setup.CatchUp(_ =>
        {
            calls++;
            return Task.CompletedTask;
        }));

        await spec.Given(Action(_ => Task.CompletedTask))
            .When(Action(_ => Task.CompletedTask))
            .Then([], TestContext.Current.CancellationToken);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CatchUpFunctionThatThrows_FailsTheTestWithItsException()
    {
        await using var spec = app.With(setup => setup
            .CatchUp(_ => Task.CompletedTask)
            .CatchUp(_ => throw new TimeZoneNotFoundException("the clock is gone")));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(CheckStock("socks")).Then([], TestContext.Current.CancellationToken));

        Assert.Equal("When request POST /stock/socks/checks: catch-up function 2 of 2 failed: the clock is gone", error.Message);
        Assert.IsType<TimeZoneNotFoundException>(error.InnerException);
    }

    [Fact]
    public async Task CatchUpAroundTheStepThatThrows_FailsTheTestWithItsException()
    {
        await using var spec = app.With(setup => setup.CatchUp(async (step, ct) =>
        {
            await step(ct);
            throw new TimeZoneNotFoundException("tracking failed");
        }));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(CheckStock("socks")).Then([], TestContext.Current.CancellationToken));

        Assert.Equal("When request POST /stock/socks/checks: the catch-up around the step failed: tracking failed", error.Message);
        Assert.IsType<TimeZoneNotFoundException>(error.InnerException);
    }

    [Fact]
    public async Task StepThatThrowsInsideTheCatchUpAroundIt_FailsWithTheStepsOwnException()
    {
        await using var spec = app.With(setup => setup.CatchUp((step, ct) => step(ct)));

        var error = await Assert.ThrowsAsync<TimeZoneNotFoundException>(() =>
            spec.Given()
                .When(Action(_ => throw new TimeZoneNotFoundException("the step failed")))
                .Then([], TestContext.Current.CancellationToken));

        Assert.Equal("the step failed", error.Message);
    }

    [Fact]
    public async Task StepSlowerThanTheDeadline_InsideTheCatchUpAroundIt_IsBoundOnlyByTheThenToken()
    {
        var clock = new FakeTimeProvider();
        await using var spec = app.With(setup => setup
            .Clock(clock)
            .Deadline(TimeSpan.FromSeconds(1))
            .CatchUp((step, ct) => step(ct)));

        await spec.Given()
            .When(Action(_ =>
            {
                clock.Advance(TimeSpan.FromSeconds(1.5));
                return Task.CompletedTask;
            }))
            .Then([], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CatchUpAroundTheStepThatSkipsTheStep_FailsSayingItMustCallTheStepOnce()
    {
        await using var spec = app.With(setup => setup.CatchUp((_, _) => Task.CompletedTask));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(CheckStock("socks")).Then([], TestContext.Current.CancellationToken));

        Assert.Equal(
            "When request: the catch-up around the step returned without running the step; it must call the step exactly once.",
            error.Message);
    }

    [Fact]
    public async Task CatchUpAroundTheStepThatCallsTheStepTwice_FailsSayingItMustCallTheStepOnce_AndRunsTheStepOnce()
    {
        var runs = 0;
        await using var spec = app.With(setup => setup.CatchUp(async (step, ct) =>
        {
            await step(ct);
            await step(ct);
        }));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(Action(_ =>
                {
                    runs++;
                    return Task.CompletedTask;
                }))
                .Then([], TestContext.Current.CancellationToken));

        Assert.Equal(
            "When action: the catch-up around the step called the step 2 times; it must call the step exactly once. The step ran once.",
            error.Message);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task OneDeadline_CoversTheCatchUpAroundTheStepAndThePlainCatchUpTogether()
    {
        var clock = new FakeTimeProvider();
        await using var spec = app.With(setup => setup
            .Clock(clock)
            .Deadline(TwoSeconds)
            .CatchUp(async (step, ct) =>
            {
                await step(ct);
                clock.Advance(TimeSpan.FromSeconds(1.5));
            })
            .CatchUp(ct =>
            {
                var waiting = Task.Delay(TimeSpan.FromSeconds(1.5), clock, ct);
                clock.Advance(TimeSpan.FromSeconds(1.5));
                return waiting;
            }));
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Action(_ => Task.CompletedTask)).Then([], TestContext.Current.CancellationToken));

        Assert.StartsWith("The application kept producing messages for longer than 00:00:02 after the When action.",
            error.Message);
    }

    [Fact]
    public async Task PlainCatchUpThatKeepsAddingItems_FailsAtTheDeadlineSayingSo()
    {
        var recorded = new Recorded<int>();
        var clock = new FakeTimeProvider();
        await using var spec = app.With(setup => setup
            .Clock(clock)
            .Deadline(TwoSeconds)
            .Recording(recorded, item => new Observed(item, null), _ =>
            {
                recorded.Add(1);
                clock.Advance(TimeSpan.FromMilliseconds(500));
                return Task.CompletedTask;
            }));
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Action(_ => Task.CompletedTask)).Then([], TestContext.Current.CancellationToken));

        Assert.StartsWith("The application kept producing messages for longer than 00:00:02 after the When action.",
            error.Message);
    }

    [Fact]
    public async Task CancellingThen_WhileTheCatchUpAroundTheStepWaits_NamesTheWait()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var spec = app.With(setup => setup.CatchUp(async (step, ct) =>
        {
            await step(ct);
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
        }));

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            spec.Given().When(CheckStock("socks")).Then([], cancellation.Token));

        Assert.Equal("Then was cancelled while waiting for catch-up after the When request POST /stock/socks/checks.",
            error.Message);
    }

    [Fact]
    public async Task CatchUpFunctionThatNeverReturns_FailsAtTheDeadline()
    {
        var clock = new FakeTimeProvider();
        await using var spec = app.With(setup => setup
            .Clock(clock)
            .Deadline(TimeSpan.FromSeconds(1))
            .CatchUp(_ =>
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                return Task.Delay(Timeout.Infinite, TestContext.Current.CancellationToken);
            }));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Action(_ => Task.CompletedTask)).Then([], TestContext.Current.CancellationToken));

        Assert.Equal("The application kept producing messages for longer than 00:00:01 after the When action.", error.Message);
    }

    [Fact]
    public async Task CancellingThen_DuringCatchUp_NamesTheStepItFollows()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var spec = app.With(setup => setup.CatchUp(ct =>
        {
            cancellation.Cancel();
            return Task.Delay(Timeout.Infinite, ct);
        }));

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            spec.Given(CheckStock("socks")).When(Action(_ => Task.CompletedTask)).Then([], cancellation.Token));

        Assert.Equal("Then was cancelled while waiting for catch-up after the Given step 1 request POST /stock/socks/checks.",
            error.Message);
    }

    [Fact]
    public async Task StepStillRunningAfterItsThenWasCancelled_EndsBeforeTheNextScenarioOnTheAppRunsItsStep()
    {
        var steps = new List<string>();
        var cancelledStepRunning = new TaskCompletionSource();
        var releaseCancelledStep = new TaskCompletionSource();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cancelled = Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.Given()
                .When(Action(async _ =>
                {
                    cancelledStepRunning.SetResult();
                    await releaseCancelledStep.Task;
                    lock (steps)
                        steps.Add("cancelled scenario's step ended");
                }))
                .Then([], cancellation.Token));
        await cancelledStepRunning.Task;
        await cancellation.CancelAsync();
        await cancelled;

        var next = api.Given()
            .When(Action(_ =>
            {
                lock (steps)
                    steps.Add("next scenario's step started");
                return Task.CompletedTask;
            }))
            .Then([], TestContext.Current.CancellationToken);
        releaseCancelledStep.SetResult();
        await next;

        Assert.Equal(["cancelled scenario's step ended", "next scenario's step started"], steps);
    }

    [Fact]
    public async Task GivenStep_UsesTheOneAssemblyHost() =>
        await AssemblyHost.Record(api);
}

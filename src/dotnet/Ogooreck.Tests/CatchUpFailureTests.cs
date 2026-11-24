using Microsoft.Extensions.Time.Testing;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests;

public class CatchUpFailureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public record OrderPlaced(Guid CartId);

    public record ShipOrder(Guid CartId);

    private readonly Guid cartId = Guid.NewGuid();
    private readonly FakeTimeProvider clock = new();
    private readonly Recorded<Observed> recorded = new();

    private ApiSpecification Api(TimeSpan? deadline = null) =>
        For(() => Task.FromResult(new Uri("http://localhost")))
            .Recording(recorded, observed => observed, _ => Task.CompletedTask)
            .Deadline(deadline ?? TimeSpan.FromSeconds(5));

    private TestStep Recording(params Func<string, Observed>[] observed) =>
        Action(context =>
        {
            foreach (var item in observed)
                recorded.Add(item(context.Traceparent));
            return Task.CompletedTask;
        });

    [Fact]
    public async Task ExpectCausation_FailsWhenAMessageOfTheSameTypeCarriesOtherValues()
    {
        await using var api = Api();

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(Recording(traceparent => new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null)))
                .Then([ExpectCausation(PublishedIn(recorded, new OrderPlaced(Guid.NewGuid())))], Ct));

        Assert.Contains("to be published, but the When step published only:", error.Message);
        Assert.Contains($"1. OrderPlaced {{\"cartId\":\"{cartId}\"}} (id m1, correlation c1, causation (none))", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_ListsMessagesThatCarryNoIds()
    {
        await using var api = Api();

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(Recording(traceparent => new Observed(new ShipOrder(cartId), traceparent)))
                .Then([ExpectCausation(PublishedIn(recorded, new OrderPlaced(cartId)))], Ct));

        Assert.Contains("ShipOrder", error.Message);
        Assert.Contains("(id (none), correlation (none), causation (none))", error.Message);
    }

    private Task PassingTheDeadline(CancellationToken ct)
    {
        clock.Advance(TimeSpan.FromMilliseconds(2));
        return Task.CompletedTask;
    }

    private ApiSpecification ApiWithAShortDeadline() =>
        For(() => Task.FromResult(new Uri("http://localhost"))).Clock(clock).Deadline(TimeSpan.FromMilliseconds(1));

    private static TestStep Responding() => Request(_ => Task.FromResult(new HttpResponseMessage(OK)));

    [Fact]
    public async Task ACatchUpThatKeepsProducingMessages_FailsOnceTheDeadlineHasPassed()
    {
        var keepsProducing = new Recorded<Observed>();
        await using var api = ApiWithAShortDeadline()
            .Recording(keepsProducing, observed => observed, async ct =>
            {
                await PassingTheDeadline(ct);
                keepsProducing.Add(new Observed(new OrderPlaced(cartId), null));
            });

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Responding()).Then([ExpectResponse(OK)], Ct));

        Assert.StartsWith("The application kept producing messages for longer than 00:00:00.0010000 after the When request", error.Message);
    }

    [Fact]
    public async Task ACatchUpThatFailsAfterTheDeadline_ReportsThatTheApplicationKeptProducingMessages()
    {
        await using var api = ApiWithAShortDeadline()
            .CatchUp(PassingTheDeadline)
            .CatchUp(_ => throw new InvalidOperationException("broker is gone"));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Responding()).Then([ExpectResponse(OK)], Ct));

        Assert.EndsWith("The catch-up reported: broker is gone", error.Message);
    }

    [Fact]
    public async Task ACatchUpThatFailsBeforeTheDeadline_NamesTheFailedCatchUp()
    {
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .CatchUp(_ => throw new InvalidOperationException("broker is gone"));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Responding()).Then([ExpectResponse(OK)], Ct));

        Assert.Contains("catch-up function 1 of 1 failed: broker is gone", error.Message);
    }

    [Fact]
    public async Task ACatchUpThatNeverReturns_FailsOnceTheDeadlineHasPassed()
    {
        await using var api = ApiWithAShortDeadline().CatchUp(async ct =>
        {
            await PassingTheDeadline(ct);
            await new TaskCompletionSource().Task;
        });

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Responding()).Then([ExpectResponse(OK)], Ct));

        Assert.StartsWith("The application kept producing messages for longer than 00:00:00.0010000 after the When request", error.Message);
    }

    [Fact]
    public async Task ACatchUpAroundTheStep_ThatNeverRunsTheStep_Fails()
    {
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .CatchUp((step, ct) => Task.CompletedTask);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Responding()).Then([ExpectResponse(OK)], Ct));

        Assert.Equal(
            "When request: the catch-up around the step returned without running the step; it must call the step exactly once.",
            error.Message);
    }

    [Fact]
    public async Task ACatchUpAroundTheStep_ThatRunsTheStepTwice_Fails()
    {
        string? seenByTheCatchUp = null;
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .CatchUp(async (step, ct) =>
            {
                await step(ct);
                seenByTheCatchUp = (await Assert.ThrowsAsync<AssertionException>(() => step(ct))).Message;
            });

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Responding()).Then([ExpectResponse(OK)], Ct));

        Assert.Equal(
            "When request: the catch-up around the step called the step 2 times; it must call the step exactly once. The step ran once.",
            error.Message);
        Assert.Contains("the catch-up around the step called it again; it must call the step exactly once.", seenByTheCatchUp);
    }

    [Fact]
    public async Task AStepThatThrows_SurfacesItsOwnErrorThroughACatchUpAroundTheStep()
    {
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .CatchUp(async (step, ct) => await step(ct));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.Given()
                .When(Request(_ => throw new InvalidOperationException("the request failed")))
                .Then([ExpectResponse(OK)], Ct));
    }

    [Fact]
    public async Task ACatchUpAroundTheStep_ThatFailsAfterTheStep_NamesTheFailure()
    {
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .CatchUp(async (step, ct) =>
            {
                await step(ct);
                throw new InvalidOperationException("tracking failed");
            });

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Responding()).Then([ExpectResponse(OK)], Ct));

        Assert.Equal("When request: the catch-up around the step failed: tracking failed", error.Message);
    }

    [Fact]
    public async Task ACatchUpAroundTheStep_ThatFailsAfterTheDeadline_ReportsThatTheApplicationKeptProducingMessages()
    {
        await using var api = ApiWithAShortDeadline()
            .CatchUp(async (step, ct) =>
            {
                await step(ct);
                await PassingTheDeadline(ct);
                throw new InvalidOperationException("tracking failed");
            });

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Responding()).Then([ExpectResponse(OK)], Ct));

        Assert.EndsWith("The catch-up reported: tracking failed", error.Message);
    }

    [Fact]
    public async Task ACatchUpAroundTheStep_ThatNeverReturns_FailsOnceTheDeadlineHasPassed()
    {
        await using var api = ApiWithAShortDeadline()
            .CatchUp(async (step, ct) =>
            {
                await step(ct);
                await PassingTheDeadline(ct);
                await new TaskCompletionSource().Task;
            });

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Responding()).Then([ExpectResponse(OK)], Ct));

        Assert.StartsWith("The application kept producing messages for longer than 00:00:00.0010000 after the When request", error.Message);
    }

    [Fact]
    public async Task CancellingThenDuringAStepRunByACatchUpAroundIt_EndsWithOperationCanceled()
    {
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .CatchUp((step, ct) => step(ct));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var error = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            api.Given()
                .When(Request(_ =>
                {
                    cancellation.Cancel();
                    return Task.FromResult(new HttpResponseMessage(OK));
                }))
                .Then([ExpectResponse(OK)], cancellation.Token));

        Assert.StartsWith("Then was cancelled while the When request was running.", error.Message);
    }

    [Fact]
    public async Task CancellingThenDuringAStepWhoseCancellationACatchUpSwallows_StillEndsWithOperationCanceled()
    {
        await using var api = For(() => Task.FromResult(new Uri("http://localhost")))
            .CatchUp(async (step, ct) =>
            {
                try
                {
                    await step(ct);
                }
                catch (OperationCanceledException)
                {
                }
            });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            api.Given()
                .When(Request(_ =>
                {
                    cancellation.Cancel();
                    return Task.FromResult(new HttpResponseMessage(OK));
                }))
                .Then([ExpectResponse(OK)], cancellation.Token));
    }
}

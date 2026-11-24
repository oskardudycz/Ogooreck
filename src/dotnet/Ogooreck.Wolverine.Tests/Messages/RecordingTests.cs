using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Sample.Wolverine.Events;
using Wolverine;
using Xunit;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.ShopRequests;

namespace Ogooreck.Wolverine.Tests.Messages;

public class RecordingTests(AppApi api, BareApp app)
{
    private const string MalformedTraceparent = "00-not-a-trace-id-01";

    private static readonly Func<CancellationToken, Task> NoWait = _ => Task.CompletedTask;

    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public async Task ItemWithoutTraceparent_DoesNotCount()
    {
        var extra = new Envelope(new OrderPlaced(cartId));

        await api.Given(AddProduct(cartId, "socks"))
            .When(Request(async (client, ctx) =>
            {
                Messaging.Wolverine.Published.Add(extra);
                return await client.PostAsync($"/carts/{cartId}/orders", null, TestContext.Current.CancellationToken);
            }))
            .Then([ExpectPublishedIn(Messaging.Wolverine.Published, new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })],
                TestContext.Current.CancellationToken);

        Assert.Null(extra.ParentId);
    }

    [Fact]
    public async Task ItemWithMalformedTraceparent_IsIgnored()
    {
        var messages = new Recorded<Observed>();
        await using var spec = app.With(setup => setup.Recording(messages, observed => observed, NoWait));

        await spec.Given()
            .When(Action(ctx =>
            {
                messages.Add(new Observed("malformed", MalformedTraceparent));
                messages.Add(new Observed("own", ctx.Traceparent));
                return Task.CompletedTask;
            }))
            .Then([ExpectPublishedIn(messages, "own")], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Recording_IsRepeatable_AndEachRecorderIsCheckedSeparately()
    {
        var first = new Recorded<Observed>();
        var second = new Recorded<Observed>();
        await using var spec = app.With(setup => setup
            .Recording(first, observed => observed, NoWait)
            .Recording(second, observed => observed, NoWait));

        await spec.Given()
            .When(Action(ctx =>
            {
                first.Add(new Observed("to first", ctx.Traceparent));
                second.Add(new Observed("to second", ctx.Traceparent));
                return Task.CompletedTask;
            }))
            .Then([ExpectPublishedIn(first, "to first"), ExpectPublishedIn(second, "to second")],
                TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RecordingWithACatchUpAroundTheStep_RunsEachStepInsideItAndChecksExactMessages()
    {
        var messages = new Recorded<Observed>();
        var wrappedSteps = 0;
        await using var spec = app.With(setup => setup.Recording(messages, observed => observed, async (step, ct) =>
        {
            wrappedSteps++;
            await step(ct);
        }));

        await spec.Given()
            .When(Action(ctx =>
            {
                messages.Add(new Observed("own", ctx.Traceparent));
                return Task.CompletedTask;
            }))
            .Then([ExpectPublishedIn(messages, "own")], TestContext.Current.CancellationToken);

        Assert.Equal(1, wrappedSteps);
    }

    [Fact]
    public async Task AddingTenThousandItems_NeverBlocksOrThrows()
    {
        var unread = new Recorded<int>();
        await using var spec = app.With(setup => setup.Recording(unread, item => new Observed(item, null)));

        await spec.Given()
            .When(Action(_ =>
            {
                for (var item = 0; item < 10_000; item++)
                    unread.Add(item);
                return Task.CompletedTask;
            }))
            .Then([], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RecordedAdd_AfterTheSpecificationIsDisposed_DoesNotThrow()
    {
        var messages = new Recorded<Observed>();
        var spec = app.With(setup => setup.Recording(messages, observed => observed, NoWait));
        await spec.Given().When(Action(_ => Task.CompletedTask)).Then([], TestContext.Current.CancellationToken);

        await spec.DisposeAsync();

        messages.Add(new Observed("after disposal", null));
    }

    [Fact]
    public async Task ExpectNothingPublished_WithoutCatchUp_FailsAtOnceSayingItNeedsACatchUpFunction()
    {
        var messages = new Recorded<Observed>();
        await using var spec = app.With(setup => setup.Recording(messages, observed => observed));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(Action(_ => Task.CompletedTask))
                .Then([ExpectNothingPublishedIn(messages)], TestContext.Current.CancellationToken));

        Assert.Equal(
            "When action: ExpectNothingPublishedIn(Recorded<Observed>) needs a catch-up function on that recorder to know that nothing more will arrive; register it with Recording(recorded, read, catchUp).",
            error.Message);
    }

    [Fact]
    public async Task ExpectPublished_OnARecorderThatIsNotRegistered_FailsSayingSo()
    {
        var unregistered = new Recorded<Observed>();
        var spec = app;

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(Action(_ => Task.CompletedTask))
                .Then([ExpectPublishedIn(unregistered, "any")], TestContext.Current.CancellationToken));

        Assert.Equal(
            "When action: ExpectPublishedIn(Recorded<Observed>, ...) needs a recorder registered on the specification with Recording(...), but this recorder is not registered.",
            error.Message);
    }

    [Fact]
    public async Task GivenStep_UsesTheOneAssemblyHost() =>
        await AssemblyHost.Record(api);
}

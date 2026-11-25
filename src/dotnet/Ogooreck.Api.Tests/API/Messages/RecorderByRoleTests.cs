using Ogooreck.Api.Tests.API.Messages.Fixtures;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Messages;

public class RecorderByRoleTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private readonly Recorded<Observed> published = new();
    private readonly Recorded<Observed> appended = new();
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExpectPublished_WithoutARecorder_ChecksTheSpecificationsRecorderOfPublishedMessages()
    {
        await using var spec = api.With(setup => setup.Recording(published, observed => observed, NoWait));

        await spec.Given().When(Adding(published, new OrderPlaced(cartId))).Then([ExpectPublished(new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task ExpectNothingPublished_WithoutARecorder_PassesWhenTheWhenStepPublishedNothing()
    {
        await using var spec = api.With(setup => setup.Recording(published, observed => observed, NoWait));

        await spec.Given().When(Adding(published)).Then([ExpectNothingPublished()], ct);
    }

    [Fact]
    public async Task ExpectPublished_WithoutARecorder_AndNoRecorder_FailsSayingHowToAddOne()
    {
        await using var spec = api.With(setup => setup);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(published)).Then([ExpectPublished(new OrderPlaced(cartId))], ct));

        Assert.EndsWith(
            "ExpectPublished(...) needs a recorder of the messages the app published, but the specification has none. Add a plugin with Use(...), or register a recorder with Recording(...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectNothingAppended_WithoutARecorder_AndNoRecorder_FailsSayingHowToAddOne()
    {
        await using var spec = api.With(setup => setup);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(appended)).Then([ExpectNothingAppended()], ct));

        Assert.EndsWith(
            "ExpectNothingAppended() needs a recorder of the events the app appended, but the specification has none. Add a plugin with Use(...), or register a recorder with Appending(...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectPublished_WithoutARecorder_AndTwoRecorders_FailsSayingToNameOne()
    {
        var other = new Recorded<Observed>();
        await using var spec = api.With(setup => setup
            .Recording(published, observed => observed, NoWait)
            .Recording(other, observed => observed, NoWait));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(published)).Then([ExpectPublished(new OrderPlaced(cartId))], ct));

        Assert.EndsWith(
            "ExpectPublished(...) found 2 recorders of the messages the app published, so it can't tell which one to check. Name it with ExpectPublishedIn(...), for example ExpectPublishedIn(Messaging.Kafka.Published, ...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectNothingPublished_WithoutARecorder_AndTwoRecorders_FailsSayingToNameOneWithExpectNothingPublishedIn()
    {
        var other = new Recorded<Observed>();
        await using var spec = api.With(setup => setup
            .Recording(published, observed => observed, NoWait)
            .Recording(other, observed => observed, NoWait));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(published)).Then([ExpectNothingPublished()], ct));

        Assert.EndsWith(
            "ExpectNothingPublished() found 2 recorders of the messages the app published, so it can't tell which one to check. Name it with ExpectNothingPublishedIn(...), for example ExpectNothingPublishedIn(Messaging.Kafka.Published).",
            error.Message);
    }

    [Fact]
    public async Task ExpectAppended_WithoutARecorder_AndTwoRecorders_FailsNamingARecorderOfAppendedEvents()
    {
        var other = new Recorded<Observed>();
        await using var spec = api.With(setup => setup
            .Appending(appended, observed => observed, NoWait)
            .Appending(other, observed => observed, NoWait));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(appended)).Then([ExpectAppended(new OrderPlaced(cartId))], ct));

        Assert.EndsWith(
            "ExpectAppended(...) found 2 recorders of the events the app appended, so it can't tell which one to check. Name it with ExpectAppendedIn(...), for example ExpectAppendedIn(Events.Marten.Appended, ...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WithoutRecorders_AndTwoRecorders_FailsSayingToNameOneInTheTree()
    {
        var other = new Recorded<Observed>();
        await using var spec = api.With(setup => setup
            .Recording(published, observed => observed, NoWait)
            .Recording(other, observed => observed, NoWait));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(published)).Then([ExpectCausation(Published(new OrderPlaced(cartId)))], ct));

        Assert.EndsWith(
            "ExpectCausation(...) found 2 recorders of the messages the app published, so it can't tell which one to check. Name it with PublishedIn(...) or SentIn(...), for example PublishedIn(Messaging.Kafka.Published, ...) or SentIn(Messaging.Kafka.Published, ...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WithoutRecorders_AndTwoRecordersOfAppendedEvents_FailsSayingToNameOneInTheTree()
    {
        var other = new Recorded<Observed>();
        await using var spec = api.With(setup => setup
            .Appending(appended, observed => observed, NoWait)
            .Appending(other, observed => observed, NoWait));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(appended)).Then([ExpectCausation(Appended(new OrderPlaced(cartId)))], ct));

        Assert.EndsWith(
            "ExpectCausation(...) found 2 recorders of the events the app appended, so it can't tell which one to check. Name it with AppendedIn(...), for example AppendedIn(Events.Marten.Appended, ...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectPublishedIn_OnARecorderOfAppendedEvents_FailsSayingToUseExpectAppendedIn()
    {
        await using var spec = api.With(setup => setup.Appending(appended, observed => observed, NoWait));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(appended, new OrderPlaced(cartId))).Then([ExpectPublishedIn(appended, new OrderPlaced(cartId))], ct));

        Assert.EndsWith(
            "ExpectPublishedIn(Recorded<Observed>, ...) checks the messages the app published, but this recorder is registered with Appending(...), so it holds the events the app appended. Check it with ExpectAppendedIn(...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectNothingAppendedIn_OnARecorderOfPublishedMessages_FailsSayingToUseExpectNothingPublishedIn()
    {
        await using var spec = api.With(setup => setup.Recording(published, observed => observed, NoWait));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(published)).Then([ExpectNothingAppendedIn(published)], ct));

        Assert.EndsWith(
            "ExpectNothingAppendedIn(Recorded<Observed>) checks the events the app appended, but this recorder is registered with Recording(...), so it holds the messages the app published. Check it with ExpectNothingPublishedIn(...).",
            error.Message);
    }

    [Fact]
    public async Task AppendedIn_OnARecorderOfPublishedMessages_FailsSayingToUsePublishedInOrSentIn()
    {
        await using var spec = api.With(setup => setup.Recording(published, observed => observed, NoWait));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Adding(published, new OrderPlaced(cartId))).Then([ExpectCausation(AppendedIn(published, new OrderPlaced(cartId)))], ct));

        Assert.EndsWith(
            "ExpectCausation(...) checks the events the app appended, but this recorder is registered with Recording(...), so it holds the messages the app published. Check it with PublishedIn(...) or SentIn(...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectAppended_WithoutARecorder_ChecksAppendedEvents_AndExpectPublishedDoesNotSeeThem()
    {
        await using var spec = api.With(setup => setup
            .Recording(published, observed => observed, NoWait)
            .Appending(appended, observed => observed, NoWait));

        await spec.Given().When(Adding(appended, new OrderPlaced(cartId)))
            .Then([ExpectAppended(new OrderPlaced(cartId)), ExpectNothingPublished()], ct);
    }

    [Fact]
    public async Task ExpectNothingAppended_WithoutARecorder_PassesWhenTheWhenStepAppendedNothing()
    {
        await using var spec = api.With(setup => setup.Appending(appended, observed => observed, NoWait));

        await spec.Given().When(Adding(appended)).Then([ExpectNothingAppended()], ct);
    }

    [Fact]
    public async Task ExpectAppended_WithoutARecorder_OnAnAppendingRecorderWithoutCatchUp_WaitsForTheEvent()
    {
        await using var spec = api.With(setup => setup.Appending(appended, observed => observed));

        await spec.Given().When(Adding(appended, new OrderPlaced(cartId))).Then([ExpectAppended(new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task ExpectAppended_WithAnAppendingRecorderWrappingTheStep_ChecksTheEvents()
    {
        await using var spec = api.With(setup => setup.Appending(appended, observed => observed, (step, token) => step(token)));

        await spec.Given().When(Adding(appended, new OrderPlaced(cartId))).Then([ExpectAppended(new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task ExpectCausation_WithoutRecorders_FollowsPublishedAndAppendedMessages()
    {
        await using var spec = api.With(setup => setup
            .Recording(published, observed => observed, NoWait)
            .Appending(appended, observed => observed, NoWait));

        await spec.Given()
            .When(Action(ctx =>
            {
                appended.Add(new Observed(new OrderPlaced(cartId), ctx.Traceparent, "m1", "c1", null));
                published.Add(new Observed(new ShipOrder(cartId), ctx.Traceparent, "m2", "c1", "m1"));
                published.Add(new Observed(new Dispatched(cartId), ctx.Traceparent, "m3", "c1", "m2"));
                return Task.CompletedTask;
            }))
            .Then([ExpectCausation(Appended(new OrderPlaced(cartId), Sent(new ShipOrder(cartId), Published(new Dispatched(cartId)))))], ct);
    }

    [Fact]
    public async Task PluginAppendingAroundEachStep_ChecksTheEventsWithoutARecorder()
    {
        await using var spec = api.With(setup => setup.Use(new AppendingAroundEachStep(appended)));

        await spec.Given().When(Adding(appended, new OrderPlaced(cartId))).Then([ExpectAppended(new OrderPlaced(cartId))], ct);
    }

    private static Task NoWait(CancellationToken _) => Task.CompletedTask;

    private sealed class AppendingAroundEachStep(Recorded<Observed> appended): ISpecificationPlugin
    {
        public void Register(SpecificationPluginRegistration registration) =>
            registration.Appending(appended, observed => observed, (step, token) => step(token));
    }

    private static TestStep Adding(Recorded<Observed> recorder, params object[] messages) =>
        Action(ctx =>
        {
            foreach (var message in messages)
                recorder.Add(new Observed(message, ctx.Traceparent));
            return Task.CompletedTask;
        });
}

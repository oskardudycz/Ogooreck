using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static Ogooreck.Api.Tests.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.Messages;

public class CausationTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly RecordedMessages recorded = new(api);
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    private TestStep OrderPlacedThenShipOrder(string shipCausation = "m1", string shipCorrelation = "c1") =>
        recorded.Adding(traceparent =>
        [
            new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null),
            new Observed(new ShipOrder(cartId), traceparent, "m2", shipCorrelation, shipCausation)
        ]);

    private Expectation OrderPlacedCausedShipOrder() =>
        ExpectCausation(PublishedIn(recorded.Items, new OrderPlaced(cartId), SentIn(recorded.Items, new ShipOrder(cartId))));

    [Fact]
    public async Task ExpectCausation_PassesForAMessageCausedByAnotherWithTheSameCorrelation()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        await spec.Given().When(OrderPlacedThenShipOrder()).Then([OrderPlacedCausedShipOrder()], ct);
    }

    [Fact]
    public async Task ExpectCausation_FailsNamingExpectedAndActualCausation()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(OrderPlacedThenShipOrder(shipCausation: "m9")).Then([OrderPlacedCausedShipOrder()], ct));

        Assert.Contains("ShipOrder", error.Message);
        Assert.Contains("expected causation id m1, but found m9", error.Message);
        Assert.Matches(@"1\. OrderPlaced \{[^\n]*\} \(id m1, correlation c1, causation \(none\)\)\s+2\. ShipOrder \{[^\n]*\} \(id m2, correlation c1, causation m9\)", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_FailsNamingExpectedCorrelation()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(OrderPlacedThenShipOrder(shipCorrelation: "c2")).Then([OrderPlacedCausedShipOrder()], ct));

        Assert.Contains("ShipOrder", error.Message);
        Assert.Contains("expected correlation id c1, but found c2", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WhenReadReturnsNoIds_FailsSayingItNeedsIds()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(new OrderPlaced(cartId), new ShipOrder(cartId)))
                .Then([OrderPlacedCausedShipOrder()], ct));

        Assert.Equal(
            "When action: ExpectCausation(...) needs message, correlation and causation ids from read, but read returned no message id and correlation id for OrderPlaced. Return them in Observed from the function given to Recording(...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WhenALeafHasNoMessageId_Passes()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        await spec.Given()
            .When(recorded.Adding(traceparent =>
            [
                new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null),
                new Observed(new ShipOrder(cartId), traceparent, null, "c1", "m1")
            ]))
            .Then([OrderPlacedCausedShipOrder()], ct);
    }

    [Fact]
    public async Task ExpectCausation_WhenAChildHasNoCausationId_FailsSayingItNeedsIds()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(traceparent =>
                [
                    new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null),
                    new Observed(new ShipOrder(cartId), traceparent, "m2", "c1", null)
                ]))
                .Then([OrderPlacedCausedShipOrder()], ct));

        Assert.Equal(
            "When action: ExpectCausation(...) needs message, correlation and causation ids from read, but read returned no causation id for ShipOrder. Return them in Observed from the function given to Recording(...).",
            error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WhenTheRootWasNotPublished_FailsListingWhatWasPublishedWithIds()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(traceparent => [new Observed(new CartClosed(cartId), traceparent, "m1", "c1", null)]))
                .Then([OrderPlacedCausedShipOrder()], ct));

        Assert.Contains("expected OrderPlaced", error.Message);
        Assert.Contains("to be published, but the When step published only", error.Message);
        Assert.Matches(@"1\. CartClosed \{[^\n]*\} \(id m1, correlation c1, causation \(none\)\)", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WhenTheCausedMessageWasNotPublished_FailsNamingIt()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(traceparent => [new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null)]))
                .Then([OrderPlacedCausedShipOrder()], ct));

        Assert.Contains("expected ShipOrder", error.Message);
        Assert.Contains("to be published", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WithAnAppendedNode_UsesTheWordAppended()
    {
        await using var spec = recorded.AppendingSpec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(traceparent => [new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null)]))
                .Then([ExpectCausation(AppendedIn(recorded.Items, new OrderPlaced(cartId), AppendedIn(recorded.Items, new Dispatched(cartId))))], ct));

        Assert.Contains("expected Dispatched", error.Message);
        Assert.Contains("to be appended, but the When step appended only", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WithAThreeLevelTree_ChecksEachParentChildPair()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(traceparent =>
                [
                    new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null),
                    new Observed(new ShipOrder(cartId), traceparent, "m2", "c1", "m1"),
                    new Observed(new Dispatched(cartId), traceparent, "m3", "c1", "m1")
                ]))
                .Then([
                    ExpectCausation(PublishedIn(recorded.Items, new OrderPlaced(cartId),
                        SentIn(recorded.Items, new ShipOrder(cartId), PublishedIn(recorded.Items, new Dispatched(cartId)))))
                ], ct));

        Assert.Contains("Dispatched", error.Message);
        Assert.Contains("expected causation id m2, but found m1", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WithAThreeLevelTree_PassesWhenEveryPairMatches()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        await spec.Given()
            .When(recorded.Adding(traceparent =>
            [
                new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null),
                new Observed(new ShipOrder(cartId), traceparent, "m2", "c1", "m1"),
                new Observed(new Dispatched(cartId), traceparent, "m3", "c1", "m2")
            ]))
            .Then([
                ExpectCausation(PublishedIn(recorded.Items, new OrderPlaced(cartId),
                    SentIn(recorded.Items, new ShipOrder(cartId), PublishedIn(recorded.Items, new Dispatched(cartId)))))
            ], ct);
    }

    [Fact]
    public async Task ExpectCausation_WithTwoRoots_ChecksBoth()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(OrderPlacedThenShipOrder())
                .Then([
                    ExpectCausation(PublishedIn(recorded.Items, new OrderPlaced(cartId)),
                        PublishedIn(recorded.Items, new Dispatched(cartId)))
                ], ct));

        Assert.Contains("expected Dispatched", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_IgnoresMessagesOfGivenSteps()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given(OrderPlacedThenShipOrder())
                .When(GetCart(cartId))
                .Then([OrderPlacedCausedShipOrder()], ct));

        Assert.Contains("expected OrderPlaced", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WithoutCatchUp_PassesOnceTheCausedMessageArrives()
    {
        await using var spec = recorded.Spec();
        await spec.Given()
            .When(recorded.Adding(
                traceparent => [new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null)],
                traceparent => recorded.Items.Add(new Observed(new ShipOrder(cartId), traceparent, "m2", "c1", "m1"))))
            .Then([OrderPlacedCausedShipOrder()], ct);
    }

    [Fact]
    public async Task ExpectCausation_WithoutCatchUp_FailsAtTheDeadlineWhenTheCausedMessageNeverArrives()
    {
        await using var spec = recorded.Spec(deadline: TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(traceparent => [new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null)]))
                .Then([OrderPlacedCausedShipOrder()], ct));

        Assert.Contains("ExpectCausation(...) did not find the tree within 00:00:02", error.Message);
        Assert.Contains("expected ShipOrder", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_AcrossTwoRecorders_WaitsWhenOneHasNoCatchUp()
    {
        var events = new RecordedMessages(api);
        await using var spec = api.With(setup => setup
            .Recording(recorded.Items, recorded.Read, _ => Task.CompletedTask)
            .Appending(events.Items, observed => observed));

        await spec.Given()
            .When(recorded.Adding(
                traceparent => [new Observed(new OrderPlaced(cartId), traceparent, "m1", "c1", null)],
                traceparent => events.Items.Add(new Observed(new ShipOrder(cartId), traceparent, "m2", "c1", "m1"))))
            .Then([
                ExpectCausation(PublishedIn(recorded.Items, new OrderPlaced(cartId), AppendedIn(events.Items, new ShipOrder(cartId))))
            ], ct);
    }

    [Fact]
    public async Task ExpectCausation_OnARecorderThatIsNotRegistered_FailsSayingSo()
    {
        await using var spec = recorded.Spec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(OrderPlacedThenShipOrder())
                .Then([ExpectCausation(PublishedIn(new Recorded<Observed>(), new OrderPlaced(cartId)))], ct));

        Assert.Contains("ExpectCausation(...) needs a recorder registered on the specification with Recording(...)", error.Message);
    }

    [Fact]
    public async Task ExpectCausation_WhenCancelledWhileWaiting_NamesTheWait()
    {
        await using var spec = recorded.Spec();
        using var cancelling = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var error = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            spec.Given().When(recorded.CancellingAtTheFirstCheck(cancelling)).Then([OrderPlacedCausedShipOrder()], cancelling.Token));

        Assert.Contains("Then was cancelled while ExpectCausation(...) was waiting", error.Message);
    }
}

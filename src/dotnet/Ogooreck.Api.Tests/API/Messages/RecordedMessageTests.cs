using Ogooreck.Api.Tests.API.Messages.Fixtures;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Messages;

public class RecordedMessageTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly RecordedMessages recorded = new(api);
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_MatchesASubsequenceWithGapsBetweenMessages()
    {
        await using var spec = recorded.Spec();

        await spec.Given()
            .When(recorded.Adding(new OrderPlaced(cartId), new CartClosed(cartId), new ShipOrder(cartId)))
            .Then([ExpectPublishedIn(recorded.Items, new OrderPlaced(cartId), new ShipOrder(cartId))], ct);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_WaitsForAMessageThatArrivesLater()
    {
        await using var spec = recorded.Spec();
        await spec.Given()
            .When(recorded.AddingAfterTheFirstCheck(new OrderPlaced(cartId), new CartClosed(cartId)))
            .Then([ExpectPublishedIn(recorded.Items, new OrderPlaced(cartId), new CartClosed(cartId))], ct);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_FailsAtTheDeadline_WhenTheOrderDiffers()
    {
        await using var spec = recorded.Spec(deadline: TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(new OrderPlaced(cartId), new CartClosed(cartId)))
                .Then([ExpectPublishedIn(recorded.Items, new CartClosed(cartId), new OrderPlaced(cartId))], ct));

        Assert.Contains("ExpectPublishedIn(Recorded<Observed>, ...): after 00:00:02", error.Message);
        Assert.Contains("message 2 (OrderPlaced) did not arrive", error.Message);
        Assert.Matches(@"Expected:\s+1\. CartClosed \{[^\n]*\}\s+2\. OrderPlaced", error.Message);
        Assert.Matches(@"Published by the When step:\s+1\. OrderPlaced \{[^\n]*\}\s+2\. CartClosed \{", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_WhenNothingWasPublished_FailsAtTheDeadlineListingNone()
    {
        await using var spec = recorded.Spec(deadline: TimeSpan.FromSeconds(2));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([ExpectPublishedIn(recorded.Items, new OrderPlaced(cartId))], ct));

        Assert.Contains("message 1 (OrderPlaced) did not arrive", error.Message);
        Assert.Matches(@"Published by the When step:\s+\(none\)", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_IgnoresMessagesWithAnotherTraceId()
    {
        await using var spec = recorded.Spec(deadline: TimeSpan.FromSeconds(2));
        var foreign = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        recorded.Items.Add(new Observed(new OrderPlaced(cartId), foreign));

        await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([ExpectPublishedIn(recorded.Items, new OrderPlaced(cartId))], ct));
    }

    [Fact]
    public async Task ExpectPublished_WithoutCatchUp_WhenCancelled_EndsWithOperationCanceledExceptionNamingTheWait()
    {
        await using var spec = recorded.Spec();
        using var cancelling = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            spec.Given().When(recorded.CancellingAtTheFirstCheck(cancelling)).Then([ExpectPublishedIn(recorded.Items, new OrderPlaced(cartId))],
                cancelling.Token));

        Assert.Contains("Then was cancelled while ExpectPublishedIn(Recorded<Observed>, ...) was waiting", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_OnARecorderThatIsNotRegistered_FailsSayingSo()
    {
        await using var spec = recorded.Spec();

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([ExpectPublishedIn(new Recorded<Observed>(), new OrderPlaced(cartId))], ct));

        Assert.Contains("needs a recorder registered on the specification with Recording(...)", error.Message);
    }

    [Fact]
    public async Task ExpectAppended_PassesWhenTheWhenStepAppendedTheEvents()
    {
        await using var spec = recorded.AppendingSpec(withCatchUp: true);

        await spec.Given()
            .When(recorded.Adding(new OrderPlaced(cartId)))
            .Then([ExpectAppendedIn(recorded.Items, new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task ExpectAppended_WithADifferentEvent_FailsListingAppendedEventsNumbered()
    {
        await using var spec = recorded.AppendingSpec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(new OrderPlaced(cartId), new CartClosed(cartId)))
                .Then([ExpectAppendedIn(recorded.Items, new OrderPlaced(cartId))], ct));

        Assert.Contains("ExpectAppendedIn(Recorded<Observed>, ...): expected 1 events, but the When step appended 2.", error.Message);
        Assert.Matches(@"Expected:\s+1\. OrderPlaced \{[^\n]*\}\s+Appended by the When step:\s+1\. OrderPlaced \{[^\n]*\}\s+2\. CartClosed \{", error.Message);
    }

    [Fact]
    public async Task ExpectAppended_WithAnotherEventTypeInTheSamePlace_FailsNamingBothEventTypes()
    {
        await using var spec = recorded.AppendingSpec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(new OrderPlaced(cartId)))
                .Then([ExpectAppendedIn(recorded.Items, new CartClosed(cartId))], ct));

        Assert.Contains("event 1: expected CartClosed, but found OrderPlaced.", error.Message);
    }

    [Fact]
    public async Task ExpectNothingAppended_PassesWhenTheWhenStepAppendedNothing()
    {
        await using var spec = recorded.AppendingSpec(withCatchUp: true);

        await spec.Given().When(GetCart(cartId)).Then([ExpectNothingAppendedIn(recorded.Items)], ct);
    }

    [Fact]
    public async Task ExpectNothingAppended_WhenAnEventWasAppended_FailsListingIt()
    {
        await using var spec = recorded.AppendingSpec(withCatchUp: true);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(recorded.Adding(new OrderPlaced(cartId)))
                .Then([ExpectNothingAppendedIn(recorded.Items)], ct));

        Assert.Contains("ExpectNothingAppendedIn(Recorded<Observed>): expected no events, but the When step appended 1.", error.Message);
        Assert.Matches(@"Appended by the When step:\s+1\. OrderPlaced \{", error.Message);
    }

    [Fact]
    public async Task ExpectNothingAppended_WithoutCatchUp_FailsAtOnceSayingItNeedsACatchUpFunction()
    {
        await using var spec = recorded.AppendingSpec();
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(GetCart(cartId)).Then([ExpectNothingAppendedIn(recorded.Items)], ct));

        Assert.EndsWith("ExpectNothingAppendedIn(Recorded<Observed>) needs a catch-up function on that recorder to know that nothing more will arrive; register it with Appending(recorded, read, catchUp).", error.Message);
    }

    [Fact]
    public async Task ExpectAppended_IgnoresEventsAppendedByAGivenStep()
    {
        await using var spec = recorded.AppendingSpec(withCatchUp: true);

        await spec.Given(recorded.Adding(new CartClosed(cartId)))
            .When(recorded.Adding(new OrderPlaced(cartId)))
            .Then([ExpectAppendedIn(recorded.Items, new OrderPlaced(cartId))], ct);
    }
}

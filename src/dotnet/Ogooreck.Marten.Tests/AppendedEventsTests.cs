using System.Net.Http.Json;
using Ogooreck.Assertions;
using Ogooreck.Sample.Marten.Events;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Marten.Tests.ShopRequests;

namespace Ogooreck.Marten.Tests;

public class AppendedEventsTests(PostgresDatabase database, MartenApi shop): IClassFixture<MartenApi>
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExpectAppended_PassesWhenTheWhenStepAppendedTheEvent()
    {
        await shop.Given()
            .When(PlaceOrder(cartId))
            .Then([ExpectAppended(new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task ExpectNothingAppended_PassesWhenTheWhenStepAppendedNothing()
    {
        await shop.Given()
            .When(GetCart(cartId))
            .Then([ExpectResponse(NotFound), ExpectNothingAppended()], ct);
    }

    [Fact]
    public async Task ExpectAppended_WithADifferentEvent_FailsListingAppendedEventsNumbered()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given()
                .When(ShipOrder(cartId))
                .Then([ExpectAppended(new OrderPlaced(cartId))], ct));

        Assert.Contains("ExpectAppended(...): expected 1 events, but the When step appended 2.", error.Message);
        Assert.Matches(@"Expected:\s+1\. OrderPlaced \{[^\n]*\}\s+Appended by the When step:\s+1\. OrderPlaced \{[^\n]*\}\s+2\. OrderShipped \{", error.Message);
    }

    [Fact]
    public async Task ExpectNothingAppended_WhenAnEventWasAppended_FailsListingIt()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given().When(PlaceOrder(cartId)).Then([ExpectNothingAppended()], ct));

        Assert.Contains("expected no events, but the When step appended 1.", error.Message);
        Assert.Matches(@"Appended by the When step:\s+1\. OrderPlaced \{", error.Message);
    }

    [Fact]
    public async Task ExpectNothingAppended_WithoutCatchUp_FailsAtOnceSayingItNeedsACatchUpFunction()
    {
        await using var shop = MartenApi.WithoutCatchUp(database);

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given().When(GetCart(cartId)).Then([ExpectNothingAppended()], ct));

        Assert.Contains("ExpectNothingAppended() needs a catch-up function on that recorder", error.Message);
    }

    [Fact]
    public async Task ExpectAppended_IgnoresEventsAppendedByAGivenStep()
    {
        await shop.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectAppended(new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task ExpectAppended_IgnoresEventsAppendedWithoutATraceparent()
    {
        await shop.Given()
            .When(Request(async client =>
            {
                var imported = await client.PostAsJsonAsync($"/carts/{cartId}/imports", new { product = "socks" }, ct);
                imported.EnsureSuccessStatusCode();
                return await client.PostAsync($"/carts/{cartId}/orders", null, ct);
            }))
            .Then([ExpectAppended(new OrderPlaced(cartId))], ct);
    }

    [Fact]
    public async Task ExpectNothingAppended_OnAnEmptyEventStore_Passes()
    {
        await using var empty = new MartenApi(database);

        await empty.Given().When(GetCart(cartId)).Then([ExpectNothingAppended()], ct);
    }

    [Fact]
    public async Task ProjectionsStaleUntilTheDeadline_FailTheCatchUpWithMartensTimeout()
    {
        await using var app = MartenApi.WithStalledProjections(database);
        await using var shop = app.With(spec => spec.Deadline(TimeSpan.FromMilliseconds(500)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given().When(AddProduct(cartId, "socks")).Then([], ct));

        Assert.StartsWith(
            $"The application kept producing messages for longer than 00:00:00.5000000 after the When request POST /carts/{cartId}/products.",
            error.Message);
        Assert.IsType<TimeoutException>(error.InnerException);
    }

    [Fact]
    public async Task PlugDeadline_BoundsTheProjectionWait_WithOgoorecksCatchUpMessage()
    {
        await using var app = MartenApi.WithStalledProjections(database);
        await using var shop = app.With(spec => spec.Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(2)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            shop.Given().When(AddProduct(cartId, "socks")).Then([], ct));

        Assert.StartsWith(
            $"The application kept producing messages for longer than 00:00:02 after the When request POST /carts/{cartId}/products.",
            error.Message);
    }

    [Fact]
    public async Task ProjectionCatchUp_MakesAReadOfAnAsyncProjectionSeeTheGivenStepsEvent()
    {
        var reads = 0;

        await shop.Given(AddProduct(cartId, "socks"))
            .When(Request(client =>
            {
                reads++;
                return client.GetAsync($"/carts/{cartId}", ct);
            }))
            .Then([ExpectResponse(OK, Body(new { products = new[] { "socks" } }))], ct);

        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task ExpectCausation_WithAnAppendedNodeOnAMartenRecorder_Passes()
    {
        await shop.Given()
            .When(ShipOrder(cartId))
            .Then([
                ExpectCausation(Appended(new OrderPlaced(cartId),
                    Appended(new OrderShipped(cartId))))
            ], ct);
    }
}

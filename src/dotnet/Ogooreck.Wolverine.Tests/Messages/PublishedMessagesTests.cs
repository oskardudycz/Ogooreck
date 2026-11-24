using Ogooreck.Assertions;
using Ogooreck.Sample.Wolverine.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.ShopRequests;

namespace Ogooreck.Wolverine.Tests.Messages;

public class PublishedMessagesTests(AppApi api)
{
    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public Task ExpectPublished_PassesWhenTheWhenStepPublishedExactlyThatMessage() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([
                ExpectResponse(Created),
                ExpectPublished(new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })
            ], TestContext.Current.CancellationToken);

    [Fact]
    public async Task PublishObserverRegisteredThroughHost_AddsPublishedMessagesToTheRecorder()
    {
        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectPublished(new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })],
                TestContext.Current.CancellationToken);

        Assert.Contains(AppApi.EveryPublishedMessage, envelope => envelope.Message is OrderPlaced placed && placed.CartId == cartId);
    }

    [Fact]
    public async Task ExpectPublished_FailsListingExpectedAndPublishedMessagesNumbered_WhenAnExtraMessageWasPublished()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(CheckOut(cartId))
                .Then([ExpectPublished(new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })],
                    TestContext.Current.CancellationToken));

        Assert.StartsWith(
            $"When request POST /carts/{cartId}/checkout: ExpectPublished(...): expected 1 messages, but the When step published 2.",
            error.Message);
        Assert.Matches(@"Expected:\s+1\. OrderPlaced \{[^\n]*\}\s+Published by the When step:\s+1\. OrderPlaced \{[^\n]*\}\s+2\. CartClosed \{", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_FailsWhenTheOrderDiffers()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(CheckOut(cartId))
                .Then([
                    ExpectPublished(new CartClosed(cartId),
                        new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })
                ], TestContext.Current.CancellationToken));

        Assert.Contains("ExpectPublished(...): message 1: expected CartClosed, but found OrderPlaced.",
            error.Message);
    }

    [Fact]
    public async Task ExpectPublished_FailsWhenAFieldDiffers()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"), AddProduct(cartId, "shoes"))
                .When(PlaceOrder(cartId))
                .Then([
                    ExpectPublished(new OrderPlaced(cartId) { Total = 20, Products = ["socks", "shoes"] })
                ], TestContext.Current.CancellationToken));

        Assert.Contains("message 1 (OrderPlaced): $.Total: expected <20>, but found <10>.", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_FailsWhenNestedCollectionOrderDiffers()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"), AddProduct(cartId, "shoes"))
                .When(PlaceOrder(cartId))
                .Then([
                    ExpectPublished(new OrderPlaced(cartId) { Total = 10, Products = ["shoes", "socks"] })
                ], TestContext.Current.CancellationToken));

        Assert.Contains("message 1 (OrderPlaced): $.Products[0]: expected <shoes>, but found <socks>.", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_IgnoresMessagesOfGivenSteps()
    {
        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectPublished(new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })],
                TestContext.Current.CancellationToken);

        Assert.Contains(AppApi.EveryPublishedMessage, envelope => envelope.Message is ProductAdded added && added.CartId == cartId);
    }

    [Fact]
    public async Task LateMessageOfAGivenStep_DoesNotCount()
    {
        await api.Given(AddProduct(cartId, "socks"), HoldLateArrival(cartId))
            .When(Request(async client =>
            {
                var released = await client.PostAsync($"/late-arrivals/{cartId}/release", null, TestContext.Current.CancellationToken);
                released.EnsureSuccessStatusCode();
                return await client.PostAsync($"/carts/{cartId}/orders", null, TestContext.Current.CancellationToken);
            }))
            .Then([ExpectPublished(new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })],
                TestContext.Current.CancellationToken);

        Assert.Contains(AppApi.EveryPublishedMessage, envelope => envelope.Message is LateArrived late && late.CartId == cartId);
    }

    [Fact]
    public async Task ExpectPublished_WhenNoMessageWasPublished_FailsListingEmptyPublished()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(PlaceOrder(cartId))
                .Then([ExpectPublished(new OrderPlaced(cartId))],
                    TestContext.Current.CancellationToken));

        Assert.Contains("expected 1 messages, but the When step published 0.", error.Message);
        Assert.Matches(@"Published by the When step:\s+\(none\)", error.Message);
    }

    [Fact]
    public async Task ExpectPublished_OnARequestWhenStep_UsesTheTraceIdOfTheRequestsTraceparent()
    {
        string? traceparent = null;

        await api.Given(AddProduct(cartId, "socks"))
            .When(Request(async (client, ctx) =>
            {
                traceparent = ctx.Traceparent;
                return await client.PostAsync($"/carts/{cartId}/orders", null, TestContext.Current.CancellationToken);
            }))
            .Then([ExpectPublished(new OrderPlaced(cartId) { Total = 5, Products = ["socks"] })],
                TestContext.Current.CancellationToken);

        var placed = Assert.Single(AppApi.EveryPublishedMessage, envelope => envelope.Message is OrderPlaced order && order.CartId == cartId);
        Assert.Equal(traceparent!.Split('-')[1], placed.ParentId!.Split('-')[1]);
    }

    [Fact]
    public Task ExpectNothingPublished_PassesWhenTheWhenStepPublishedNothing() =>
        api.Given()
            .When(PlaceOrder(cartId))
            .Then([ExpectResponse(BadRequest), ExpectNothingPublished()],
                TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExpectNothingPublished_FailsListingThePublishedMessage()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(PlaceOrder(cartId))
                .Then([ExpectNothingPublished()], TestContext.Current.CancellationToken));

        Assert.StartsWith(
            $"When request POST /carts/{cartId}/orders: ExpectNothingPublished(): expected no messages, but the When step published 1.",
            error.Message);
        Assert.Matches(@"Published by the When step:\s+1\. OrderPlaced \{", error.Message);
    }

    [Fact]
    public async Task GivenStep_UsesTheOneAssemblyHost() =>
        await AssemblyHost.Record(api);
}

using Ogooreck.Api.Tests.API.Messages.Fixtures;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Steps;

public class EventuallyTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Eventually_ResendsTheRequestUntilTheReadModelShowsTheChange()
    {
        var attempts = 0;

        await api.Given(AddProduct(cartId, "socks"))
            .When(Eventually(Request(client =>
                client.GetAsync($"/carts/{(++attempts < 3 ? Guid.NewGuid() : cartId)}", ct))))
            .Then([ExpectResponse(OK)], ct);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Eventually_StillFailingAtTheDeadline_SaysHowManyAttemptsAndTheLastResponse()
    {
        await using var spec = api.With(setup => setup.Deadline(TimeSpan.FromMilliseconds(200)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given().When(Eventually(GetCart(cartId))).Then([ExpectResponse(OK)], ct));

        Assert.Matches(@"the response expectations still failed when the deadline of 00:00:00\.2000000 passed, after \d+ attempts\. Last response: 404 NotFound", error.Message);
    }

    [Fact]
    public async Task Eventually_OfAnAction_IsAnArgumentError()
    {
        var error = Assert.Throws<ArgumentException>(() => Eventually(Action(_ => Task.CompletedTask)));

        Assert.StartsWith("Eventually(...) takes a Request(...) step", error.Message);
    }

    [Fact]
    public async Task Eventually_InGiven_FailsSayingItBelongsInWhen()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(Eventually(GetCart(cartId))).When(GetCart(cartId)).Then([ExpectResponse(NotFound)], ct));

        Assert.Contains("Given step 1 eventually request: Eventually(...) is allowed only as the When step", error.Message);
    }

    [Fact]
    public async Task Eventually_WithOnlyMessageExpectations_SendsOnce()
    {
        var attempts = 0;
        var published = new Recorded<Observed>();
        await using var spec = api.With(setup => setup.Recording(published, observed => observed, _ => Task.CompletedTask));

        await spec.Given()
            .When(Eventually(Request(client => { attempts++; return client.GetAsync($"/carts/{cartId}", ct); })))
            .Then([ExpectNothingPublished()], ct);

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Publish_WithoutAPublishFunction_FailsSayingHowToConfigureIt()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Publish(new OrderPlaced(cartId))).Then([], ct));

        Assert.Contains("Publish(...) needs a publish function, but none is configured", error.Message);
    }

    [Fact]
    public async Task Then_CancelledWhileCatchingUp_SaysItWasWaitingForCatchUp()
    {
        using var cancellation = new CancellationTokenSource();
        await using var spec = api.With(setup => setup.CatchUp(token => Task.Delay(Timeout.Infinite, token)));

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            spec.Given().When(Action(_ => { cancellation.CancelAfter(50); return Task.CompletedTask; }))
                .Then([], cancellation.Token));

        Assert.StartsWith("Then was cancelled while waiting for catch-up after the When action.", error.Message);
    }
}

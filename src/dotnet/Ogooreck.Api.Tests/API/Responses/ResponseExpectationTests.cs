using System.Net.Http.Json;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Responses;

public class ResponseExpectationTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public async Task ExpectResponse_WithWrongStatus_FailsWithExpectedActualAndBody()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(PlaceOrder(cartId))
                .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        Assert.Contains($"When request POST /carts/{cartId}/orders: status: expected <200 OK>, but found <201 Created>",
            error.Message);
        Assert.Contains("\"status\":\"Placed\"", error.Message);
    }

    [Fact]
    public Task Body_AllowsExtraResponseFields() =>
        api.Given(AddProduct(cartId, "socks"), AddProduct(cartId, "shoes"), PlaceOrder(cartId))
            .When(GetOrder(cartId))
            .Then([ExpectResponse(OK, Body(new { status = "Placed" }))], TestContext.Current.CancellationToken);

    [Fact]
    public async Task Body_FailsWhenTopLevelArrayOrderDiffers()
    {
        await api.Given(AddProduct(cartId, "a"), AddProduct(cartId, "b"))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { products = new[] { "a", "b" } }))], TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK, Body(new { products = new[] { "b", "a" } }))], TestContext.Current.CancellationToken));

        Assert.Contains($"When request GET /carts/{cartId}: response body", error.Message);
        Assert.Contains("$[products][0]: expected <b>, but found <a>", error.Message);
    }

    [Fact]
    public async Task Body_FailsWhenNestedArrayOrderDiffers()
    {
        TestStep[] placedOrder = [AddProduct(cartId, "a"), AddProduct(cartId, "b"), PlaceOrder(cartId)];

        await api.Given(placedOrder)
            .When(GetOrder(cartId))
            .Then([ExpectResponse(OK,
                Body(new { cart = new { lines = new[] { new { product = "a" }, new { product = "b" } } } }))], TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(GetOrder(cartId))
                .Then([ExpectResponse(OK,
                    Body(new { cart = new { lines = new[] { new { product = "b" }, new { product = "a" } } } }))], TestContext.Current.CancellationToken));

        Assert.Contains("$[cart][lines][0][product]: expected <b>, but found <a>", error.Message);
    }

    [Fact]
    public async Task Body_WithAnEmptyResponseBody_FailsNamingTheEmptyBody()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(ConfirmCart(cartId))
                .Then([ExpectResponse(NoContent, Body(new { status = "Confirmed" }))], TestContext.Current.CancellationToken));

        Assert.Contains("response body is empty", error.Message);
    }

    [Fact]
    public Task ETag_MatchesAWeakETag() =>
        api.Given(AddProduct(cartId, "socks"), AddProduct(cartId, "shoes"))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, ETag(WeakETag(2)))], TestContext.Current.CancellationToken);

    [Fact]
    public Task Location_MatchesTheLocationHeader() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectResponse(Created, Location($"/orders/{cartId}"))], TestContext.Current.CancellationToken);

    [Fact]
    public async Task Body_WithAFieldTheResponseLacks_FailsSayingThereIsNoSuchField()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK, Body(new { discount = 10 }))], TestContext.Current.CancellationToken));

        Assert.Contains("$[discount]: expected <10>, but found <no such field>", error.Message);
    }

    [Fact]
    public async Task Body_OnANonJsonResponse_FailsShowingExpectedAndActual()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
                .When(Request(client => client.GetAsync($"/orders/{cartId}/status", TestContext.Current.CancellationToken)))
                .Then([ExpectResponse(OK, Body(new { status = "Placed" }))], TestContext.Current.CancellationToken));

        Assert.Contains("""{"status":"Placed"}""", error.Message);
        Assert.Contains("is not JSON: Placed", error.Message);
    }

    [Fact]
    public async Task ETag_WithDifferentValue_FailsShowingExpectedAndActual()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK, ETag(WeakETag(2)))], TestContext.Current.CancellationToken));

        Assert.Contains($"When request GET /carts/{cartId}: ETag: expected <W/\"2\">, but found <W/\"1\">",
            error.Message);
    }

    [Fact]
    public async Task Location_WithDifferentValue_FailsShowingExpectedAndActual()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(PlaceOrder(cartId))
                .Then([ExpectResponse(Created, Location("/orders/other"))], TestContext.Current.CancellationToken));

        Assert.Contains(
            $"When request POST /carts/{cartId}/orders: Location: expected </orders/other>, but found </orders/{cartId}>",
            error.Message);
    }

    [Fact]
    public async Task LocationPredicate_NotMatched_FailsShowingTheActualLocation()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(Request(client => client.PostAsJsonAsync("/api/products",
                    new RegisterProductRequest("abc-123", "Ogooreck"), TestContext.Current.CancellationToken)))
                .Then([ExpectResponse(Created, Location(l => l.StartsWith("/carts/")))], TestContext.Current.CancellationToken));

        Assert.Contains("When request POST /api/products: Location </api/products/", error.Message);
        Assert.Contains("does not match", error.Message);
    }

    [Fact]
    public async Task Headers_WithDifferentValue_FailsNamingHeaderExpectedAndActual()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "a"), AddProduct(cartId, "b"), AddProduct(cartId, "c"))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK, Headers(("X-Version", "4")))], TestContext.Current.CancellationToken));

        Assert.Contains($"When request GET /carts/{cartId}: header X-Version", error.Message);
        Assert.Contains("<4>", error.Message);
        Assert.Contains("<3>", error.Message);
    }

    [Fact]
    public Task Headers_ChecksContentHeadersToo() =>
        api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
            .When(GetOrderPage(cartId))
            .Then([ExpectResponse(OK, Headers(("Content-Type", "text/html")))], TestContext.Current.CancellationToken);

    [Fact]
    public async Task MissingHeaders_FailSayingTheHeaderIsMissing()
    {
        TestStep[] cartWithSocks = [AddProduct(cartId, "socks")];

        var header = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(cartWithSocks).When(GetCart(cartId))
                .Then([ExpectResponse(OK, Headers(("X-Missing", "1")))], TestContext.Current.CancellationToken));
        var eTag = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(cartWithSocks).When(PlaceOrder(cartId))
                .Then([ExpectResponse(Created, ETag(WeakETag(1)))], TestContext.Current.CancellationToken));
        var location = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(cartWithSocks).When(GetCart(cartId))
                .Then([ExpectResponse(OK, Location("/carts/x"))], TestContext.Current.CancellationToken));
        var matchingLocation = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(cartWithSocks).When(GetCart(cartId))
                .Then([ExpectResponse(OK, Location(_ => true))], TestContext.Current.CancellationToken));

        Assert.Contains($"When request GET /carts/{cartId}: header X-Missing: expected <1>, but found <no such header>.",
            header.Message);
        Assert.Contains(
            $"When request POST /carts/{cartId}/orders: ETag: expected <W/\"1\">, but found <no ETag header>.",
            eTag.Message);
        Assert.Contains($"When request GET /carts/{cartId}: Location: expected </carts/x>, but found <no Location header>.",
            location.Message);
        Assert.Contains(
            $"When request GET /carts/{cartId}: expected a Location that matches the condition, but the response has no Location header.",
            matchingLocation.Message);
    }

    [Fact]
    public async Task Satisfying_PropagatesTheAssertionsException()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK, Satisfying(_ => Assert.Fail("boom")))], TestContext.Current.CancellationToken));

        Assert.Equal("boom", error.Message);
    }

    [Fact]
    public async Task SatisfyingAsync_FailsWithTheAssertionMessageAfterTheAsyncCheck()
    {
        var checkCompleted = false;

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
                .When(GetOrder(cartId))
                .Then([ExpectResponse(OK, Satisfying(async response =>
                {
                    await Task.Yield();
                    var order = await response.Content.ReadFromJsonAsync<OrderStatusOnly>(TestContext.Current.CancellationToken);
                    checkCompleted = true;
                    Assert.Equal("Cancelled", order!.Status);
                }))], TestContext.Current.CancellationToken));

        Assert.True(checkCompleted);
        Assert.Contains("Cancelled", error.Message);
        Assert.Contains("Placed", error.Message);
    }

    [Fact]
    public async Task ExpectResponse_AfterAnActionWhenStep_FailsSayingNoResponse()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(Action(_ => Task.CompletedTask))
                .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        Assert.Contains(
            "When action: ExpectResponse(200 OK) needs a response, but the When step is an action, which sends no request.",
            error.Message);
    }

    [Fact]
    public Task ExpectError_MatchesStatusAndProblemDetailsPartially() =>
        api.Given(AddProduct(cartId, "socks"), ConfirmCart(cartId))
            .When(AddProduct(cartId, "shoes"))
            .Then([ExpectError(Conflict, new { title = "Cart already closed" })], TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExpectError_WithWrongStatus_FailsWithBothStatuses()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"), ConfirmCart(cartId))
                .When(AddProduct(cartId, "shoes"))
                .Then([ExpectError(NotFound)], TestContext.Current.CancellationToken));

        Assert.Contains("409", error.Message);
        Assert.Contains("404", error.Message);
    }

    [Fact]
    public async Task ExpectedErrorBodyMismatch_FailsNamingTheDifferentField()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"), ConfirmCart(cartId))
                .When(AddProduct(cartId, "shoes"))
                .Then([ExpectError(Conflict, new { title = "other" })], TestContext.Current.CancellationToken));

        Assert.Contains("$[title]: expected <other>, but found <Cart already closed>", error.Message);
    }

    [Fact]
    public async Task HtmlContaining_ChecksTheHtmlBody()
    {
        TestStep[] placedOrder = [AddProduct(cartId, "socks"), PlaceOrder(cartId)];

        await api.Given(placedOrder)
            .When(GetOrderPage(cartId))
            .Then([ExpectResponse(OK, HtmlContaining("Order placed"))], TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(placedOrder)
                .When(GetOrderPage(cartId))
                .Then([ExpectResponse(OK, HtmlNotContaining("Order placed"))], TestContext.Current.CancellationToken));

        Assert.Contains($"When request GET /orders/{cartId}/page: expected the HTML not to contain <Order placed>, but it does.",
            error.Message);
    }

    [Fact]
    public async Task HtmlContaining_WithAMissingFragment_FailsNamingTheFragmentAndShowingTheHtml()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
                .When(GetOrderPage(cartId))
                .Then([ExpectResponse(OK, HtmlContaining("Order placed", "Order shipped"))], TestContext.Current.CancellationToken));

        Assert.Contains($"When request GET /orders/{cartId}/page: expected the HTML to contain <Order shipped>",
            error.Message);
        Assert.Contains("<h1>Order placed</h1>", error.Message);
    }

    [Fact]
    public async Task HtmlContaining_OnAJsonResponse_FailsSayingContentTypeIsNotHtml()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK, HtmlContaining("x"))], TestContext.Current.CancellationToken));

        Assert.Contains($"When request GET /carts/{cartId}: expected an HTML response, but the content type is",
            error.Message);
    }

    [Fact]
    public Task RedirectsTo_IgnoresPathParametersAndQuery() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(CheckOut(cartId))
            .Then([ExpectResponse(SeeOther, RedirectsTo($"/orders/{cartId}"))], TestContext.Current.CancellationToken);

    [Fact]
    public async Task RedirectsTo_WithADifferentPath_FailsShowingTheLocation()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(CheckOut(cartId))
                .Then([ExpectResponse(SeeOther, RedirectsTo("/orders/other"))], TestContext.Current.CancellationToken));

        Assert.Contains(
            $"When request POST /carts/{cartId}/checkout: path of Location /orders/{cartId};jsessionid=1?x=1: expected </orders/other>, but found </orders/{cartId}>",
            error.Message);
    }

    [Fact]
    public async Task RedirectsTo_WithoutALocation_FailsSayingThereIsNone()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK, RedirectsTo($"/carts/{cartId}"))], TestContext.Current.CancellationToken));

        Assert.Contains(
            $"When request GET /carts/{cartId}: expected a redirect to </carts/{cartId}>, but the response has no Location header.",
            error.Message);
    }

    private record OrderStatusOnly(string Status);
}

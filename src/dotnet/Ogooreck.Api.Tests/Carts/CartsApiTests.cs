using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.Carts;

#region ApiClassFixture

public class CartsApiTests(ApiSpecification<Program> api)
    : IClassFixture<ApiSpecification<Program>>
{
    private readonly Guid cartId = Guid.NewGuid();

    private readonly CancellationToken ct =
        TestContext.Current.CancellationToken;

    #endregion

    #region ApiGetCart

    [Fact]
    public Task AddingAProduct_ShowsItInTheCart() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(GetCart(cartId))
            .Then([
                ExpectResponse(OK,
                    Body(new { products = new[] { "socks" } }),
                    ETag(WeakETag(1)))
            ], ct);

    #endregion

    #region ApiCreatedId

    [Fact]
    public Task CreatedCart_CanBeReadByItsId() =>
        api.Given(CreateCart())
            .When(Request((client, ctx) =>
                client.GetAsync($"/carts/{ctx.CreatedId()}", ct)))
            .Then([ExpectResponse(OK, Body(new { status = "Open" }))], ct);

    #endregion

    #region ApiPutWithIfMatch

    [Fact]
    public Task ConfirmingACart_WithItsVersion_ConfirmsIt() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(ConfirmCart(cartId, version: 1))
            .Then([ExpectResponse(NoContent)], ct);

    [Fact]
    public Task ConfirmingACart_WithAnOutdatedVersion_IsRejected() =>
        api.Given(AddProduct(cartId, "socks"), AddProduct(cartId, "shoes"))
            .When(ConfirmCart(cartId, version: 1))
            .Then([ExpectError(PreconditionFailed)], ct);

    #endregion

    #region ApiDelete

    [Fact]
    public Task RemovingAProduct_TakesItOutOfTheCart() =>
        api.Given(
                AddProduct(cartId, "socks"),
                AddProduct(cartId, "shoes"),
                RemoveProduct(cartId, "socks", version: 2))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { products = new[] { "shoes" } }))],
                ct);

    #endregion

    #region ApiPostCreated

    [Fact]
    public Task PlacingAnOrder_RespondsWithTheOrderAndItsLocation() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([
                ExpectResponse(Created,
                    Location($"/orders/{cartId}"),
                    Body(new { status = "Placed" }))
            ], ct);

    #endregion

    #region ApiErrors

    [Fact]
    public Task GettingAnUnknownCart_IsNotFound() =>
        api.Given()
            .When(GetCart(cartId))
            .Then([ExpectError(NotFound, new { title = "Cart not found" })], ct);

    #endregion

    #region ApiScenario

    [Fact]
    public Task PlacingAnOrder_ForAConfirmedCart_IsAConflict()
    {
        TestStep[] confirmedCart =
            [AddProduct(cartId, "socks"), ConfirmCart(cartId, version: 1)];

        return api.Given(confirmedCart)
            .When(PlaceOrder(cartId))
            .Then([ExpectError(Conflict, new { title = "Cart already closed" })],
                ct);
    }

    #endregion

    #region ApiRedirectsAndHtml

    [Fact]
    public Task CartSummary_RedirectsToTheCart() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(GetCartSummary(cartId))
            .Then([ExpectResponse(Found, RedirectsTo($"/carts/{cartId}"))], ct);

    [Fact]
    public Task OrderPage_ShowsThatTheOrderWasPlaced() =>
        api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
            .When(GetOrderPage(cartId))
            .Then([ExpectResponse(OK, HtmlContaining("Order placed"))], ct);

    #endregion

    #region ApiTrace

    [Fact]
    public Task PlacingAnOrder_PublishesAndHandlesOrderPlaced() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([
                ExpectTrace(Span("POST /carts/{id}/orders",
                    Span("publish OrderPlaced",
                        Span("handle OrderPlaced"))))
            ], ct);

    #endregion

    #region ApiMetric

    [Fact]
    public Task PlacingAnOrder_CountsAWebOrder() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([
                ExpectMetric("orders.placed",
                    new Dictionary<string, string>
                    {
                        ["channel"] = "web",
                        ["cart.id"] = cartId.ToString()
                    },
                    increase: 1)
            ], ct);

    #endregion
}

using System.Globalization;
using Ogooreck.Api.Tests.API.Messages.Fixtures;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Telemetry;

public class TraceTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private const string PlaceOrderSpan = "POST /carts/{id}/orders";

    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public Task ExpectTrace_MatchesTheSpanTreeOfTheWhenRequest() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectResponse(Created), ExpectTrace(Span(PlaceOrderSpan, Span("publish OrderPlaced")))],
                TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExpectTrace_WithAWrongChild_FailsAtTheDeadlineDescribingTheFinishedSpans()
    {
        await using var spec = api.With(setup => setup.Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(2)));
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given(AddProduct(cartId, "socks"))
                .When(PlaceOrder(cartId))
                .Then([ExpectTrace(Span(PlaceOrderSpan, Span("publish OrderCancelled")))],
                    TestContext.Current.CancellationToken));

        Assert.StartsWith(
            $"When request POST /carts/{cartId}/orders: ExpectTrace(Span({PlaceOrderSpan}, Span(publish OrderCancelled))): No span 'publish OrderCancelled' below '{PlaceOrderSpan}', when the deadline of 00:00:02 passed.",
            error.Message);
        Assert.Contains("Finished spans of the test:", error.Message);
        Assert.Contains($"  POST /carts/{{id}}/products (trace ", error.Message);
        Assert.Contains($"  {PlaceOrderSpan} (trace ", error.Message);
        Assert.Contains("  publish OrderPlaced (trace ", error.Message);
    }

    [Fact]
    public async Task ExpectTrace_MatchesSpanAttributes()
    {
        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectTrace(Span(PlaceOrderSpan, new Dictionary<string, string> { ["cart.id"] = cartId.ToString() }))],
                TestContext.Current.CancellationToken);

        await using var spec = api.With(setup => setup.Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(1)));
        var secondCartId = Guid.NewGuid();
        var otherCartId = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given(AddProduct(secondCartId, "socks"))
                .When(PlaceOrder(secondCartId))
                .Then([ExpectTrace(Span(PlaceOrderSpan, new Dictionary<string, string> { ["cart.id"] = otherCartId.ToString() }))],
                    TestContext.Current.CancellationToken));

        Assert.Contains(
            $"No span '{PlaceOrderSpan}' with attributes {{cart.id={otherCartId}}}, the spans of that name have [{{cart.closed=true, cart.id={secondCartId}, cart.total=0.75,",
            error.Message);
    }

    [Fact]
    public async Task ExpectTrace_IgnoresTheSpansOfAParallelTest()
    {
        await using var spec = api.With(setup => setup.Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(2)));
        var otherCartId = Guid.NewGuid();
        var expected = ExpectTrace(Span(PlaceOrderSpan, Span("publish OrderPlaced")));

        var otherTest = spec.Given(AddProduct(otherCartId, "socks"))
            .When(PlaceOrder(otherCartId))
            .Then([ExpectResponse(Created), expected], TestContext.Current.CancellationToken);
        var thisTest = spec.Given(AddProduct(cartId, "socks"), ConfirmCart(cartId))
            .When(PlaceOrder(cartId))
            .Then([ExpectError(Conflict), expected], TestContext.Current.CancellationToken);

        await otherTest;
        var error = await Assert.ThrowsAsync<AssertionException>(() => thisTest);
        Assert.Contains($"When request POST /carts/{cartId}/orders: ExpectTrace(", error.Message);
        Assert.Contains($"No span 'publish OrderPlaced' below '{PlaceOrderSpan}'", error.Message);
    }

    [Fact]
    public async Task ExpectTrace_WaitsForASpanThatFinishesAfterTheResponse()
    {
        OrderHandling.Hold(cartId);

        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([
                ExpectResponse(Created, Satisfying(_ =>
                {
                    OrderHandling.Release(cartId);
                    return Task.CompletedTask;
                })),
                ExpectTrace(Span(PlaceOrderSpan, Span("publish OrderPlaced", Span("handle OrderPlaced"))))
            ], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AttributeValues_AreStringsLikeJavaStringValueOf_UnderPlPlCulture()
    {
        var (culture, defaultCulture) = (CultureInfo.CurrentCulture, CultureInfo.DefaultThreadCurrentCulture);
        CultureInfo.CurrentCulture = CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
        try
        {
            var spec = api;
            await spec.Given(AddProduct(cartId, "socks"), AddProduct(cartId, "shoes"))
                .When(PlaceOrder(cartId))
                .Then([ExpectTrace(Span(PlaceOrderSpan,
                        new Dictionary<string, string> { ["cart.closed"] = "true", ["cart.total"] = "1.5" }))],
                    TestContext.Current.CancellationToken);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
        }
    }

    [Fact]
    public async Task ExpectTrace_MatchesSiblingsAndSpansFurtherBelow()
    {
        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectTrace(Span(PlaceOrderSpan, Span("publish OrderPlaced"), Span("handle OrderPlaced")))],
                TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("missing", "No span 'missing'")]
    [InlineData("wrongAttributes", "No span 'POST /carts/{id}/orders' with attributes {cart.id=other}, the spans of that name have [{cart.closed=true,")]
    [InlineData("childNotBelow", "No span 'POST /carts/{id}/orders' below 'publish OrderPlaced'")]
    [InlineData("childWithWrongAttributes", "No span 'publish OrderPlaced' with attributes {messaging.message.id=other} below 'POST /carts/{id}/orders', the spans of that name have [{messaging.message.correlation_id=")]
    [InlineData("mismatchDeeperDown", "No span 'POST /carts/{id}/orders' below 'publish OrderPlaced'")]
    public async Task ExpectTrace_FailsWithTheFirstRuleTheTreeBreaks(string tree, string mismatch)
    {
        var expected = tree switch
        {
            "missing" => Span("missing"),
            "wrongAttributes" => Span(PlaceOrderSpan, new Dictionary<string, string> { ["cart.id"] = "other" }),
            "childNotBelow" => Span("publish OrderPlaced", Span(PlaceOrderSpan)),
            "childWithWrongAttributes" => Span(PlaceOrderSpan,
                Span("publish OrderPlaced", new Dictionary<string, string> { ["messaging.message.id"] = "other" })),
            _ => Span(PlaceOrderSpan, Span("publish OrderPlaced", Span(PlaceOrderSpan)))
        };
        await using var spec = api.With(setup => setup.Clock(new PassingClock()).Deadline(TimeSpan.FromMilliseconds(500)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given(AddProduct(cartId, "socks"))
                .When(PlaceOrder(cartId))
                .Then([ExpectTrace(expected)], TestContext.Current.CancellationToken));

        Assert.Contains($"ExpectTrace({expected}): {mismatch}", error.Message);
    }

    [Fact]
    public async Task ExpectTrace_WithNoSpansOfTheTest_FailsListingNone()
    {
        await using var spec = api.With(setup => setup.Clock(new PassingClock()).Deadline(TimeSpan.FromMilliseconds(500)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(Action(_ => Task.CompletedTask))
                .Then([ExpectTrace(Span("never"))], TestContext.Current.CancellationToken));

        Assert.Equal(
            $"When action: ExpectTrace(Span(never)): No span 'never', when the deadline of 00:00:00.5000000 passed.{Environment.NewLine}Finished spans of the test:{Environment.NewLine}  Ogooreck action (trace ",
            error.Message[..error.Message.IndexOf("(trace ", StringComparison.Ordinal)] + "(trace ");
    }

    [Fact]
    public async Task CancellingThen_WhileExpectTraceWaits_NamesTheExpectation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.Given()
                .When(GetCart(cartId))
                .Then([ExpectTrace(Span("never"))], cancellation.Token));

        Assert.Equal(
            $"Then was cancelled while ExpectTrace(Span(never)) was waiting, after the When request GET /carts/{cartId}.",
            error.Message);
    }

    [Fact]
    public async Task SpansOfGivenSteps_CountForExpectTrace()
    {
        await api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
            .When(GetCart(cartId))
            .Then([ExpectTrace(Span(PlaceOrderSpan, Span("publish OrderPlaced")))], TestContext.Current.CancellationToken);
    }
}

using Ogooreck.Api.Tests.API.Messages.Fixtures;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static Ogooreck.Api.Tests.API.Telemetry.TestSpans;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Telemetry;

public class CorrelatedTraceTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public Task ExpectCorrelatedTrace_PassesWhenAllSpansShareTheCorrelationId() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId, "c1"))
            .Then([
                ExpectTrace(Span("POST /carts/{id}/orders", Span("publish OrderPlaced", Span("handle OrderPlaced")))),
                ExpectCorrelatedTrace()
            ], TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExpectCorrelatedTrace_FailsNamingTheSpanAndBothIds()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(Action(_ => Emit(
                    ("handle CartOpened", [(CorrelationId, "c1")]),
                    ("handle ProductAdded", [(CorrelationId, "c1")]),
                    ("handle OrderPlaced", [(CorrelationId, "c2")]))))
                .Then([ExpectCorrelatedTrace()], TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "When action: ExpectCorrelatedTrace(): Span 'handle OrderPlaced' carries correlation 'c2', but its trace carries 'c1'.",
            error.Message);
        Assert.Contains("Finished spans of the test:", error.Message);
    }

    [Fact]
    public async Task ExpectCorrelatedTrace_FailsWhenCausationSpanIsNotBelowTheHandlingSpan()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(Action(_ => Emit(
                    ("publish OrderPlaced", [(CorrelationId, "c1"), (MessageId, "m1")]),
                    ("handle OrderPlaced", [(CorrelationId, "c1"), (CausationId, "m1")]))))
                .Then([ExpectCorrelatedTrace()], TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "When action: ExpectCorrelatedTrace(): Span 'handle OrderPlaced' was caused by message m1, but is not below 'publish OrderPlaced' that handled it.",
            error.Message);
    }

    [Fact]
    public async Task ExpectCorrelatedTrace_FailsWhenNoSpanHasACorrelationId()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(GetCart(cartId))
                .Then([ExpectCorrelatedTrace()], TestContext.Current.CancellationToken));

        Assert.StartsWith(
            $"When request GET /carts/{cartId}: ExpectCorrelatedTrace(): No span carries the attribute messaging.message.correlation_id.{Environment.NewLine}Finished spans of the test:{Environment.NewLine}  POST /carts/{{id}}/products (trace ",
            error.Message);
    }

    [Fact]
    public async Task ExpectCorrelatedTrace_FailsWhenTheHandlingSpanIsInAGivenStepsTrace()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(Action(_ => Emit(("publish OrderPlaced", [(CorrelationId, "c1"), (MessageId, "m1")]))))
                .When(Action(_ => Emit(("handle OrderPlaced", [(CorrelationId, "c1"), (CausationId, "m1")]))))
                .Then([ExpectCorrelatedTrace()], TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "When action: ExpectCorrelatedTrace(): Span 'handle OrderPlaced' was caused by message m1, but is not below 'publish OrderPlaced' that handled it.",
            error.Message);
    }

    [Fact]
    public async Task ExpectCorrelatedTrace_FailsWhenNoSpanHandledTheCausingMessage()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(Action(_ => Emit(("handle OrderPlaced", [(CorrelationId, "c1"), (CausationId, "m9")]))))
                .Then([ExpectCorrelatedTrace()], TestContext.Current.CancellationToken));

        Assert.StartsWith(
            "When action: ExpectCorrelatedTrace(): Span 'handle OrderPlaced' was caused by message m9, but no span handled that message.",
            error.Message);
    }

    [Fact]
    public async Task ExpectCorrelatedTrace_RunsOnceAfterCatchUp()
    {
        var handled = new Recorded<string>();
        string? whenTraceparent = null;
        var catchUps = 0;
        await using var spec = api.With(setup => setup
            .Recording(handled, message => new Observed(message, whenTraceparent), _ =>
            {
                catchUps++;
                if (whenTraceparent is not null && catchUps == 1)
                {
                    EmitBelow(whenTraceparent, "handle OrderPlaced late", (CorrelationId, "c1"));
                    handled.Add("OrderPlaced handled");
                }

                return Task.CompletedTask;
            }));

        await spec.Given()
            .When(Action(ctx =>
            {
                whenTraceparent = ctx.Traceparent;
                return Task.CompletedTask;
            }))
            .Then([ExpectCorrelatedTrace()], TestContext.Current.CancellationToken);

        Assert.Equal(2, catchUps);
    }
}

using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Sample.Wolverine.Events;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Wolverine.Tests.ShopRequests;

namespace Ogooreck.Wolverine.Tests;

public class EventuallyTests(AppApi api)
{
    private static readonly object PlacedView = new { status = "Placed" };

    private readonly Guid orderId = Guid.NewGuid();
    private readonly List<string> traceparents = [];

    private TestStep[] PlacedOrder => [AddProduct(orderId, "socks"), PlaceOrder(orderId)];

    [Fact]
    public async Task Eventually_ResendsUntilTheReadModelShowsPlaced()
    {
        await api.Given(PlacedOrder)
            .When(Eventually(CountedGetOrder()))
            .Then([ExpectResponse(OK, Body(PlacedView))], TestContext.Current.CancellationToken);

        Assert.Equal(3, traceparents.Count);
    }

    [Fact]
    public async Task Eventually_AtTheDeadline_FailsWithTheLastResponseStatusAndBody()
    {
        var clock = new FakeTimeProvider();
        await using var spec = api.With(setup => setup.Clock(clock).Deadline(TimeSpan.FromSeconds(2)));
        var getOrderTakingTheDeadline = Request(async client =>
        {
            clock.Advance(TimeSpan.FromSeconds(2));
            return await client.GetAsync($"/orders/{orderId}", TestContext.Current.CancellationToken);
        });

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given()
                .When(Eventually(getOrderTakingTheDeadline))
                .Then([ExpectResponse(OK, Body(PlacedView))], TestContext.Current.CancellationToken));

        Assert.StartsWith(
            $"When eventually request GET /orders/{orderId}: the response expectations still failed when the deadline of 00:00:02 passed, after 1 attempts. Last response: 200 OK. Body: {{\"id\":\"{orderId}\",\"status\":\"Pending\"}}. The last failure: When eventually request GET /orders/{orderId}: response body",
            error.Message);
        Assert.IsType<AssertionException>(error.InnerException);
    }

    [Fact]
    public async Task NoEventually_SendsOnceAndFails()
    {
        await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(PlacedOrder)
                .When(CountedGetOrder())
                .Then([ExpectResponse(OK, Body(PlacedView))], TestContext.Current.CancellationToken));

        Assert.Single(traceparents);
    }

    [Fact]
    public async Task EventuallyAsAGivenStep_FailsSayingItIsAllowedOnlyAsTheWhenStep()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(orderId, "socks"), Eventually(GetOrder(orderId)))
                .When(GetOrder(orderId))
                .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        Assert.Equal(
            "Given step 2 eventually request: Eventually(...) is allowed only as the When step; use the request without Eventually in Given.",
            error.Message);
    }

    [Fact]
    public void Eventually_OfAStepOtherThanARequest_ThrowsSayingItTakesARequest()
    {
        var error = Assert.Throws<ArgumentException>(() => Eventually(Attempt(GetOrder(Guid.NewGuid()))));

        Assert.StartsWith(
            "Eventually(...) takes a Request(...) step; Attempt, Action, Publish and Eventually steps can't be re-sent.",
            error.Message);
    }

    [Fact]
    public async Task Eventually_CountsOnlyTheFinalAttemptsMessages_AndChecksThemOnce()
    {
        await api.Given(PlacedOrder)
            .When(Eventually(CountedGetOrder()))
            .Then([ExpectResponse(OK, Body(PlacedView)), ExpectPublished(new OrderViewed(orderId))],
                TestContext.Current.CancellationToken);

        Assert.Equal(3, traceparents.Count);
    }

    [Fact]
    public Task Eventually_CountsEveryAttemptsMeasurements() =>
        api.Given(PlacedOrder)
            .When(Eventually(CountedGetOrder()))
            .Then([
                ExpectResponse(OK, Body(PlacedView)),
                ExpectMetric("http.server.request.duration", new Dictionary<string, string> { ["http.route"] = "/orders/{id}" }, 3)
            ], TestContext.Current.CancellationToken);

    [Fact]
    public async Task Eventually_WithAnExpectErrorExpectation_ResendsUntilTheErrorStatusAppears()
    {
        var holds = 0;

        await api.Given()
            .When(Eventually(Request(client =>
            {
                holds++;
                return client.PostAsync($"/orders/{orderId}/holds", null, TestContext.Current.CancellationToken);
            })))
            .Then([ExpectError(Conflict, new { title = "Hold limit reached" })], TestContext.Current.CancellationToken);

        Assert.Equal(3, holds);
    }

    [Fact]
    public async Task Eventually_WithNoResponseExpectations_SendsOnce()
    {
        await api.Given(PlacedOrder)
            .When(Eventually(CountedGetOrder()))
            .Then([ExpectPublished(new OrderViewed(orderId))], TestContext.Current.CancellationToken);

        Assert.Single(traceparents);
    }

    [Fact]
    public async Task Eventually_RequestsEachCarryTheirOwnTraceparent()
    {
        await api.Given(PlacedOrder)
            .When(Eventually(CountedGetOrder()))
            .Then([ExpectResponse(OK, Body(PlacedView))], TestContext.Current.CancellationToken);

        Assert.Equal(3, traceparents.Select(TraceIdOf).Distinct().Count());
    }

    [Fact]
    public async Task Eventually_CancelledThroughTheToken_EndsWithOperationCanceledException()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cancelledAfterTheFirstAttempt = Request(async client =>
        {
            var response = await client.GetAsync($"/orders/{orderId}", TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            return response;
        });

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.Given()
                .When(Eventually(cancelledAfterTheFirstAttempt))
                .Then([ExpectResponse(OK, Body(PlacedView))], cancellation.Token));

        Assert.Equal($"Then was cancelled while the When eventually request GET /orders/{orderId} was running.",
            error.Message);
    }

    [Fact]
    public async Task Eventually_SpansOfEveryAttemptCount_ForExpectTrace()
    {
        await api.Given(PlacedOrder)
            .When(Eventually(Request(async client =>
            {
                if (traceparents.Count == 0)
                    await client.PostAsync("/stock/socks/checks", null, TestContext.Current.CancellationToken);
                var response = await client.GetAsync($"/orders/{orderId}", TestContext.Current.CancellationToken);
                traceparents.Add(response.RequestMessage!.Headers.GetValues("traceparent").Single());
                return response;
            })))
            .Then([ExpectResponse(OK, Body(PlacedView)), ExpectTrace(Span("receive",
                new Dictionary<string, string> { ["messaging.message_type"] = typeof(StockChecked).FullName! }))], TestContext.Current.CancellationToken);
    }

    private TestStep CountedGetOrder() =>
        Request(async client =>
        {
            var response = await client.GetAsync($"/orders/{orderId}", TestContext.Current.CancellationToken);
            traceparents.Add(response.RequestMessage!.Headers.GetValues("traceparent").Single());
            return response;
        });

    private static string TraceIdOf(string traceparent) => ActivityContext.Parse(traceparent, null).TraceId.ToHexString();
}

using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Telemetry;

public class MetricTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private static readonly Dictionary<string, string> Web = new() { ["channel"] = "web" };

    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public Task ExpectMetric_CountsAnOrderOfTheWhenStep() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectResponse(Created), ExpectMetric("orders.placed", Web, 1)], TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExpectMetric_IgnoresAParallelTestsMeasurement()
    {
        var otherCartId = Guid.NewGuid();
        using var bothPlaced = new CountdownEvent(2);
        TestStep PlaceOrderAlongsideTheOtherTest(Guid id) =>
            Request(async client =>
            {
                var response = await client.PostAsync($"/carts/{id}/orders", null, TestContext.Current.CancellationToken);
                bothPlaced.Signal();
                bothPlaced.Wait(TestContext.Current.CancellationToken);
                return response;
            });

        await Task.WhenAll(
            Task.Run(() => api.Given(AddProduct(cartId, "socks"))
                .When(PlaceOrderAlongsideTheOtherTest(cartId))
                .Then([ExpectMetric("orders.placed", Web, 1)], TestContext.Current.CancellationToken), TestContext.Current.CancellationToken),
            Task.Run(() => api.Given(AddProduct(otherCartId, "socks"))
                .When(PlaceOrderAlongsideTheOtherTest(otherCartId))
                .Then([ExpectMetric("orders.placed", Web, 1)], TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExpectMetric_FailsAtOnceWhenTheIncreaseExceedsTheExpectation()
    {
        var otherCartId = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"), AddProduct(otherCartId, "socks"))
                .When(PlaceOrders(cartId, otherCartId))
                .Then([ExpectMetric("orders.placed", Web, 1)], TestContext.Current.CancellationToken));

        Assert.Equal(
            $"When request POST /carts/{otherCartId}/orders: ExpectMetric(orders.placed, {{channel=web}}, 1): expected an increase of 1 since the baseline, but found 2, which is more.",
            error.Message);
    }

    [Fact]
    public Task ExpectMetric_SumsTheIncreaseAcrossSeriesWhoseTagsIncludeTheGivenTags()
    {
        var otherCartId = Guid.NewGuid();
        return api.Given(AddProduct(cartId, "socks"), AddProduct(otherCartId, "socks"))
            .When(PlaceOrders(cartId, otherCartId))
            .Then([
                ExpectMetric("orders.placed", Web, 2),
                ExpectMetric("orders.placed", new Dictionary<string, string> { ["channel"] = "web", ["cart.id"] = cartId.ToString() }, 1)
            ], TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task ExpectMetric_CountsAMissingBaselineAsZero() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectMetric("orders.placed", new Dictionary<string, string> { ["cart.id"] = cartId.ToString() }, 1)],
                TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExpectMetric_CountsHistogramMeasurements_NotTheirSum()
    {
        var route = new Dictionary<string, string> { ["http.route"] = "/carts/{id}/orders" };
        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectMetric("http.server.request.duration", route, 1)], TestContext.Current.CancellationToken);

        var otherCartId = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(otherCartId, "socks"))
                .When(PlaceOrder(otherCartId))
                .Then([ExpectMetric("http.server.request.duration", route, 0.034)], TestContext.Current.CancellationToken));

        Assert.Contains("expected an increase of 0.034 since the baseline, but found 1, which is more.", error.Message);
    }

    [Fact]
    public Task ExpectMetric_CountsAMeasurementWithoutATraceIdByTags() =>
        api.Given()
            .When(ExpireCart(cartId))
            .Then([
                ExpectResponse(Accepted),
                ExpectMetric("carts.expired", new Dictionary<string, string> { ["cart.id"] = cartId.ToString() }, 1)
            ], TestContext.Current.CancellationToken);

    [Fact]
    public async Task ExpectMetric_WithNoMatchingTags_FailsAtTheDeadlineWithIncreaseZero()
    {
        await using var spec = api.With(setup => setup.Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(2)));
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            spec.Given(AddProduct(cartId, "socks"))
                .When(PlaceOrder(cartId))
                .Then([ExpectMetric("orders.placed", new Dictionary<string, string> { ["channel"] = "phone" }, 1)],
                    TestContext.Current.CancellationToken));

        Assert.Equal(
            $"When request POST /carts/{cartId}/orders: ExpectMetric(orders.placed, {{channel=phone}}, 1): expected an increase of 1 since the baseline, but found 0 when the deadline of 00:00:02 passed.",
            error.Message);
    }

    [Fact]
    public async Task CancellingThen_WhileExpectMetricWaits_NamesTheExpectation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var telemetry = new CancellingAtTheFirstCheck(cancellation);
        await using var spec = api.With(setup => setup.Telemetry(telemetry));

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            spec.Given()
                .When(GetCart(cartId))
                .Then([ExpectMetric("orders.placed", new Dictionary<string, string> { ["cart.id"] = cartId.ToString() }, 1)],
                    cancellation.Token));

        Assert.Equal(
            $"Then was cancelled while ExpectMetric(orders.placed, {{cart.id={cartId}}}, 1) was waiting, after the When request GET /carts/{cartId}.",
            error.Message);
    }

    private static TestStep PlaceOrders(Guid first, Guid second) =>
        Request(async client =>
        {
            await client.PostAsync($"/carts/{first}/orders", null, TestContext.Current.CancellationToken);
            return await client.PostAsync($"/carts/{second}/orders", null, TestContext.Current.CancellationToken);
        });

    private sealed class CancellingAtTheFirstCheck(CancellationTokenSource cancellation): ITelemetrySource
    {
        private readonly ITelemetrySource inProcess = TelemetrySources.InProcess();
        private int collects;

        public void Attach(Recorded<RecordedSpan> spans, Recorded<RecordedMeasurement> measurements) =>
            inProcess.Attach(spans, measurements);

        public async Task Collect(CancellationToken ct)
        {
            await inProcess.Collect(ct);
            if (Interlocked.Increment(ref collects) == 2)
                await cancellation.CancelAsync();
        }

        public ValueTask DisposeAsync() => inProcess.DisposeAsync();
    }
}

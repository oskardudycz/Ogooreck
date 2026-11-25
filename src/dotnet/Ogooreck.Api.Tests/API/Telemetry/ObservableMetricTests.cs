using System.Diagnostics.Metrics;
using Ogooreck.Api.Tests.API.Telemetry.Fixtures;
using Ogooreck.Assertions;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Telemetry;

[Collection(nameof(SerialMetrics))]
public class ObservableMetricTests
{
    private static readonly Dictionary<string, string> AnyTags = new();

    [Fact]
    public async Task ExpectMetric_OnAnObservableCounter_UsesCumulativeValues()
    {
        await using var spec = For<Program>();
        await spec.Given(FiveOpenCarts())
            .When(OpenTwoCarts())
            .Then([ExpectMetric("carts.open", AnyTags, 2)], TestContext.Current.CancellationToken);

        await using var other = For<Program>().Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            other.Given(FiveOpenCarts())
                .When(OpenTwoCarts())
                .Then([ExpectMetric("carts.open", AnyTags, 7)], TestContext.Current.CancellationToken));

        Assert.Equal(
            "When request POST /carts: ExpectMetric(carts.open, {}, 7): expected an increase of 7 since the baseline, but found 2 when the deadline of 00:00:02 passed.",
            error.Message);
    }

    [Fact]
    public async Task ExpectMetric_OnTwoLiveHosts_SumsTheirObservableValuesWhicheverIsReadFirst()
    {
        await using var older = For<Program>().Deadline(TimeSpan.FromSeconds(2));
        await using var younger = For<Program>().Deadline(TimeSpan.FromSeconds(2));
        await older.Given(CreateCart()).When(CreateCart()).Then([ExpectResponse(Created)], TestContext.Current.CancellationToken);
        await younger.Given(CreateCart()).When(CreateCart()).Then([ExpectResponse(Created)], TestContext.Current.CancellationToken);

        await older.Given(FiveOpenCarts()).When(OpenTwoCarts())
            .Then([ExpectMetric("carts.open", AnyTags, 2)], TestContext.Current.CancellationToken);
        await younger.Given(FiveOpenCarts()).When(OpenTwoCarts())
            .Then([ExpectMetric("carts.open", AnyTags, 2)], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExpectMetric_OnACumulativeSeriesWithNoBaselineValue_CountsTheIncreaseFromZero()
    {
        var cartId = Guid.NewGuid();
        var name = $"carts.pending.{cartId:N}";
        var pending = new Dictionary<string, int>();
        using var meter = new Meter($"Ogooreck.Api.Tests.{cartId:N}");
        meter.CreateObservableGauge(name, () =>
            pending.Select(each => new Measurement<int>(each.Value, new KeyValuePair<string, object?>("cart.id", each.Key))).ToList());
        await using var spec = For<Program>();

        await spec.Given()
            .When(Action(_ =>
            {
                pending[cartId.ToString()] = 4;
                return Task.CompletedTask;
            }))
            .Then([ExpectMetric(name, new Dictionary<string, string> { ["cart.id"] = cartId.ToString() }, 4)],
                TestContext.Current.CancellationToken);
    }

    private static Ogooreck.API.TestStep[] FiveOpenCarts() =>
        [CreateCart(), CreateCart(), CreateCart(), CreateCart(), CreateCart()];

    private static Ogooreck.API.TestStep OpenTwoCarts() =>
        Request(async client =>
        {
            await client.PostAsync("/carts", null, TestContext.Current.CancellationToken);
            return await client.PostAsync("/carts", null, TestContext.Current.CancellationToken);
        });
}

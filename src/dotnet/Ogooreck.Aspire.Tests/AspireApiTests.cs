using System.Globalization;
using System.Text.Json;
using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;
using static Ogooreck.Aspire.Tests.ShopRequests;

namespace Ogooreck.Aspire.Tests;

[Collection("Aspire apps")]
public class AspireApiTests(AspireApi api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> Cart(Guid cartId) => new() { ["cart.id"] = cartId.ToString() };

    [Fact]
    public async Task PlacingAnOrder_ExportsTheRequestSpanOverOtlp()
    {
        var cartId = Guid.NewGuid();

        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectResponse(Created), ExpectTrace(Span("POST /carts/{id}/orders", Cart(cartId)))], Ct);
    }

    [Fact]
    public async Task SpanAttributes_ArriveOverOtlpAsStrings_UnderPolishCulture()
    {
        var cartId = Guid.NewGuid();
        var previous = CultureInfo.DefaultThreadCurrentCulture;
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("pl-PL");

        try
        {
            await api.Given(AddProduct(cartId, "socks"))
                .When(PlaceOrder(cartId))
                .Then([
                    ExpectTrace(Span("POST /carts/{id}/orders",
                        new Dictionary<string, string>
                        {
                            ["cart.id"] = cartId.ToString(), ["cart.closed"] = "true", ["cart.total"] = "1.5"
                        }))
                ], Ct);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = previous;
        }
    }

    [Fact]
    public async Task PlacingAnOrder_ExportsTheCumulativeCounterOverOtlp()
    {
        var cartId = Guid.NewGuid();

        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectMetric("orders.placed", Cart(cartId), 1)], Ct);
    }

    [Fact]
    public async Task KafkaPlugConsuming_RecordsTheProducedMessage_ReadingTheAppsOwnHeaderNames()
    {
        var cartId = Guid.NewGuid();

        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([
                ExpectResponse(Created),
                ExpectPublished(new OrderPlaced(cartId, 1.5))
            ], Ct);
    }

    [Fact]
    public async Task KafkaPlugConsuming_ReadsTheAppsOwnIdHeaders_SoCausationIsChecked()
    {
        var cartId = Guid.NewGuid();
        var kafka = AspireApi.App.Kafka;

        await api.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([
                ExpectCausation(PublishedIn(kafka.Published, new OrderPlaced(cartId, 1.5),
                    SentIn(kafka.Published, new ShipOrder(cartId))))
            ], Ct);
    }

    [Fact]
    public async Task EveryRequest_CarriesATraceparentWithAUniqueTraceIdPerStep()
    {
        var traceparents = new List<string>();
        var collect = Satisfying(async response =>
            traceparents.Add(JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement
                .GetProperty("traceparent").GetString()!));

        await api.Given(Echo()).When(Echo()).Then([ExpectResponse(OK, collect)], Ct);
        await api.Given().When(Echo()).Then([ExpectResponse(OK, collect)], Ct);

        Assert.Equal(2, traceparents.Count);
        Assert.NotEqual(traceparents[0].Split('-')[1], traceparents[1].Split('-')[1]);
    }

    [Fact]
    public async Task ForWithTheAddressFunction_SendsRequestsToTheApp()
    {
        await using var other = For(AspireApi.App.Api);

        await other.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);
    }

    [Fact]
    public async Task SubclassCallingHost_SendsRequestsToTheApp()
    {
        await using var other = new HostCallingAspireApi();

        await other.Given().When(Echo()).Then([ExpectResponse(OK)], Ct);
    }

    private sealed class HostCallingAspireApi(): ApiSpecification(spec => spec.Host(AspireApi.App.Api));
}

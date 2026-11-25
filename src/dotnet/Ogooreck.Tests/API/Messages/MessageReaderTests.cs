using System.Text;
using System.Text.Json;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests.API.Messages;

public class MessageReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public record OrderPlaced(Guid CartId);

    public record ShipOrder(Guid CartId);

    public static class Other
    {
        public record OrderPlaced(int OrderId);
    }

    private sealed record Raw(Dictionary<string, string> Headers, string Body);

    private readonly Guid cartId = Guid.NewGuid();

    private static Observed Read(MessageReader reader, Dictionary<string, string> headers, string body) =>
        reader.Read(name => headers.GetValueOrDefault(name), Encoding.UTF8.GetBytes(body));

    private static Dictionary<string, string> Recommended(string type, string? traceparent = null) =>
        new()
        {
            ["messageType"] = type,
            ["messageId"] = "m1",
            ["correlationId"] = "c1",
            ["causationId"] = "m0",
            ["traceparent"] = traceparent ?? "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
        };

    private ApiSpecification Api(Recorded<Raw> recorded, MessageReader reader) =>
        For(() => Task.FromResult(new Uri("http://localhost")))
            .Recording(recorded, raw => Read(reader, raw.Headers, raw.Body), _ => Task.CompletedTask)
            .Deadline(TimeSpan.FromSeconds(5));

    private static TestStep Delivering(Recorded<Raw> recorded, string type, string body) =>
        Action(context =>
        {
            recorded.Add(new Raw(Recommended(type, context.Traceparent), body));
            return Task.CompletedTask;
        });

    [Fact]
    public void recommendedHeaders_giveTheMessageItsTraceparentAndIds()
    {
        var reader = new MessageReader().MessageTypes(typeof(OrderPlaced));

        var observed = Read(reader, Recommended("OrderPlaced"), $$"""{"cartId":"{{cartId}}"}""");

        Assert.Equal(
            new Observed(new OrderPlaced(cartId), "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", "m1", "c1",
                "m0"),
            observed);
    }

    [Fact]
    public void appsOwnHeaderNames_giveTheSameResult()
    {
        var reader = new MessageReader().MessageTypes(typeof(OrderPlaced)).Headers(new MessageHeaders
        {
            MessageType = "message-type",
            MessageId = "message-id",
            CorrelationId = "correlation-id",
            CausationId = "causation-id",
            Traceparent = "trace-parent"
        });
        var headers = new Dictionary<string, string>
        {
            ["message-type"] = "OrderPlaced",
            ["message-id"] = "m1",
            ["correlation-id"] = "c1",
            ["causation-id"] = "m0",
            ["trace-parent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
        };

        var observed = Read(reader, headers, $$"""{"cartId":"{{cartId}}"}""");

        Assert.Equal(new OrderPlaced(cartId), observed.Message);
        Assert.Equal(("m1", "c1", "m0"), (observed.MessageId, observed.CorrelationId, observed.CausationId));
        Assert.Equal("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", observed.Traceparent);
    }

    [Fact]
    public void jsonOptions_readTheBody()
    {
        var reader = new MessageReader().MessageTypes(typeof(OrderPlaced))
            .Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        var observed = Read(reader, Recommended("OrderPlaced"), $$"""{"cart_id":"{{cartId}}"}""");

        Assert.Equal(new OrderPlaced(cartId), observed.Message);
    }

    [Fact]
    public void ownLookup_replacesTheListedTypes()
    {
        var reader = new MessageReader().MessageTypes(name => name == "placed" ? typeof(OrderPlaced) : null);

        var observed = Read(reader, Recommended("placed"), $$"""{"cartId":"{{cartId}}"}""");
        var unknown = Read(reader, Recommended("other"), "{}");

        Assert.Equal(new OrderPlaced(cartId), observed.Message);
        var unknownMessage = Assert.IsType<UnknownMessage>(unknown.Message);
        Assert.Equal("other", unknownMessage.TypeHeader);
        Assert.Empty(unknownMessage.ListedTypes);
    }

    [Fact]
    public void messageReader_matchesFullNameBeforeName()
    {
        var reader = new MessageReader().MessageTypes(typeof(OrderPlaced), typeof(Other.OrderPlaced));

        var byFullName = Read(reader, Recommended(typeof(Other.OrderPlaced).FullName!), """{"orderId":5}""");
        var byName = Read(reader, Recommended("OrderPlaced"), $$"""{"cartId":"{{cartId}}"}""");

        Assert.Equal(new Other.OrderPlaced(5), byFullName.Message);
        Assert.Equal(new OrderPlaced(cartId), byName.Message);
    }

    [Fact]
    public void missingOrUnlistedType_becomesAnUnknownMessageStillCarryingTheIds()
    {
        var reader = new MessageReader().MessageTypes(typeof(OrderPlaced), typeof(ShipOrder));
        var withoutType = Recommended("x");
        withoutType.Remove("messageType");

        var unlisted = Read(reader, Recommended("OrderShipped"), "{}");
        var missing = Read(reader, withoutType, "{}");

        var unlistedMessage = Assert.IsType<UnknownMessage>(unlisted.Message);
        var missingMessage = Assert.IsType<UnknownMessage>(missing.Message);
        Assert.Equal("OrderShipped", unlistedMessage.TypeHeader);
        Assert.Null(missingMessage.TypeHeader);
        Assert.Equal([typeof(OrderPlaced), typeof(ShipOrder)], unlistedMessage.ListedTypes);
        Assert.Equal([typeof(OrderPlaced), typeof(ShipOrder)], missingMessage.ListedTypes);
        Assert.Equal("m1", unlisted.MessageId);
    }

    [Fact]
    public async Task messageReader_unknownMessageType_failsNamingTheValueAndTheListedTypes()
    {
        var recorded = new Recorded<Raw>();
        await using var api = Api(recorded, new MessageReader().MessageTypes(typeof(OrderPlaced), typeof(ShipOrder)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Delivering(recorded, "OrderShipped", "{}")).Then([ExpectPublishedIn(recorded)], Ct));

        Assert.Equal(
            "When action: a message of the When step has an unknown type: its messageType header is OrderShipped, and the listed types are OrderPlaced, ShipOrder. List its type with MessageTypes(...).",
            error.Message);
    }

    [Fact]
    public async Task messageReader_unknownMessageTypeWithoutAnyListedType_namesTheMissingHeaderAndNoTypes()
    {
        var recorded = new Recorded<Raw>();
        await using var api = Api(recorded, new MessageReader().Headers(new MessageHeaders { MessageType = "kind" }));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Delivering(recorded, "OrderShipped", "{}")).Then([ExpectPublishedIn(recorded)], Ct));

        Assert.Equal(
            "When action: a message of the When step has an unknown type: its kind header is missing, and the listed types are none. List its type with MessageTypes(...).",
            error.Message);
    }

    [Fact]
    public async Task messageReader_unknownTypeOfAGivenStepsMessage_isIgnored_andOfTheWhenStepsMessage_fails()
    {
        var recorded = new Recorded<Raw>();
        await using var api = Api(recorded, new MessageReader().MessageTypes(typeof(OrderPlaced)));

        await api.Given(Delivering(recorded, "OrderShipped", "{}"))
            .When(Delivering(recorded, "OrderPlaced", $$"""{"cartId":"{{cartId}}"}"""))
            .Then([ExpectPublishedIn(recorded, new OrderPlaced(cartId))], Ct);
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Delivering(recorded, "OrderShipped", "{}")).Then([ExpectPublishedIn(recorded)], Ct));

        Assert.Contains("its messageType header is OrderShipped", error.Message);
    }

    [Fact]
    public async Task causationOnAnUnknownMessage_failsTheSameWay()
    {
        var recorded = new Recorded<Raw>();
        await using var api = Api(recorded, new MessageReader().MessageTypes(typeof(OrderPlaced)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given()
                .When(Delivering(recorded, "OrderShipped", "{}"))
                .Then([ExpectCausation(PublishedIn(recorded, new OrderPlaced(cartId)))], Ct));

        Assert.Contains("a message of the When step has an unknown type", error.Message);
    }
}

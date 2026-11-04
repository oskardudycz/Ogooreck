using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ogooreck.API;
using TestContext = Ogooreck.API.TestContext;
using Ogooreck.Assertions;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests;

public class HttpExpectationsTests
{
    private static HttpResponseMessage Json(string body) => new() { Content = new StringContent(body) };

    [Fact]
    public async Task ResponseUsesWebDefaultsForRecords()
    {
        using var response = Json("""{"name":"item","quantity":"3"}""");
        Assert.Equal(new Product("item", 3), await response.GetResultFromJson<Product>());
    }

    [Fact]
    public async Task BothResponseOverloadsHonorCustomOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<Status>());
        using var response = Json("""{"status":"Ready"}""");
        Assert.Equal(Status.Ready, (await response.GetResultFromJson<State>(options)).Status);
        var result = new Result(response, new TestContext(), () => new HttpClient());
        Assert.Equal(Status.Ready, (await result.GetResultFromJson<State>(options)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("{")]
    public async Task InvalidOrEmptyJsonFails(string json)
    {
        using var response = Json(json);
        await Assert.ThrowsAsync<JsonException>(() => response.GetResultFromJson<Product>());
    }

    [Fact]
    public async Task JsonNullFailsAnExpectation()
    {
        using var response = Json("null");
        await Assert.ThrowsAsync<AssertionException>(() => response.GetResultFromJson<Product>());
    }

    [Fact]
    public async Task StandardConstructorAttributesAreRespected()
    {
        using var response = Json("""{"name":"item"}""");
        Assert.Equal("item", (await response.GetResultFromJson<PrivateConstructor>()).Name);
    }

    [Fact]
    public async Task BodyExpectationChecksActualValuesAndInvokesCallbacks()
    {
        using var response = Json("""{"name":"item","quantity":3}""");
        await RESPONSE_BODY(new Product("item", 3))(response, new TestContext());
        await Assert.ThrowsAsync<AssertionException>(() =>
            RESPONSE_BODY(new Product("wrong", 3))(response, new TestContext()).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RESPONSE_BODY<Product>((Action<Product>)(_ => throw new InvalidOperationException()))(response, new TestContext()).AsTask());
    }

    [Fact]
    public async Task StatusAndHeaderExpectationsRejectMismatches()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Created)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://localhost/items")
        };
        var context = new TestContext();
        await Assert.ThrowsAsync<AssertionException>(() => OK(response, context).AsTask());
        await Assert.ThrowsAsync<AssertionException>(() => RESPONSE_ETAG_HEADER(42)(response, context).AsTask());
        response.Headers.ETag = new EntityTagHeaderValue("\"42\"", true);
        response.Headers.Location = new Uri("/items/99", UriKind.Relative);
        await RESPONSE_ETAG_HEADER(42)(response, context);
        await RESPONSE_LOCATION_HEADER()(response, context);
        await Assert.ThrowsAsync<AssertionException>(() => RESPONSE_ETAG_HEADER(43)(response, context).AsTask());
        await Assert.ThrowsAsync<AssertionException>(() => RESPONSE_ETAG_HEADER(42, false)(response, context).AsTask());
        await Assert.ThrowsAsync<AssertionException>(() => RESPONSE_LOCATION_HEADER("/other")(response, context).AsTask());
        Assert.Equal(42, response.GetETagValue<int>());
        Assert.Equal(99, response.GetCreatedId<int>());
    }

    public record Product(string Name, int Quantity);
    public enum Status { Ready }
    public record State(Status Status);
    public class PrivateConstructor
    {
        public string Name { get; }
        [JsonConstructor]
        private PrivateConstructor(string name) => Name = name;
    }
}

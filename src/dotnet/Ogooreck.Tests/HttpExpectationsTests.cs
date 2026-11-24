using System.Text.Json;
using System.Text.Json.Serialization;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;

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
    public async Task ResponseHonorsCustomOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<Status>());
        using var response = Json("""{"status":"Ready"}""");
        Assert.Equal(Status.Ready, (await response.GetResultFromJson<State>(options)).Status);
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
    public void CreatedIdIsTheLastSegmentOfTheLocationHeader()
    {
        using var response = new HttpResponseMessage { Headers = { Location = new Uri("/items/99", UriKind.Relative) } };
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

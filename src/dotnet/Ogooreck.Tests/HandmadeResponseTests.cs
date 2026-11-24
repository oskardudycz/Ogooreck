using System.Text;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests;

public class HandmadeResponseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ApiSpecification Api() => For(() => Task.FromResult(new Uri("http://localhost")));

    private static TestStep Respond(Func<HttpResponseMessage> response) => Request(_ => Task.FromResult(response()));

    private static TestStep RespondWithJson(string json) =>
        Respond(() => new HttpResponseMessage(OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    [Fact]
    public Task Body_MatchesNumbersBeyondTheRangeOfADecimal() =>
        Api().Given().When(RespondWithJson("""{"mass":1e30}""")).Then([ExpectResponse(OK, Body(new { mass = 1e30 }))], Ct);

    [Fact]
    public Task Body_MatchesBooleans() =>
        Api().Given().When(RespondWithJson("""{"open":true,"closed":false}""")).Then([ExpectResponse(OK, Body(new { open = true, closed = false }))], Ct);

    [Fact]
    public Task Body_MatchesNulls() =>
        Api().Given().When(RespondWithJson("""{"name":null}""")).Then([ExpectResponse(OK, Body(new { name = (string?)null }))], Ct);

    [Fact]
    public async Task Body_FailsWhenTheResponseArrayHasMoreItemsThanExpected()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            Api().Given().When(RespondWithJson("""{"items":[1,2]}""")).Then([ExpectResponse(OK, Body(new { items = new[] { 1 } }))], Ct));

        Assert.Contains("expected 1 items, but found 2", error.Message);
    }

    [Fact]
    public async Task HtmlContaining_FailsWhenTheResponseHasNoContentType()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            Api().Given()
                .When(Respond(() => new HttpResponseMessage(OK) { Content = new ByteArrayContent([]) }))
                .Then([ExpectResponse(OK, HtmlContaining("<p>"))], Ct));

        Assert.Contains("expected an HTML response, but the content type is missing", error.Message);
    }

    [Fact]
    public async Task CreatedId_FailsNamingAResponseThatHasNoRequest()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            Api().Given(Respond(() => new HttpResponseMessage(Created)))
                .When(Request((_, context) => Task.FromResult(new HttpResponseMessage(OK) { Content = new StringContent(context.CreatedId()) })))
                .Then([ExpectResponse(OK)], Ct));

        Assert.Contains("the first 201 Created response (response 1: 201 Created from  ) has no Location header", error.Message);
    }

    [Fact]
    public async Task CreatedId_FailsNamingAResponseWhoseRequestHasNoAddress()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            Api().Given(Respond(() => new HttpResponseMessage(Created) { RequestMessage = new HttpRequestMessage() }))
                .When(Request((_, context) => Task.FromResult(new HttpResponseMessage(OK) { Content = new StringContent(context.CreatedId()) })))
                .Then([ExpectResponse(OK)], Ct));

        Assert.Contains("(response 1: 201 Created from GET ) has no Location header", error.Message);
    }

    [Fact]
    public async Task Given_ANamedScenarioAndMoreSteps_RunsThemInOrder()
    {
        var order = new List<string>();
        TestStep Step(string name) => Action(_ =>
        {
            order.Add(name);
            return Task.CompletedTask;
        });

        await Api().Given([Step("first"), Step("second")], Step("third")).When(Step("when")).Then([], Ct);

        Assert.Equal(["first", "second", "third", "when"], order);
    }

    [Fact]
    public async Task Then_WithoutACancellationToken_ChecksTheExpectations()
    {
        Func<Expectation[], Task> then = Api().Given().When(Respond(() => new HttpResponseMessage(OK))).Then;

        await then([ExpectResponse(OK)]);
    }

    [Fact]
    public async Task InProcessTelemetry_CollectedBeforeItIsAttached_DoesNothing()
    {
        await using var source = TelemetrySources.InProcess();

        await source.Collect(Ct);
    }

    [Theory]
    [InlineData("items/7", 7)]
    [InlineData("/items/7/", 7)]
    [InlineData("https://example.com/items/7", 7)]
    public void TryGetCreatedId_ReadsTheLastSegmentOfAnyKindOfLocation(string location, int expected)
    {
        using var response = new HttpResponseMessage { Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) } };

        Assert.True(response.TryGetCreatedId<int>(out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/")]
    public void TryGetCreatedId_ReturnsFalseWhenThereIsNoIdInTheLocation(string? location)
    {
        using var response = new HttpResponseMessage();
        if (location is not null)
            response.Headers.Location = new Uri(location, UriKind.Relative);

        Assert.False(response.TryGetCreatedId<int>(out _));
    }

    [Fact]
    public void GetCreatedId_WithoutALocation_Throws()
    {
        using var response = new HttpResponseMessage();

        Assert.Throws<ArgumentOutOfRangeException>(() => response.GetCreatedId<Guid>());
    }

    [Fact]
    public void GetCreatedId_ReadsTheIdAsAStringByDefault()
    {
        using var response = new HttpResponseMessage { Headers = { Location = new Uri("/carts/abc", UriKind.Relative) } };

        Assert.Equal("abc", response.GetCreatedId());
    }
}

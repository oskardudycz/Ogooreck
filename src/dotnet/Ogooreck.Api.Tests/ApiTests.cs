using System.Net.Http.Json;
using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests;

public class Tests: IClassFixture<ApiSpecification<Program>>
{
    private readonly ApiSpecification<Program> API;
    public Tests(ApiSpecification<Program> api) => API = api;

    #region ApiGetSample

    [Fact]
    public Task GetProducts()
    {
        var ct = TestContext.Current.CancellationToken;

        return API.Given()
            .When(Request(client => client.GetAsync("/api/products", ct)))
            .Then([ExpectResponse(OK)], ct);
    }

    #endregion ApiGetSample


    #region ApiPostSample

    [Fact]
    public Task RegisterProduct()
    {
        var ct = TestContext.Current.CancellationToken;

        return API.Given()
            .When(Request(client => client.PostAsJsonAsync("/api/products",
                new RegisterProductRequest("abc-123", "Ogooreck"), ct)))
            .Then([ExpectResponse(Created,
                Location(l => l.StartsWith("/api/products/")))], ct);
    }

    #endregion ApiPostSample

    [Fact]
    public Task CanRegisterAndReadAProductUsingAClassFixture() =>
        API.Given(Request(client => client.PostAsJsonAsync("/api/products",
                new RegisterProductRequest("xunit-v3", "Product"), TestContext.Current.CancellationToken)))
            .When(Request((client, ctx) => client.GetAsync($"/api/products/{ctx.CreatedId<Guid>()}", TestContext.Current.CancellationToken)))
            .Then([ExpectResponse(OK, Body(new { sku = "xunit-v3", name = "Product" }))],
                TestContext.Current.CancellationToken);

    [Fact]
    public async Task DefaultsWorkWithoutAnyExplicitJsonConfiguration()
    {
        var api = API;

        await api.Given(Request(client => client.PostAsJsonAsync("/api/products",
                new RegisterProductRequest("json-default", "Default"), TestContext.Current.CancellationToken)))
            .When(Request((client, ctx) => client.GetAsync($"/api/products/{ctx.CreatedId<Guid>()}", TestContext.Current.CancellationToken)))
            .Then([ExpectResponse(OK, Body(new { sku = "json-default", name = "Default" }))], TestContext.Current.CancellationToken);
    }
}

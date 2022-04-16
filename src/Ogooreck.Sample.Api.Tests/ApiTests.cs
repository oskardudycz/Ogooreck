using Ogooreck.API;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Sample.Api.Tests;

public class Tests: IClassFixture<ApiSpecification<Program>>
{
    private ApiSpecification<Program> API;
    public Tests(ApiSpecification<Program> api) => API = api;

    #region ApiGetSample

    [Fact]
    public Task GetProducts() =>
        API.Given(URI("/api/products"))
            .When(GET)
            .Then(OK);

    #endregion ApiGetSample


    #region ApiPostSample

    [Fact]
    public Task RegisterProduct() =>
        API.Given(
                URI("/api/products"),
                BODY(new RegisterProductRequest("abc-123", "Ogooreck"))
            )
            .When(POST)
            .Then(CREATED);

    #endregion ApiPostSample
}

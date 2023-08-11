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
        API.Given()
            .When(GET, URI("/api/products"))
            .Then(OK);

    #endregion ApiGetSample


    #region ApiPostSample

    [Fact]
    public Task RegisterProduct() =>
        API.Given(
            )
            .When(
                POST,
                URI("/api/products"),
                BODY(new RegisterProductRequest("abc-123", "Ogooreck"))
            )
            .Then(CREATED, RESPONSE_LOCATION_HEADER());

    #endregion ApiPostSample
}

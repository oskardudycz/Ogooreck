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
        API.Given()
            .When(
                POST,
                URI("/api/products"),
                BODY(new RegisterProductRequest("abc-123", "Ogooreck"))
            )
            .Then(CREATED, RESPONSE_LOCATION_HEADER());

    #endregion ApiPostSample

    [Fact]
    public async Task CanRegisterAndReadAProductUsingAClassFixture()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        var created = API.Given()
            .When(
                POST,
                URI("/api/products"),
                BODY(new RegisterProductRequest("xunit-v3", "Product"))
            )
            .Then([CREATED, RESPONSE_LOCATION_HEADER()], ct);

        var id = (await created).Response.GetCreatedId<Guid>();
        await created.AndWhen(GET, URI($"/api/products/{id}"))
            .Then([OK, RESPONSE_BODY(new Product(id, "xunit-v3", "Product"))], ct);
    }
}

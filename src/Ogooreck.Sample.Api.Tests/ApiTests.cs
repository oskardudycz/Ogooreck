using Ogooreck.API;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Sample.Api.Tests;

public class Tests: IClassFixture<ApiSpecification<Program>>
{
    private ApiSpecification<Program> API;
    public Tests(ApiSpecification<Program> api) => API = api;

    [Fact]
    public Task GetProducts() =>
        API.Given(URL("/api/products"))
            .When(GET())
            .Then(OK());

    // [Fact]
    // public Task RegisterProduct() =>
    //     API.Given(URL("/api/products"))
    //         .When(GET())
    //         .Then(OK());
}

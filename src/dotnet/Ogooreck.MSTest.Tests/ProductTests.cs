using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ogooreck.API;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.MSTest.Tests;

[TestClass]
public class ProductTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RegisteringAProduct_RespondsWithCreated()
    {
        var ct = TestContext.CancellationToken;
        await using var api = For<Program>();

        await api.Given()
            .When(Request(client => client.PostAsJsonAsync("/api/products",
                new RegisterProductRequest($"mstest-{Guid.NewGuid()}", "Ogooreck"), ct)))
            .Then([ExpectResponse(Created)], ct);
    }

    [TestMethod]
    public async Task ExpectingTheWrongStatus_FailsWithOgooreckMessageAsAPlainException()
    {
        var ct = TestContext.CancellationToken;
        await using var api = For<Program>();

        var error = await Assert.ThrowsExactlyAsync<Ogooreck.Assertions.AssertionException>(() =>
            api.Given()
                .When(Request(client => client.PostAsJsonAsync("/api/products",
                    new RegisterProductRequest($"mstest-{Guid.NewGuid()}", "Ogooreck"), ct)))
                .Then([ExpectResponse(OK)], ct));

        Assert.Contains("200", error.Message);
        Assert.Contains("201", error.Message);
    }
}

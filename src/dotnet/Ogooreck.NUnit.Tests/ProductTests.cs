using System.Net.Http.Json;
using NUnit.Framework;
using Ogooreck.API;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.NUnit.Tests;

public class ProductTests
{
    #region NUnitTest

    [Test]
    public async Task RegisteringAProduct_RespondsWithCreated()
    {
        var ct = TestContext.CurrentContext.CancellationToken;
        await using var api = For<Program>();

        await api.Given()
            .When(Request(client => client.PostAsJsonAsync("/api/products",
                new RegisterProductRequest($"nunit-{Guid.NewGuid()}", "Ogooreck"),
                ct)))
            .Then([ExpectResponse(Created)], ct);
    }

    #endregion

    [Test]
    public async Task ExpectingTheWrongStatus_FailsWithOgooreckMessageAsAPlainException()
    {
        var ct = TestContext.CurrentContext.CancellationToken;
        await using var api = For<Program>();

        await Assert.ThatAsync(() => api.Given()
                .When(Request(client => client.PostAsJsonAsync("/api/products",
                    new RegisterProductRequest($"nunit-{Guid.NewGuid()}", "Ogooreck"), ct)))
                .Then([ExpectResponse(OK)], ct),
            Throws.TypeOf<Ogooreck.Assertions.AssertionException>()
                .And.Message.Contains("200")
                .And.Message.Contains("201"));
    }
}

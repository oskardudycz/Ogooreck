using System.Net.Http.Json;
using Ogooreck.API;
using TUnit.Assertions.Extensions;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.TUnit.Tests;

public class ProductTests
{
    [Test]
    public async Task RegisteringAProduct_RespondsWithCreated(CancellationToken ct)
    {
        await using var api = For<Program>();

        await api.Given()
            .When(Request(client => client.PostAsJsonAsync("/api/products",
                new RegisterProductRequest($"tunit-{Guid.NewGuid()}", "Ogooreck"), ct)))
            .Then([ExpectResponse(Created)], ct);
    }

    [Test]
    public async Task ExpectingTheWrongStatus_FailsWithOgooreckMessageAsAPlainException(CancellationToken ct)
    {
        await using var api = For<Program>();

        await Assert.That(() => api.Given()
                .When(Request(client => client.PostAsJsonAsync("/api/products",
                    new RegisterProductRequest($"tunit-{Guid.NewGuid()}", "Ogooreck"), ct)))
                .Then([ExpectResponse(OK)], ct))
            .Throws<Ogooreck.Assertions.AssertionException>()
            .WithMessageContaining("200")
            .And.WithMessageContaining("201");
    }
}

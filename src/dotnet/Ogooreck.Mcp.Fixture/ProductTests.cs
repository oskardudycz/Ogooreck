using System.Diagnostics;
using System.Net.Http.Json;
using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Mcp.Fixture;

public class ProductTests
{
    [Fact]
    public async Task RegisteringAProduct_RespondsWithCreated()
    {
        await using var api = For<Program>();

        await api.Given()
            .When(RegisterProduct())
            .Then([ExpectResponse(Created)], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RegisteringAProduct_RespondsWithOk()
    {
        await using var api = For<Program>();

        await api.Given()
            .When(RegisterProduct())
            .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken);
    }

    [Fact(Skip = "Shows how a skipped test is counted.")]
    public void RegisteringAProduct_WithoutAName() { }

    [Fact]
    public void WritingSpansOfItsOwn()
    {
        using var activity = OwnSpans.StartActivity("own work");
        activity?.SetTag("own.done", true).SetTag("own.retried", false).SetTag("own.ratio", 0.5)
            .SetTag("own.labels", new[] { "first", "second" }).SetStatus(ActivityStatusCode.Ok);
    }

    private static readonly ActivitySource OwnSpans = new("Ogooreck.Mcp.Fixture");

    private static TestStep RegisterProduct() =>
        Request(client => client.PostAsJsonAsync("/api/products",
            new RegisterProductRequest($"mcp-{Guid.NewGuid()}", "Ogooreck"), TestContext.Current.CancellationToken));
}

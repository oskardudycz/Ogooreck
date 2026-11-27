using System.Diagnostics;
using Ogooreck.Api.Tests.API.Telemetry.Fixtures;
using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Telemetry;

[Collection(nameof(SerialMetrics))]
public class OwnSpansTests
{
    [Fact]
    public async Task WithTelemetryOff_CatchUpStillRunsInsideOgoorecksCatchUpSpan()
    {
        string? current = null;
        await using var app = For<Program>().Telemetry(TelemetrySources.Off).CatchUp(_ =>
        {
            current = Activity.Current?.DisplayName;
            return Task.CompletedTask;
        });

        await app.Given()
            .When(GetCart(Guid.NewGuid()))
            .Then([ExpectError(NotFound)], TestContext.Current.CancellationToken);

        Assert.Equal("catch-up", current);
    }
}

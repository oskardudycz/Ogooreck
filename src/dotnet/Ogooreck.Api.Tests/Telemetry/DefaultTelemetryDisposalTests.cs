using System.Diagnostics;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.Telemetry;

[Collection(nameof(SerialMetrics))]
public class DefaultTelemetryDisposalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposing_ASpecification_StopsItsDefaultInProcessListeners(bool asynchronous)
    {
        using var source = new ActivitySource($"test-{Guid.NewGuid()}");
        var spec = For<Program>();
        await spec.Given().When(CreateCart()).Then([ExpectResponse(Created)], TestContext.Current.CancellationToken);
        using (var listening = source.StartActivity("while listening"))
            Assert.NotNull(listening);

        if (asynchronous)
            await spec.DisposeAsync();
        else
            spec.Dispose();

        using var afterwards = source.StartActivity("after dispose");
        Assert.Null(afterwards);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposing_ASpecificationBeforeItsFirstStep_DoesNotStartTheHost(bool asynchronous)
    {
        var spec = For<Program>();

        if (asynchronous)
            await spec.DisposeAsync();
        else
            spec.Dispose();
    }
}

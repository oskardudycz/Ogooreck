using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Plugins;

public class AppServicesTests
{
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task PluginReadingAppServicesBeforeTheFirstStep_FailsSayingWhereToReadThem()
    {
        await using var api = new ApiSpecification<Program>();

        var error = Assert.Throws<InvalidOperationException>(() => api.Use(new ReadingServices(registration => _ = registration.AppServices)));

        Assert.StartsWith("The host has not started yet: AppServices is available after the first step has started it.", error.Message);
    }

    [Fact]
    public async Task PluginReadingAppServicesInItsCatchUp_GetsTheRunningAppsServices()
    {
        var readByRoot = new List<IServiceProvider>();
        var readByVariant = new List<IServiceProvider>();
        await using var api = new ApiSpecification<Program>().Use(ReadingInCatchUp(readByRoot));
        await using var variant = api.With(setup => setup.Use(ReadingInCatchUp(readByVariant)));

        await api.Given().When(GetCart(Guid.NewGuid())).Then([ExpectResponse(NotFound)], ct);
        await variant.Given().When(GetCart(Guid.NewGuid())).Then([ExpectResponse(NotFound)], ct);

        Assert.Equal(2, readByRoot.Count);
        var services = Assert.Single(readByVariant);
        Assert.Same(readByRoot[0], services);
    }

    private static ReadingServices ReadingInCatchUp(List<IServiceProvider> read) =>
        new(registration => registration.CatchUp(_ => { read.Add(registration.AppServices); return Task.CompletedTask; }));

    private sealed class ReadingServices(Action<SpecificationPluginRegistration> register): ISpecificationPlugin
    {
        public void Register(SpecificationPluginRegistration registration) => register(registration);
    }
}

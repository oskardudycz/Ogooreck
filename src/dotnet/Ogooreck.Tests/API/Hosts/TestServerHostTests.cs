using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ogooreck.API;
using Ogooreck.Tests.API.Hosts.Fixtures;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests.API.Hosts;

public class TestServerHostTests
{
    private static readonly ActivitySource Shop = new("Ogooreck.Tests.TestServerShop");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static TestStep GetStatus() => Request(client => client.GetAsync("/status", Ct));

    private static Task<IHost> StartShop() =>
        new HostBuilder()
            .ConfigureWebHost(web => web.UseTestServer()
                .ConfigureServices(services => services.AddRouting()
                    .ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter())))
                .Configure(app => app.UseRouting().UseEndpoints(endpoints => endpoints.MapGet("/status", () =>
                {
                    using var _ = Shop.StartActivity("check status");
                    return Results.Ok(new { status = Status.Placed });
                }))))
            .StartAsync(Ct);

    [Fact]
    public async Task HostBuiltInTheTest_ReceivesTheRequests_WithTheAppsJsonOptions()
    {
        using var host = await StartShop();
        await using var api = For(host);

        await api.Given().When(GetStatus()).Then([ExpectResponse(OK, Body(new { status = Status.Placed }))], Ct);
    }

    [Fact]
    public async Task HostBuiltInTheTest_IsTracedInProcess()
    {
        using var host = await StartShop();
        await using var api = For(host);

        await api.Given().When(GetStatus()).Then([ExpectTrace(Span("check status"))], Ct);
    }

    [Fact]
    public async Task PluginOnAHostBuiltInTheTest_ReadsTheAppsServices()
    {
        using var host = await StartShop();
        var plugin = new ServerReader();
        await using var api = For(host).Use(plugin);

        await api.Given().When(GetStatus()).Then([ExpectResponse(OK)], Ct);

        Assert.IsType<TestServer>(plugin.Server);
    }

    [Fact]
    public async Task DisposingTheSpecification_LeavesTheHostRunning()
    {
        using var host = await StartShop();
        await using (var api = For(host))
            await api.Given().When(GetStatus()).Then([ExpectResponse(OK)], Ct);

        using var client = host.GetTestClient();
        Assert.Equal(OK, (await client.GetAsync("/status", Ct)).StatusCode);
    }

    [Fact]
    public void HostWithoutATestServer_Throws_NamingUseTestServerAndTheAddressAlternative()
    {
        using var host = new HostBuilder().Build();

        var error = Assert.Throws<ArgumentException>(() => For(host));

        Assert.StartsWith(
            "The host has no test server. Call UseTestServer() on its web host builder, or use ApiSpecification.For(async () => address) for an app that listens on a port.",
            error.Message);
    }

    [Fact]
    public async Task HostCallAfterFor_ReplacesTheTestHost()
    {
        using var host = await StartShop();
        await using var echo = EchoServer.Start();
        await using var api = For(host).Host(() => Task.FromResult(echo.Address));

        await api.Given().When(Request(client => client.GetAsync("/echo", Ct))).Then([ExpectResponse(OK)], Ct);
    }

    private enum Status
    {
        Placed
    }

    private sealed class ServerReader: ISpecificationPlugin
    {
        private SpecificationPluginRegistration? registration;

        public IServer? Server { get; private set; }

        public void Register(SpecificationPluginRegistration pluginRegistration)
        {
            registration = pluginRegistration;
            pluginRegistration.Starting(_ =>
            {
                Server = registration.AppServices.GetService<IServer>();
                return Task.CompletedTask;
            });
        }
    }
}

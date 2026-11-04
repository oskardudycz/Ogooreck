using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ogooreck.API;
using Xunit;
using TestContext = Ogooreck.API.TestContext;

namespace Ogooreck.Tests;

public class ApiJsonSettingsTests
{
    [Fact]
    public void HostIndependentResolverAcceptsGenericHostServices()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<State>());
        });
        using var host = builder.Build();
        var settings = ApiJsonSettings.FromServices(host.Services);
        var configured = host.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;

        Assert.Same(configured, settings.Request);
        Assert.Same(configured, settings.Response);
        var json = JsonSerializer.Serialize(new Message("test", State.Ready), settings.Request);
        Assert.Equal("""{"display_name":"test","state":"Ready"}""", json);
        Assert.Equal(new Message("test", State.Ready), JsonSerializer.Deserialize<Message>(json, settings.Response));
    }

    [Fact]
    public void MvcRegistrationWithoutJsonFormattersFallsBackToHttpSettings()
    {
        var services = new ServiceCollection();
        services.AddOptions<MvcOptions>();
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
        using var provider = services.BuildServiceProvider();

        var settings = ApiJsonSettings.FromServices(provider);
        Assert.Same(provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions,
            settings.Request);
        Assert.Same(settings.Request, settings.Response);
    }

    [Fact]
    public void EmptyServicesAndStandaloneContextUseWebDefaults()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var settings = ApiJsonSettings.FromServices(provider);
        Assert.Same(JsonSerializerOptions.Web, settings.Request);
        Assert.Same(JsonSerializerOptions.Web, settings.Response);
        Assert.Same(settings, new TestContext().JsonSettings);
    }

    [Fact]
    public async Task ContextResolvesSettingsLazilyOnceAndDoesNotShareState()
    {
        var firstCalls = 0;
        var secondCalls = 0;
        var firstSettings = new ApiJsonSettings(new JsonSerializerOptions(), new JsonSerializerOptions());
        var secondSettings = new ApiJsonSettings(new JsonSerializerOptions(), new JsonSerializerOptions());
        var first = new TestContext(() =>
        {
            Interlocked.Increment(ref firstCalls);
            return firstSettings;
        });
        var second = new TestContext(() =>
        {
            Interlocked.Increment(ref secondCalls);
            return secondSettings;
        });
        Assert.Equal(0, firstCalls);
        Assert.Equal(0, secondCalls);

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            Assert.Same(firstSettings, first.JsonSettings);
            Assert.Same(secondSettings, second.JsonSettings);
        }, Xunit.TestContext.Current.CancellationToken)));
        Assert.Equal(1, firstCalls);
        Assert.Equal(1, secondCalls);
    }

    public enum State { Ready }
    public record Message(string DisplayName, State State);
}

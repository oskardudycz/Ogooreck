using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ogooreck.API;
using Xunit;

namespace Ogooreck.Tests.API.Responses;

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
    public void EmptyServicesUseWebDefaults()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var settings = ApiJsonSettings.FromServices(provider);
        Assert.Same(JsonSerializerOptions.Web, settings.Request);
        Assert.Same(JsonSerializerOptions.Web, settings.Response);
    }

    public enum State { Ready }
    public record Message(string DisplayName, State State);
}

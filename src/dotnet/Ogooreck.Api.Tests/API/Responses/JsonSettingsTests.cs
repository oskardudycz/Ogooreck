using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Responses;

public class JsonSettingsTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    internal static readonly Payload Example = new("A product", new Token("123"), Status.Ready);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InheritsHostSettingsThroughoutTheScenario(bool mvc)
    {
        WebApplicationFactory<Program>? factory = null;
        using var api = For<Program>(f => factory = Factory(f, mvc));
        var route = mvc ? "/json/mvc" : "/json/minimal";
        var prefix = mvc ? "mvc" : "http";
        var sendExample = Request(client => client.PostAsJsonAsync(route, Example, Options(prefix), TestContext.Current.CancellationToken));

        await api.Given(sendExample)
            .When(sendExample)
            .Then([ExpectResponse(OK, Body(Example), Satisfying(async response =>
            {
                var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                Assert.Contains("\"display_name\":", json);
                Assert.Contains($"{prefix}:123", json);
                Assert.Contains("\"ready\"", json);
            }))], TestContext.Current.CancellationToken);

        var requestBodies = factory!.Services.GetRequiredService<RequestBodies>().Bodies;
        Assert.Equal(2, requestBodies.Count);
        foreach (var json in requestBodies)
        {
            Assert.Contains("\"display_name\":", json);
            Assert.Contains($"{prefix}:123", json);
            Assert.Contains("\"ready\"", json);
        }
    }

    [Fact]
    public async Task UsesActualMvcInputAndOutputFormatterOptions()
    {
        WebApplicationFactory<Program>? factory = null;
        using var api = For<Program>(f => factory = Factory(f, mvc: true, outputPrefix: "output"));

        await api.Given()
            .When(Request(client => client.PostAsJsonAsync("/json/mvc", Example, Options("mvc"), TestContext.Current.CancellationToken)))
            .Then([ExpectResponse(OK, Body(Example), Satisfying(async response =>
                Assert.Contains("output:123", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))))], TestContext.Current.CancellationToken);

        Assert.Contains("mvc:123", Assert.Single(factory!.Services.GetRequiredService<RequestBodies>().Bodies));
    }

    [Fact]
    public async Task MixedHostDefaultsToMvcSettings()
    {
        WebApplicationFactory<Program>? factory = null;
        using var api = For<Program>(f => factory = Factory(f, mvc: true));

        var sendToMvc = Request(client => client.PostAsJsonAsync("/json/mvc", Example, Options("mvc"), TestContext.Current.CancellationToken));

        await api.Given().When(sendToMvc).Then([ExpectResponse(OK, Body(Example))], TestContext.Current.CancellationToken);
        var mvcOptions = factory!.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        var httpOptions = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        var mvcConverters = mvcOptions.Converters.ToArray();
        var httpConverters = httpOptions.Converters.ToArray();
        await api.Given().When(sendToMvc).Then([ExpectResponse(OK, Body(Example))], TestContext.Current.CancellationToken);

        Assert.Equal(mvcConverters, mvcOptions.Converters);
        Assert.Equal(httpConverters, httpOptions.Converters);
        Assert.Equal(JsonNamingPolicy.SnakeCaseLower, mvcOptions.PropertyNamingPolicy);
        Assert.Equal(JsonNamingPolicy.SnakeCaseLower, httpOptions.PropertyNamingPolicy);
    }

    [Fact]
    public async Task ConcurrentHostsAndScenariosDoNotShareSettings()
    {
        using var first = For<Program>(f => Factory(f, mvc: false, httpPrefix: "first"));
        using var second = For<Program>(f => Factory(f, mvc: false, httpPrefix: "second"));

        await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
        {
            var (api, prefix) = i % 2 == 0 ? (first, "first") : (second, "second");
            return api.Given()
                .When(Request(client => client.PostAsJsonAsync("/json/minimal", Example, Options(prefix), TestContext.Current.CancellationToken)))
                .Then([ExpectResponse(OK, Body(Example), Satisfying(async response =>
                    Assert.Contains($"{prefix}:123", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))))], TestContext.Current.CancellationToken);
        }));
    }

    [Fact]
    public async Task Json_WithSnakeCase_MatchesASnakeCaseResponse_AndWithoutItFails()
    {
        var cartId = Guid.NewGuid();
        var placedOrder = new[]
        {
            Request(client => client.PostAsJsonAsync($"/carts/{cartId}/products", new { product = "socks" }, TestContext.Current.CancellationToken)),
            Request(client => client.PostAsync($"/carts/{cartId}/orders", null, TestContext.Current.CancellationToken))
        };
        var getExport = Request(client => client.GetAsync($"/orders/{cartId}/export", TestContext.Current.CancellationToken));

        await using var snakeCase = api.With(setup => setup.Json(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        await snakeCase.Given(placedOrder).When(getExport)
            .Then([ExpectResponse(OK, Body(new { OrderStatus = "Placed" }))], TestContext.Current.CancellationToken);

        var webDefaults = api;
        var error = await Assert.ThrowsAsync<Ogooreck.Assertions.AssertionException>(() =>
            webDefaults.Given(placedOrder).When(getExport)
                .Then([ExpectResponse(OK, Body(new { OrderStatus = "Placed" }))], TestContext.Current.CancellationToken));
        Assert.Contains("differs from the expected {\"orderStatus\":\"Placed\"} at: $[orderStatus]: expected <Placed>, but found <no such field>", error.Message);
    }

    private static WebApplicationFactory<Program> Factory(WebApplicationFactory<Program> factory, bool mvc,
        string httpPrefix = "http", string? outputPrefix = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<RequestBodies>();
                services.ConfigureHttpJsonOptions(options => Configure(options.SerializerOptions, httpPrefix));
                if (!mvc)
                    return;

                services.AddControllers().AddApplicationPart(typeof(JsonEchoController).Assembly)
                    .AddJsonOptions(options => Configure(options.JsonSerializerOptions, "mvc"));
                if (outputPrefix is not null)
                    services.PostConfigure<MvcOptions>(options =>
                    {
                        var formatter = options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>().Single();
                        var index = options.OutputFormatters.IndexOf(formatter);
                        var outputOptions = new JsonSerializerOptions(formatter.SerializerOptions);
                        outputOptions.Converters.Clear();
                        Configure(outputOptions, outputPrefix);
                        options.OutputFormatters[index] = new SystemTextJsonOutputFormatter(outputOptions);
                    });
            });
            builder.Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    if (HttpMethods.IsPost(context.Request.Method))
                    {
                        context.Request.EnableBuffering();
                        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                        var json = await reader.ReadToEndAsync(context.RequestAborted);
                        context.RequestServices.GetRequiredService<RequestBodies>().Bodies.Enqueue(json);
                        context.Request.Body.Position = 0;
                    }
                    await next(context);
                });
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    if (mvc)
                        endpoints.MapControllers();
                    endpoints.MapPost("/json/minimal", (Payload body) => body);
                });
            });
        });

    private static JsonSerializerOptions Options(string prefix)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options, prefix);
        return options;
    }

    private static void Configure(JsonSerializerOptions options, string prefix)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.Converters.Add(new TokenConverter(prefix));
        options.Converters.Add(new JsonStringEnumConverter<Status>(JsonNamingPolicy.CamelCase));
    }

    public record Token(string Value);
    public enum Status { Ready }
    public record Payload(string DisplayName, Token Token, Status Status);

    private sealed class RequestBodies
    {
        public ConcurrentQueue<string> Bodies { get; } = new();
    }

    private sealed class TokenConverter(string prefix): JsonConverter<Token>
    {
        public override Token Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (value is null || !value.StartsWith(prefix + ":", StringComparison.Ordinal))
                throw new JsonException($"Expected the {prefix} token format.");
            return new Token(value[(prefix.Length + 1)..]);
        }

        public override void Write(Utf8JsonWriter writer, Token value, JsonSerializerOptions options) =>
            writer.WriteStringValue($"{prefix}:{value.Value}");
    }
}

[ApiController]
[Route("json/mvc")]
public class JsonEchoController: ControllerBase
{
    [HttpPost]
    public JsonSettingsTests.Payload Echo(JsonSettingsTests.Payload body) => body;
}

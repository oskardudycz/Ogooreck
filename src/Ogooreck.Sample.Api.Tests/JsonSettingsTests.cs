using System.Collections.Concurrent;
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
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Sample.Api.Tests;

public class JsonSettingsTests
{
    internal static readonly Payload Example = new("A product", new Token("123"), Status.Ready);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InheritsHostSettingsThroughoutTheScenario(bool mvc)
    {
        var factory = Factory(mvc);
        using var api = ApiSpecification<Program>.Setup(factory);
        var route = mvc ? "/json/mvc" : "/json/minimal";
        var checks = 0;
        var scenario = api.Given(SEND(POST, URI(route), BODY(Example)))
            .When(POST, BODY(Example), URI(route))
            .Until(RESPONSE_BODY_MATCHES<Payload>(body =>
            {
                checks++;
                return body == Example;
            }), maxNumberOfRetries: 0, retryIntervalInMs: 0)
            .Then(OK, RESPONSE_BODY(Example),
                RESPONSE_BODY<Payload>(body => Assert.Equal(Example, body)),
                RESPONSE_BODY<Payload>((body, context) =>
                {
                    Assert.Equal(Example, body);
                    Assert.Equal(2, context.Calls.Count);
                }),
                RESPONSE_BODY<Payload>(_ => Example));

        var result = await scenario;
        Assert.Equal(1, checks);
        Assert.Equal(Example, await result.GetResultFromJson<Payload>());
        Assert.Equal(Example, await scenario.GetResponseBody<Payload>());
        Assert.Equal(Example, await scenario.GetResponseBody<Payload>());

        var requestBodies = factory.Services.GetRequiredService<RequestBodies>().Bodies;
        Assert.Equal(2, requestBodies.Count);
        foreach (var json in requestBodies)
        {
            Assert.Contains("\"display_name\":", json);
            Assert.Contains(mvc ? "mvc:123" : "http:123", json);
            Assert.Contains("\"ready\"", json);
        }

        var chained = await scenario.AndWhen(POST, URI(route), BODY(Example))
            .Then(OK, RESPONSE_BODY(Example));
        Assert.Same(result.TestContext, chained.TestContext);
        Assert.Equal(3, chained.TestContext.Calls.Count);
        Assert.Equal(Example, await chained.GetResultFromJson<Payload>());
    }

    [Fact]
    public async Task UsesActualMvcInputAndOutputFormatterOptions()
    {
        var factory = Factory(mvc: true, outputPrefix: "output");
        using var api = ApiSpecification<Program>.Setup(factory);
        var result = await api.Given().When(POST, URI("/json/mvc"), BODY(Example))
            .Then(OK, RESPONSE_BODY(Example));

        Assert.Contains("mvc:123", Assert.Single(factory.Services.GetRequiredService<RequestBodies>().Bodies));
        Assert.Contains("output:123", await result.Response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken));
        Assert.Equal(Example, await result.GetResultFromJson<Payload>());
    }

    [Fact]
    public async Task MixedHostDefaultsToMvcAndAllowsMinimalApiOverrides()
    {
        using var factory = Factory(mvc: true);
        using var api = ApiSpecification<Program>.Setup(factory);
        var http = Options("http");
        var mvcOptions = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        var httpOptions = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
        var mvcConverters = mvcOptions.Converters.ToArray();
        var httpConverters = httpOptions.Converters.ToArray();

        await api.Given().When(POST, URI("/json/mvc"), BODY(Example))
            .Then(OK, RESPONSE_BODY(Example));
        var minimal = api.Given().When(POST, URI("/json/minimal"), BODY(Example, http))
            .Until(RESPONSE_BODY_MATCHES<Payload>(body => body == Example, http), 0, 0)
            .Then(OK, RESPONSE_BODY(Example, http));

        Assert.Equal(Example, await minimal.GetResponseBody<Payload>(http));
        Assert.Equal(Example, await (await minimal).GetResultFromJson<Payload>(http));
        await minimal.AndWhen(POST, URI("/json/mvc"), BODY(Example))
            .Then(OK, RESPONSE_BODY(Example));
        Assert.Equal(mvcConverters, mvcOptions.Converters);
        Assert.Equal(httpConverters, httpOptions.Converters);
        Assert.Equal(JsonNamingPolicy.SnakeCaseLower, mvcOptions.PropertyNamingPolicy);
        Assert.Equal(JsonNamingPolicy.SnakeCaseLower, httpOptions.PropertyNamingPolicy);
    }

    [Fact]
    public async Task ResponseOverridesApplyToEveryAssertionAndExtractionWithoutLeaking()
    {
        using var api = ApiSpecification<Program>.Setup(Factory(mvc: false));
        var options = Options("endpoint");
        var callbacks = 0;
        var scenario = api.Given().When(GET, URI("/json/override"))
            .Until(RESPONSE_BODY_MATCHES<Payload>(body => body == Example, options), 0, 0)
            .Then(OK, RESPONSE_BODY(Example, options),
                RESPONSE_BODY<Payload>(_ => Example, options),
                RESPONSE_BODY<Payload>(body =>
                {
                    Assert.Equal(Example, body);
                    callbacks++;
                }, options),
                RESPONSE_BODY<Payload>((body, context) =>
                {
                    Assert.Equal(Example, body);
                    Assert.Single(context.Calls);
                    callbacks++;
                }, options));

        Assert.Equal(Example, await scenario.GetResponseBody<Payload>(options));
        Assert.Equal(Example, await (await scenario).GetResultFromJson<Payload>(options));
        Assert.Equal(2, callbacks);
        await Assert.ThrowsAsync<JsonException>(() => scenario.GetResponseBody<Payload>());
        await scenario.AndWhen(POST, URI("/json/minimal"), BODY(Example))
            .Then(OK, RESPONSE_BODY(Example));
    }

    [Fact]
    public async Task FailedExpectationsAndPredicatesStillPropagateWithHostSettings()
    {
        using var api = ApiSpecification<Program>.Setup(Factory(mvc: false));
        var result = await api.Given().When(GET, URI("/json/minimal")).Then(OK);
        await Assert.ThrowsAsync<Ogooreck.Assertions.AssertionException>(() =>
            RESPONSE_BODY(Example with { DisplayName = "wrong" })(result.Response, result.TestContext).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RESPONSE_BODY<Payload>((Action<Payload>)(_ => throw new InvalidOperationException("callback")))
                (result.Response, result.TestContext).AsTask());
        Assert.False(await RESPONSE_BODY_MATCHES<Payload>(_ => false)(result.Response, result.TestContext));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RESPONSE_BODY_MATCHES<Payload>(_ => throw new InvalidOperationException("predicate"))
                (result.Response, result.TestContext).AsTask());
        Assert.Equal(Example, await result.GetResultFromJson<Payload>());
    }

    [Fact]
    public async Task ConcurrentHostsAndScenariosDoNotShareSettings()
    {
        using var first = ApiSpecification<Program>.Setup(Factory(mvc: false, httpPrefix: "first"));
        using var second = ApiSpecification<Program>.Setup(Factory(mvc: false, httpPrefix: "second"));

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            var api = i % 2 == 0 ? first : second;
            var result = await api.Given().When(POST, URI("/json/minimal"), BODY(Example))
                .Then(OK, RESPONSE_BODY(Example));
            Assert.Equal(Example, await result.GetResultFromJson<Payload>());
            var json = await result.Response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken);
            Assert.Contains(i % 2 == 0 ? "first:123" : "second:123", json);
            return result.TestContext;
        }));

        Assert.Equal(12, results.Distinct().Count());
    }

    [Fact]
    public async Task DefaultsWorkWithoutAnyExplicitJsonConfiguration()
    {
        using var api = ApiSpecification<Program>.Setup(new WebApplicationFactory<Program>());
        var id = await api.Given().When(POST, URI("/api/products"), BODY(new RegisterProductRequest("json-default", "Default")))
            .Then(CREATED).GetResponseBody<Guid>();
        await api.Given().When(GET, URI($"/api/products/{id}"))
            .Then(OK, RESPONSE_BODY(new Product(id, "json-default", "Default")));
    }

    private static WebApplicationFactory<Program> Factory(bool mvc, string httpPrefix = "http", string? outputPrefix = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<RequestBodies>();
                services.ConfigureHttpJsonOptions(options => Configure(options.SerializerOptions, httpPrefix));
                if (!mvc) return;

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
                    if (mvc) endpoints.MapControllers();
                    endpoints.MapPost("/json/minimal", (Payload body) => body);
                    endpoints.MapGet("/json/minimal", () => Example);
                    endpoints.MapGet("/json/override", () => Results.Json(Example, Options("endpoint")));
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

    private sealed class TokenConverter(string prefix) : JsonConverter<Token>
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
public class JsonEchoController : ControllerBase
{
    [HttpPost]
    public JsonSettingsTests.Payload Echo(JsonSettingsTests.Payload body) => body;
}

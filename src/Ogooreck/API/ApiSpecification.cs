using Microsoft.AspNetCore.Mvc.Testing;

#pragma warning disable CS1591

namespace Ogooreck.API;

public delegate HttpRequestMessage RequestTransform(HttpRequestMessage request, TestContext context);

public delegate ValueTask ResponseAssert(HttpResponseMessage response, TestContext context);

public class ApiSpecification<TProgram>: IDisposable where TProgram : class
{
    private readonly WebApplicationFactory<TProgram> applicationFactory;
    private readonly Func<HttpClient> createClient;

    public ApiSpecification(): this(new WebApplicationFactory<TProgram>())
    {
    }

    protected ApiSpecification(WebApplicationFactory<TProgram> applicationFactory)
    {
        this.applicationFactory = applicationFactory;
        createClient = applicationFactory.CreateClient;
    }

    public static ApiSpecification<TProgram> Setup(WebApplicationFactory<TProgram> applicationFactory) =>
        new(applicationFactory);

    public GivenApiSpecificationBuilder Given(
        params RequestDefinition[] builders
    ) =>
        Given(builders.Select(b => new ApiTestStep(TestPhase.Given, b)).ToArray());


    public GivenApiSpecificationBuilder Given(
        string description,
        params RequestDefinition[] builders
    ) =>
        Given(builders.Select(b => new ApiTestStep(TestPhase.Given, b, description)).ToArray());

    public GivenApiSpecificationBuilder Given(
        ApiTestStep[] builders) =>
        new(new TestContext(), createClient, builders);

    public async Task<ApiSpecification.Result> Scenario(
        Task<ApiSpecification.Result> first,
        params Func<HttpResponseMessage, Task<ApiSpecification.Result>>[] following)
    {
        var result = await first;

        foreach (var next in following)
        {
            result = await next(result.Response);
        }

        return result;
    }

    public async Task<HttpResponseMessage> Scenario(
        Task<HttpResponseMessage> first,
        params Task<HttpResponseMessage>[] following)
    {
        var response = await first;

        foreach (var next in following)
        {
            response = await next;
        }

        return response;
    }

    public void Dispose() =>
        applicationFactory.Dispose();
}

public class TestApiRequest
{
    private readonly RequestTransform[] builders;
    private readonly TestContext testContext;

    public TestApiRequest(TestContext testContext, params RequestTransform[] builders)
    {
        this.testContext = testContext;
        this.builders = builders;
    }

    public HttpRequestMessage Build() =>
        builders.Aggregate(new HttpRequestMessage(), (request, build) => build(request, testContext));

    public static HttpRequestMessage For(TestContext testContext, RequestDefinition requestDefinition) =>
        requestDefinition.Transformations
            .Aggregate(new HttpRequestMessage(), (request, build) => build(request, testContext));
}

public static class ApiRequestExtensions
{
    public static Task<HttpResponseMessage> Send(
        this HttpClient httpClient,
        TestApiRequest testApiRequest,
        CancellationToken ct = default
    ) =>
        httpClient.SendAsync(testApiRequest.Build(), ct);
}

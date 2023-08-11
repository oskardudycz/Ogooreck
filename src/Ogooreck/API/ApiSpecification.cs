using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

#pragma warning disable CS1591

namespace Ogooreck.API;

public record MadeApiCall(HttpRequestMessage Request, HttpResponseMessage Response);

public class TestContext
{
    public List<MadeApiCall> Calls { get; } = new();

    public void Record(HttpRequestMessage request, HttpResponseMessage response) =>
        Calls.Add(new MadeApiCall(request, response));

    public T GetCreatedId<T>() where T : notnull =>
        Calls.First().Response.GetCreatedId<T>();
}

public record RequestDefinition(RequestTransform[] Transformations);

public delegate HttpRequestMessage RequestTransform(HttpRequestMessage request, TestContext context);

public delegate ValueTask ResponseAssert(HttpResponseMessage response, TestContext context);

public static class ApiSpecification
{
    ///////////////////
    ////   GIVEN   ////
    ///////////////////

    ///////////////////
    ////   WHEN    ////
    ///////////////////

    public static RequestTransform[] SEND(params RequestTransform[] when) => when;

    public static RequestTransform URI(Func<TestContext, string> getUrl) =>
        URI(ctx => new Uri(getUrl(ctx), UriKind.RelativeOrAbsolute));

    public static RequestTransform URI(string uri) =>
        URI(new Uri(uri, UriKind.RelativeOrAbsolute));

    public static RequestTransform URI(Uri uri) =>
        URI(_ => uri);

    public static RequestTransform URI(Func<TestContext, Uri> getUri) =>
        (request, ctx) =>
        {
            request.RequestUri = getUri(ctx);
            return request;
        };

    public static RequestTransform BODY<T>(T body) =>
        (request, _) =>
        {
            request.Content = JsonContent.Create(body);

            return request;
        };

    public static RequestTransform HEADERS(params Action<HttpRequestHeaders>[] headers) =>
        (request, _) =>
        {
            foreach (var header in headers)
            {
                header(request.Headers);
            }

            return request;
        };


    public static RequestTransform GET =>
        (request, _) =>
        {
            request.Method = HttpMethod.Get;
            return request;
        };

    public static RequestTransform POST => SEND(HttpMethod.Post);

    public static RequestTransform PUT => SEND(HttpMethod.Put);

    public static RequestTransform DELETE => SEND(HttpMethod.Delete);

    public static RequestTransform OPTIONS => SEND(HttpMethod.Options);

    public static RequestTransform SEND(HttpMethod method) =>
        (request, _) =>
        {
            request.Method = method;
            return request;
        };

    public static Action<HttpRequestHeaders> IF_MATCH(string ifMatch, bool isWeak = true) =>
        headers => headers.IfMatch.Add(new EntityTagHeaderValue($"\"{ifMatch}\"", isWeak));

    public static Action<HttpRequestHeaders> IF_MATCH(object ifMatch, bool isWeak = true) =>
        IF_MATCH(ifMatch.ToString()!, isWeak);

    public static Task<HttpResponseMessage> And(this Task<Result> result,
        Func<HttpResponseMessage, HttpResponseMessage> and) =>
        result.ContinueWith(t => and(t.Result.Response));

    public static Task And(this Task<Result> result, Func<HttpResponseMessage, Task> and) =>
        result.ContinueWith(t => and(t.Result.Response));

    public static Task And<TResult>(this Task<Result> result,
        Func<HttpResponseMessage, Task<TResult>> and) =>
        result.ContinueWith(t => and(t.Result.Response));

    public static Task And(this Task<Result> result, Func<Task> and) =>
        result.ContinueWith(_ => and());

    public static Task And(this Task<Result> result, Task and) =>
        result.ContinueWith(_ => and);

    public static Task<GivenApiSpecificationBuilder> And(this Task<Result> result) =>
        result.ContinueWith(_ => new GivenApiSpecificationBuilder(result.Result.TestContext, result.Result.CreateClient));

    public static Task<WhenApiSpecificationBuilder> AndWhen(this Task<Result> result, params RequestTransform[] when) =>
        result.And().When(when);

    public static Task<WhenApiSpecificationBuilder> AndWhen(
        this Task<Result> result,
        Func<HttpResponseMessage, RequestTransform[]> when
    ) =>
        result.ContinueWith(r =>
            new GivenApiSpecificationBuilder(r.Result.TestContext, r.Result.CreateClient).When(when(r.Result.Response))
        );

    public static Task<WhenApiSpecificationBuilder> When(
        this Task<GivenApiSpecificationBuilder> result,
        params RequestTransform[] when
    ) =>
        result.ContinueWith(_ => result.Result.When(when));

    public static Task<WhenApiSpecificationBuilder> Until(
        this Task<WhenApiSpecificationBuilder> when,
        Func<HttpResponseMessage, TestContext, ValueTask<bool>> check,
        int maxNumberOfRetries = 5,
        int retryIntervalInMs = 1000
    ) =>
        when.ContinueWith(t => t.Result.Until(check, maxNumberOfRetries, retryIntervalInMs));

    public static Task<Result> Then(
        this Task<WhenApiSpecificationBuilder> when,
        ResponseAssert then
    ) =>
        when.ContinueWith(t => t.Result.Then(then)).Unwrap();

    public static Task<Result> Then(
        this Task<WhenApiSpecificationBuilder> when,
        params ResponseAssert[] thens
    ) =>
        when.ContinueWith(t => t.Result.Then(thens)).Unwrap();

    public static Task<Result> Then(
        this Task<WhenApiSpecificationBuilder> when,
        IEnumerable<ResponseAssert> thens,
        CancellationToken ct
    ) =>
        when.ContinueWith(t => t.Result.Then(thens, ct), ct).Unwrap();


    public static Task<T> GetResponseBody<T>(this Task<Result> result) => result.Map(RESPONSE_BODY<T>());
    public static Task<T> GetCreatedId<T>(this Task<Result> result) => result.Map(CREATED_ID<T>());

    public static Task<T> Map<T>(
        this Task<Result> result,
        Func<HttpResponseMessage, Task<T>> map
    ) =>
        result.ContinueWith(t => map(t.Result.Response)).Unwrap();

    ///////////////////
    ////   THEN    ////
    ///////////////////
    public static ResponseAssert OK = HTTP_STATUS(HttpStatusCode.OK);
    public static ResponseAssert CREATED = HTTP_STATUS(HttpStatusCode.Created);
    public static ResponseAssert NO_CONTENT = HTTP_STATUS(HttpStatusCode.NoContent);
    public static ResponseAssert BAD_REQUEST = HTTP_STATUS(HttpStatusCode.BadRequest);
    public static ResponseAssert NOT_FOUND = HTTP_STATUS(HttpStatusCode.NotFound);
    public static ResponseAssert CONFLICT = HTTP_STATUS(HttpStatusCode.Conflict);

    public static ResponseAssert PRECONDITION_FAILED =
        HTTP_STATUS(HttpStatusCode.PreconditionFailed);

    public static ResponseAssert METHOD_NOT_ALLOWED =
        HTTP_STATUS(HttpStatusCode.MethodNotAllowed);

    public static ResponseAssert HTTP_STATUS(HttpStatusCode status) =>
        (response, ctx) =>
        {
            response.StatusCode.Should().Be(status);
            return ValueTask.CompletedTask;
        };

    public static ResponseAssert CREATED_WITH_DEFAULT_HEADERS(
        string? locationHeaderPrefix = null, object? eTag = null, bool isETagWeak = true) =>
        async (response, ctx) =>
        {
            await CREATED(response, ctx);
            await RESPONSE_LOCATION_HEADER(locationHeaderPrefix)(response, ctx);
            if (eTag != null)
                await RESPONSE_ETAG_HEADER(eTag, isETagWeak)(response, ctx);
        };


    public static ResponseAssert RESPONSE_BODY<T>(T body) =>
        RESPONSE_BODY<T>(result => result.Should().BeEquivalentTo(body));

    public static ResponseAssert RESPONSE_BODY<T>(Func<TestContext, T> getBody) =>
        RESPONSE_BODY<T>((result, ctx) => result.Should().BeEquivalentTo(getBody(ctx)));

    public static ResponseAssert RESPONSE_BODY<T>(Action<T> assert) =>
        RESPONSE_BODY<T>((body, _) => assert(body));

    public static ResponseAssert RESPONSE_BODY<T>(Action<T, TestContext> assert) =>
        async (response, ctx) =>
        {
            var result = await response.GetResultFromJson<T>();
            assert(result, ctx);

            result.Should().BeEquivalentTo(result);
        };

    public static Func<HttpResponseMessage, Task<T>> RESPONSE_BODY<T>() =>
        response => response.GetResultFromJson<T>();

    public static Func<HttpResponseMessage, Task<T>> CREATED_ID<T>() =>
        response => Task.FromResult(response.GetCreatedId<T>());

    public static Func<HttpResponseMessage, TestContext, ValueTask<bool>> RESPONSE_ETAG_IS(object eTag,
        bool isWeak = true) =>
        async (response, ctx) =>
        {
            await RESPONSE_ETAG_HEADER(eTag, isWeak)(response, ctx);
            return true;
        };

    public static ResponseAssert RESPONSE_ETAG_HEADER(object eTag, bool isWeak = true) =>
        RESPONSE_HEADERS(headers =>
        {
            headers.ETag.Should().NotBeNull("ETag response header should be defined").And
                .NotBe("", "ETag response header should not be empty");
            headers.ETag!.Tag.Should().NotBeEmpty("ETag response header should not be empty");

            headers.ETag.IsWeak.Should().Be(isWeak, "Etag response header should be {0}", isWeak ? "Weak" : "Strong");
            headers.ETag.Tag.Should().Be($"\"{eTag}\"");
        });

    public static ResponseAssert RESPONSE_LOCATION_HEADER(string? locationHeaderPrefix = null) =>
        async (response, ctx) =>
        {
            await HTTP_STATUS(HttpStatusCode.Created)(response, ctx);

            var locationHeader = response.Headers.Location;

            locationHeader.Should().NotBeNull();

            var location = locationHeader!.ToString();

            location.Should().StartWith(locationHeaderPrefix ?? response.RequestMessage!.RequestUri!.AbsolutePath);
        };

    public static ResponseAssert RESPONSE_HEADERS(params Action<HttpResponseHeaders>[] headers) =>
        (response, ctx) =>
        {
            foreach (var header in headers)
            {
                header(response.Headers);
            }

            return ValueTask.CompletedTask;
        };

    public static Func<HttpResponseMessage, ValueTask<bool>> RESPONSE_SUCCEEDED() =>
        response =>
        {
            response.EnsureSuccessStatusCode();
            return new ValueTask<bool>(true);
        };

    public static Func<HttpResponseMessage, ValueTask<bool>> RESPONSE_BODY_MATCHES<TBody>(Func<TBody, bool> assert) =>
        async response =>
        {
            response.EnsureSuccessStatusCode();

            var result = await response.GetResultFromJson<TBody>();
            result.Should().NotBeNull();

            return assert(result);
        };

    public record Result(HttpResponseMessage Response, TestContext TestContext, Func<HttpClient> CreateClient);
}

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
        params RequestTransform[][] builders) =>
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

    /////////////////////
    ////   BUILDER   ////
    /////////////////////


    public void Dispose() =>
        applicationFactory.Dispose();
}

public class GivenApiSpecificationBuilder
{
    private readonly RequestTransform[][] given;
    private readonly Func<HttpClient> createClient;
    private readonly TestContext testContext;

    internal GivenApiSpecificationBuilder
    (
        TestContext testContext,
        Func<HttpClient> createClient,
        RequestTransform[][] given
    )
    {
        this.testContext = testContext;
        this.createClient = createClient;
        this.given = given;
    }

    internal GivenApiSpecificationBuilder
    (
        TestContext testContext,
        Func<HttpClient> createClient
    ): this(testContext, createClient, Array.Empty<RequestTransform[]>())
    {
    }

    public WhenApiSpecificationBuilder When(params RequestTransform[] when) =>
        new(createClient, testContext, given, when);
}

public class WhenApiSpecificationBuilder
{
    private readonly RequestTransform[][] given;
    private readonly RequestTransform[] when;
    private readonly Func<HttpClient> createClient;
    private readonly TestContext testContext;
    private RetryPolicy retryPolicy;

    internal WhenApiSpecificationBuilder(
        Func<HttpClient> createClient,
        TestContext testContext,
        RequestTransform[][] given,
        RequestTransform[] when
    )
    {
        this.createClient = createClient;
        this.testContext = testContext;
        this.given = given;
        this.when = when;
        retryPolicy = RetryPolicy.NoRetry;
    }

    public WhenApiSpecificationBuilder Until(
        Func<HttpResponseMessage, TestContext, ValueTask<bool>> check,
        int maxNumberOfRetries = 5,
        int retryIntervalInMs = 1000
    )
    {
        retryPolicy = new RetryPolicy(check, maxNumberOfRetries, retryIntervalInMs);
        return this;
    }

    public Task<ApiSpecification.Result> Then(ResponseAssert then) =>
        Then(new[] { then });

    public Task<ApiSpecification.Result> Then(params ResponseAssert[] thens) =>
        Then(thens, default);

    public async Task<ApiSpecification.Result> Then(IEnumerable<ResponseAssert> thens,
        CancellationToken ct)
    {
        using var client = createClient();

        // Given
        foreach (var givenBuilder in given)
            await Send(client, givenBuilder, testContext, ct);

        // When
        var response = await retryPolicy
            .Perform(t =>
                    Send(client, when, testContext, ct), testContext, ct
            );

        // Then
        foreach (var then in thens)
        {
            await then(response, testContext);
        }

        return new ApiSpecification.Result(response, testContext, createClient);
    }

    private static async Task<HttpResponseMessage> Send(
        HttpClient client,
        RequestTransform[] givenBuilder,
        TestContext testContext,
        CancellationToken ct
    )
    {
        var request = TestApiRequest.For(testContext, givenBuilder);
        var response = await client.SendAsync(request, ct);
        testContext.Record(request, response);

        return response;
    }
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

    public static HttpRequestMessage For(TestContext testContext, params RequestTransform[] builders) =>
        builders.Aggregate(new HttpRequestMessage(), (request, build) => build(request, testContext));
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

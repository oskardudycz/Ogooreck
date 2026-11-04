using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ogooreck.Assertions;

namespace Ogooreck.API;

#pragma warning disable CS1591
public static class ApiSpecification
{
    ///////////////////
    ////   GIVEN   ////
    ///////////////////

    public static GivenApiSpecificationBuilder Given<TProgram>(this ApiSpecification<TProgram> api,
        params RequestTransform[] when) where TProgram : class =>
        api.Given(new RequestDefinition(when));


    public static GivenApiSpecificationBuilder Given<TProgram>(this ApiSpecification<TProgram> api,
        string description,
        params RequestTransform[] when) where TProgram : class =>
        api.Given(description, new RequestDefinition(when, description));

    ///////////////////
    ////   WHEN    ////
    ///////////////////

    public static RequestDefinition SEND(params RequestTransform[] when) => new(when);


    public static RequestDefinition SEND(string description, params RequestTransform[] when) => new(when, description);

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

    public static async Task<HttpResponseMessage> And(this Task<Result> result,
        Func<HttpResponseMessage, HttpResponseMessage> and) =>
        and((await result).Response);

    public static async Task And(this Task<Result> result, Func<HttpResponseMessage, Task> and) =>
        await and((await result).Response);

    public static async Task And<TResult>(this Task<Result> result,
        Func<HttpResponseMessage, Task<TResult>> and) =>
        await and((await result).Response);

    public static async Task And(this Task<Result> result, Func<Task> and)
    {
        await result;
        await and();
    }

    public static async Task And(this Task<Result> result, Task and)
    {
        await result;
        await and;
    }

    public static async Task<GivenApiSpecificationBuilder> And(this Task<Result> result)
    {
        var previous = await result;
        return new GivenApiSpecificationBuilder(previous.TestContext, previous.CreateClient);
    }

    public static Task<WhenApiSpecificationBuilder> AndWhen(
        this Task<Result> result,
        string description,
        params RequestTransform[] when
    ) =>
        result.And().When(description, when);

    public static Task<WhenApiSpecificationBuilder> AndWhen(this Task<Result> result, params RequestTransform[] when) =>
        result.And().When(when);

    public static async Task<WhenApiSpecificationBuilder> AndWhen(
        this Task<Result> result,
        string description,
        Func<HttpResponseMessage, RequestTransform[]> when
    )
    {
        var previous = await result;
        return new GivenApiSpecificationBuilder(previous.TestContext, previous.CreateClient)
            .When(description, when(previous.Response));
    }

    public static async Task<WhenApiSpecificationBuilder> AndWhen(
        this Task<Result> result,
        Func<HttpResponseMessage, RequestTransform[]> when
    )
    {
        var previous = await result;
        return new GivenApiSpecificationBuilder(previous.TestContext, previous.CreateClient).When(when(previous.Response));
    }

    public static async Task<WhenApiSpecificationBuilder> When(
        this Task<GivenApiSpecificationBuilder> result,
        string description,
        params RequestTransform[] when
    ) =>
        (await result).When(description, when);

    public static async Task<WhenApiSpecificationBuilder> When(
        this Task<GivenApiSpecificationBuilder> result,
        params RequestTransform[] when
    ) =>
        (await result).When(when);

    public static async Task<WhenApiSpecificationBuilder> Until(
        this Task<WhenApiSpecificationBuilder> when,
        RetryCheck check,
        int maxNumberOfRetries = 5,
        int retryIntervalInMs = 1000
    ) =>
        (await when).Until(check, maxNumberOfRetries, retryIntervalInMs);

    public static async Task<Result> Then(
        this Task<WhenApiSpecificationBuilder> when,
        ResponseAssert then
    ) =>
        await (await when).Then(then);

    public static async Task<Result> Then(
        this Task<WhenApiSpecificationBuilder> when,
        params ResponseAssert[] thens
    ) =>
        await (await when).Then(thens);

    public static async Task<Result> Then(
        this Task<WhenApiSpecificationBuilder> when,
        IEnumerable<ResponseAssert> thens,
        CancellationToken ct
    ) =>
        await (await when.WaitAsync(ct)).Then(thens, ct);


    public static Task<T> GetResponseBody<T>(this Task<Result> result) => result.Map(RESPONSE_BODY<T>());
    public static Task<T> GetCreatedId<T>(this Task<Result> result) => result.Map(CREATED_ID<T>());

    public static async Task<T> Map<T>(
        this Task<Result> result,
        Func<HttpResponseMessage, Task<T>> map
    ) =>
        await map((await result).Response);

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
            Expect.Equal(response.StatusCode, status, "HTTP status");
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
        RESPONSE_BODY<T>(result => Expect.Equivalent(result, body, "Response body"));

    public static ResponseAssert RESPONSE_BODY<T>(Func<TestContext, T> getBody) =>
        RESPONSE_BODY<T>((result, ctx) => Expect.Equivalent(result, getBody(ctx), "Response body"));

    public static ResponseAssert RESPONSE_BODY<T>(Action<T> assert) =>
        RESPONSE_BODY<T>((body, _) => assert(body));

    public static ResponseAssert RESPONSE_BODY<T>(Action<T, TestContext> assert) =>
        async (response, ctx) =>
        {
            var result = await response.GetResultFromJson<T>();
            assert(result, ctx);
        };

    public static Func<HttpResponseMessage, Task<T>> RESPONSE_BODY<T>() =>
        response => response.GetResultFromJson<T>();

    public static Func<HttpResponseMessage, Task<T>> CREATED_ID<T>() =>
        response => Task.FromResult(response.GetCreatedId<T>());

    public static ResponseAssert RESPONSE_ETAG_HEADER(object eTag, bool isWeak = true) =>
        RESPONSE_HEADERS(headers =>
        {
            Expect.That(headers.ETag is not null, "ETag response header should be defined.");
            Expect.That(!string.IsNullOrEmpty(headers.ETag.Tag), "ETag response header should not be empty.");
            Expect.Equal(headers.ETag.IsWeak, isWeak, "ETag weakness");
            Expect.Equal(headers.ETag.Tag, $"\"{eTag}\"", "ETag value");
        });

    public static ResponseAssert RESPONSE_LOCATION_HEADER(string? locationHeaderPrefix = null) =>
        async (response, ctx) =>
        {
            await HTTP_STATUS(HttpStatusCode.Created)(response, ctx);

            var locationHeader = response.Headers.Location;

            Expect.That(locationHeader is not null, "Location response header should be defined.");

            var location = locationHeader!.ToString();

            var prefix = locationHeaderPrefix ?? response.RequestMessage?.RequestUri?.AbsolutePath;
            Expect.That(prefix is not null, "A request URI or explicit Location prefix is required.");
            Expect.That(location.StartsWith(prefix, StringComparison.Ordinal),
                $"Expected Location <{location}> to start with <{prefix}>.");
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

    /////////////////
    //     UNTIL
    ////////////////

    public static RetryCheck RESPONSE_ETAG_IS(object eTag,
        bool isWeak = true) =>
        async (response, ctx) =>
        {
            await RESPONSE_ETAG_HEADER(eTag, isWeak)(response, ctx);
            return true;
        };

    public static RetryCheck RESPONSE_SUCCEEDED() =>
        (response, ctx) =>
        {
            response.EnsureSuccessStatusCode();
            return new ValueTask<bool>(true);
        };

    public static RetryCheck RESPONSE_BODY_MATCHES<TBody>(Func<TBody, bool> assert) =>
        async (response, ctx) =>
        {
            response.EnsureSuccessStatusCode();

            var result = await response.GetResultFromJson<TBody>();

            return assert(result);
        };

    public record Result(HttpResponseMessage Response, TestContext TestContext, Func<HttpClient> CreateClient);
}

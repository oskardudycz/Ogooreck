using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Ogooreck.Newtonsoft;

#pragma warning disable CS1591

namespace Ogooreck.API;

public delegate HttpRequestMessage RequestTransform(HttpRequestMessage request);

public static class ApiSpecification
{
    ///////////////////
    ////   GIVEN   ////
    ///////////////////

    ///////////////////
    ////   WHEN    ////
    ///////////////////
    public static RequestTransform URI(string uri) =>
        URI(new Uri(uri, UriKind.RelativeOrAbsolute));

    public static RequestTransform URI(Uri uri) =>
        request =>
        {
            request.RequestUri = uri;
            return request;
        };

    public static RequestTransform BODY<T>(T body) =>
        request =>
        {
            request.Content = JsonContent.Create(body);

            return request;
        };

    public static RequestTransform HEADERS(params Action<HttpRequestHeaders>[] headers) =>
        request =>
        {
            foreach (var header in headers)
            {
                header(request.Headers);
            }

            return request;
        };


    public static RequestTransform GET =>
        request =>
        {
            request.Method = HttpMethod.Get;
            return request;
        };

    public static RequestTransform POST => SEND(HttpMethod.Post);

    public static RequestTransform PUT => SEND(HttpMethod.Put);

    public static RequestTransform DELETE => SEND(HttpMethod.Delete);

    public static RequestTransform OPTIONS => SEND(HttpMethod.Options);

    public static RequestTransform SEND(HttpMethod method) =>
        request =>
        {
            request.Method = method;
            return request;
        };

    public static Action<HttpRequestHeaders> IF_MATCH(string ifMatch, bool isWeak = true) =>
        headers => headers.IfMatch.Add(new EntityTagHeaderValue($"\"{ifMatch}\"", isWeak));

    public static Action<HttpRequestHeaders> IF_MATCH(object ifMatch, bool isWeak = true) =>
        IF_MATCH(ifMatch.ToString()!, isWeak);

    public static Task<HttpResponseMessage> And(this Task<HttpResponseMessage> response,
        Func<HttpResponseMessage, HttpResponseMessage> and) =>
        response.ContinueWith(t => and(t.Result));

    public static Task And(this Task<HttpResponseMessage> response, Func<HttpResponseMessage, Task> and) =>
        response.ContinueWith(t => and(t.Result));

    public static Task And<TResult>(this Task<HttpResponseMessage> response,
        Func<HttpResponseMessage, Task<TResult>> and) =>
        response.ContinueWith(t => and(t.Result));

    public static Task And(this Task<HttpResponseMessage> response, Func<Task> and) =>
        response.ContinueWith(_ => and());

    public static Task And(this Task<HttpResponseMessage> response, Task and) =>
        response.ContinueWith(_ => and);


    // public static Func<HttpClient, Func<HttpRequestMessage>, Task<HttpResponseMessage>> GET_UNTIL(
    //     Func<HttpResponseMessage, ValueTask<bool>> check) =>
    //     SEND_UNTIL(HttpMethod.Get, check);
    //
    // public static Func<HttpClient, Func<HttpRequestMessage>, Task<HttpResponseMessage>> SEND_UNTIL(HttpMethod httpMethod,
    //     Func<HttpResponseMessage, ValueTask<bool>> check, int maxNumberOfRetries = 5, int retryIntervalInMs = 1000) =>
    //     async (api, buildRequest) =>
    //     {
    //         var retryCount = maxNumberOfRetries;
    //         var finished = false;
    //
    //         HttpResponseMessage? response = null;
    //         do
    //         {
    //             try
    //             {
    //                 var request = buildRequest();
    //                 request.Method = httpMethod;
    //
    //                 response = await api.SendAsync(request);
    //
    //                 finished = await check(response);
    //             }
    //             catch
    //             {
    //                 if (retryCount == 0)
    //                     throw;
    //             }
    //
    //             await Task.Delay(retryIntervalInMs);
    //             retryCount--;
    //         } while (!finished);
    //
    //         response.Should().NotBeNull();
    //
    //         return response!;
    //     };

    ///////////////////
    ////   THEN    ////
    ///////////////////
    public static Func<HttpResponseMessage, ValueTask> OK = HTTP_STATUS(HttpStatusCode.OK);
    public static Func<HttpResponseMessage, ValueTask> CREATED = HTTP_STATUS(HttpStatusCode.Created);
    public static Func<HttpResponseMessage, ValueTask> NO_CONTENT = HTTP_STATUS(HttpStatusCode.NoContent);
    public static Func<HttpResponseMessage, ValueTask> BAD_REQUEST = HTTP_STATUS(HttpStatusCode.BadRequest);
    public static Func<HttpResponseMessage, ValueTask> NOT_FOUND = HTTP_STATUS(HttpStatusCode.NotFound);
    public static Func<HttpResponseMessage, ValueTask> CONFLICT = HTTP_STATUS(HttpStatusCode.Conflict);

    public static Func<HttpResponseMessage, ValueTask> PRECONDITION_FAILED =
        HTTP_STATUS(HttpStatusCode.PreconditionFailed);

    public static Func<HttpResponseMessage, ValueTask> METHOD_NOT_ALLOWED =
        HTTP_STATUS(HttpStatusCode.MethodNotAllowed);

    public static Func<HttpResponseMessage, ValueTask> HTTP_STATUS(HttpStatusCode status) =>
        response =>
        {
            response.StatusCode.Should().Be(status);
            return ValueTask.CompletedTask;
        };


    public static Func<HttpResponseMessage, ValueTask> CREATED_WITH_DEFAULT_HEADERS(
        string? locationHeaderPrefix = null, object? eTag = null, bool isETagWeak = true) =>
        async response =>
        {
            await CREATED(response);
            await RESPONSE_LOCATION_HEADER(locationHeaderPrefix)(response);
            if (eTag != null)
                await RESPONSE_ETAG_HEADER(eTag, isETagWeak)(response);
        };

    public static Func<HttpResponseMessage, ValueTask> RESPONSE_BODY<T>(T body) =>
        RESPONSE_BODY<T>(result => result.Should().BeEquivalentTo(body));

    public static Func<HttpResponseMessage, ValueTask> RESPONSE_BODY<T>(Action<T> assert) =>
        async response =>
        {
            var result = await response.GetResultFromJson<T>();
            assert(result);

            result.Should().BeEquivalentTo(result);
        };

    public static Func<HttpResponseMessage, ValueTask<bool>> RESPONSE_ETAG_IS(object eTag, bool isWeak = true) =>
        async response =>
        {
            await RESPONSE_ETAG_HEADER(eTag, isWeak)(response);
            return true;
        };

    public static Func<HttpResponseMessage, ValueTask> RESPONSE_ETAG_HEADER(object eTag, bool isWeak = true) =>
        RESPONSE_HEADERS(headers =>
        {
            headers.ETag.Should().NotBeNull("ETag response header should be defined").And
                .NotBe("", "ETag response header should not be empty");
            headers.ETag!.Tag.Should().NotBeEmpty("ETag response header should not be empty");

            headers.ETag.IsWeak.Should().Be(isWeak, "Etag response header should be {0}", isWeak ? "Weak" : "Strong");
            headers.ETag.Tag.Should().Be($"\"{eTag}\"");
        });

    public static Func<HttpResponseMessage, ValueTask> RESPONSE_LOCATION_HEADER(string? locationHeaderPrefix = null) =>
        async response =>
        {
            await HTTP_STATUS(HttpStatusCode.Created)(response);

            var locationHeader = response.Headers.Location;

            locationHeader.Should().NotBeNull();

            var location = locationHeader!.ToString();

            location.Should().StartWith(locationHeaderPrefix ?? response.RequestMessage!.RequestUri!.AbsolutePath);
        };

    public static Func<HttpResponseMessage, ValueTask> RESPONSE_HEADERS(params Action<HttpResponseHeaders>[] headers) =>
        response =>
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
        new(createClient, builders);

    public async Task<HttpResponseMessage> Scenario(
        Task<HttpResponseMessage> first,
        params Func<HttpResponseMessage, Task<HttpResponseMessage>>[] following)
    {
        var response = await first;

        foreach (var next in following)
        {
            response = await next(response);
        }

        return response;
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

    public class GivenApiSpecificationBuilder
    {
        private readonly RequestTransform[][] given;
        private readonly Func<HttpClient> createClient;

        internal GivenApiSpecificationBuilder(Func<HttpClient> createClient, RequestTransform[][] given)
        {
            this.createClient = createClient;
            this.given = given;
        }

        public WhenApiSpecificationBuilder When(params RequestTransform[] when) =>
            new(createClient, given, when);
    }

    public class WhenApiSpecificationBuilder
    {
        private readonly RequestTransform[][] given;
        private readonly RequestTransform[] when;
        private readonly Func<HttpClient> createClient;

        internal WhenApiSpecificationBuilder(
            Func<HttpClient> createClient,
            RequestTransform[][] given,
            RequestTransform[] when
        )
        {
            this.createClient = createClient;
            this.given = given;
            this.when = when;
        }

        public Task<HttpResponseMessage> Then(Func<HttpResponseMessage, ValueTask> then) =>
            Then(new[] { then });

        public Task<HttpResponseMessage> Then(params Func<HttpResponseMessage, ValueTask>[] thens) =>
            Then(thens, default);

        public async Task<HttpResponseMessage> Then(IEnumerable<Func<HttpResponseMessage, ValueTask>> thens, CancellationToken ct)
        {
            using var client = createClient();

            // Given
            foreach (var givenBuilder in given)
                await client.Send(givenBuilder, ct: ct);

            // When
            var response = await client.Send(when, ct: ct);

            // Then
            foreach (var then in thens)
            {
                await then(response);
            }

            return response;
        }
    }

    public void Dispose()
    {
        applicationFactory.Dispose();
    }
}

public class AndSpecificationBuilder
{
    private readonly Lazy<Task<HttpResponseMessage>> then;

    public AndSpecificationBuilder(Lazy<Task<HttpResponseMessage>> then) =>
        this.then = then;

    public Task<HttpResponseMessage> Response => then.Value;
}

public class ApiRequest
{
    private readonly RequestTransform[] builders;

    public ApiRequest(params RequestTransform[] builders) =>
        this.builders = builders;

    public HttpRequestMessage Build() =>
        builders.Aggregate(new HttpRequestMessage(), (request, build) => build(request));

    public static HttpRequestMessage For(params RequestTransform[] builders) =>
        builders.Aggregate(new HttpRequestMessage(), (request, build) => build(request));
}

public static class ApiRequestExtensions
{
    public static Task<HttpResponseMessage> Send(
        this HttpClient httpClient,
        ApiRequest apiRequest,
        CancellationToken ct = default
    ) =>
        httpClient.SendAsync(apiRequest.Build(), ct);


    public static Task<HttpResponseMessage> Send(
        this HttpClient httpClient,
        RequestTransform[] builders,
        CancellationToken ct = default
    ) =>
        httpClient.SendAsync(ApiRequest.For(builders), ct);
}

public static class HttpResponseMessageExtensions
{
    public static bool TryGetCreatedId<T>(this HttpResponseMessage response, out T? value)
    {
        value = default;

        var locationHeader = response.Headers.Location?.OriginalString.TrimEnd('/');

        if (string.IsNullOrEmpty(locationHeader))
            return false;

        locationHeader = locationHeader.StartsWith("/") ? locationHeader : $"/{locationHeader}";

        var start = locationHeader.LastIndexOf("/", locationHeader.Length - 1);

        var createdId = locationHeader.Substring(start + 1, locationHeader.Length - 1 - start);

        var result = TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(createdId);

        if (result == null)
            return false;

        value = (T?)result;

        return true;
    }

    public static T GetCreatedId<T>(this HttpResponseMessage response) =>
        response.TryGetCreatedId<T>(out var createdId)
            ? createdId!
            : throw new ArgumentOutOfRangeException(nameof(response.Headers.Location));

    public static string GetCreatedId(this HttpResponseMessage response) =>
        response.GetCreatedId<string>();

    public static bool TryGetETagValue<T>(this HttpResponseMessage response, out T? value)
    {
        value = default;

        var eTagHeader = response.Headers.ETag?.Tag;

        if (string.IsNullOrEmpty(eTagHeader))
            return false;

        eTagHeader = eTagHeader.Substring(1, eTagHeader.Length - 2);

        var result = TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(eTagHeader);

        if (result == null)
            return false;

        value = (T?)result;

        return true;
    }

    public static T GetETagValue<T>(this HttpResponseMessage response) =>
        response.TryGetCreatedId<T>(out var createdId)
            ? createdId!
            : throw new ArgumentOutOfRangeException(nameof(response.Headers.ETag));

    public static string GetETagValue(this HttpResponseMessage response) =>
        response.GetETagValue<string>();

    public static async Task<T> GetResultFromJson<T>(this HttpResponseMessage response,
        JsonSerializerSettings? settings = null)
    {
        var result = await response.Content.ReadAsStringAsync();

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        var deserialised = result.FromJson<T>(settings);

        deserialised.Should().NotBeNull();

        return deserialised;
    }
}

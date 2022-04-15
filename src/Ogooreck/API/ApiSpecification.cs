using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ogooreck.API;

public static class ApiSpecification
{
    ///////////////////
    ////   GIVEN   ////
    ///////////////////

    public static Func<string> URL(string url) => () => url;

    ///////////////////
    ////   WHEN    ////
    ///////////////////

    public static Func<HttpClient, string, Task<HttpResponseMessage>> GET() => GET("");

    public static Func<HttpClient, string, Task<HttpResponseMessage>> GET(string urlSuffix) =>
        (api, request) => api.GetAsync($"{request}/{urlSuffix}");

    ///////////////////
    ////   THEN    ////
    ///////////////////

    public static Action<HttpResponseMessage> OK() => AssertResponseStatus(HttpStatusCode.OK);

    public static Action<HttpResponseMessage> CREATED(string apiPrefix) =>
        response =>
        {
            //response.RequestMessage.RequestUri.AbsolutePath
            AssertResponseStatus(HttpStatusCode.Created);

            var locationHeader = response.Headers.Location;

            locationHeader.Should().NotBeNull();

            var location = locationHeader!.OriginalString;

            location.Should().StartWith(apiPrefix);
            //assertDoesNotThrow(() => UUID.fromString(location.substring(apiPrefix.length() + 1)));
        };

    // public Action<HttpResponseMessage> BAD_REQUEST = AssertResponseStatus(HttpStatus.BAD_REQUEST);
    //
    // public Action<HttpResponseMessage> NOT_FOUND = AssertResponseStatus(HttpStatus.NOT_FOUND);
    //
    // public Action<HttpResponseMessage> CONFLICT = AssertResponseStatus(HttpStatus.CONFLICT);
    //
    // public Action<HttpResponseMessage> PRECONDITION_FAILED = AssertResponseStatus(HttpStatus.PRECONDITION_FAILED);
    //
    // public Action<HttpResponseMessage> METHOD_NOT_ALLOWED = AssertResponseStatus(HttpStatus.METHOD_NOT_ALLOWED);

    public static Action<HttpResponseMessage> AssertResponseStatus(HttpStatusCode status)
    {
        return response => response.StatusCode.Should().Be(status);
    }
}

public class ApiSpecification<TProgram>: IDisposable where TProgram : class
{
    private readonly WebApplicationFactory<TProgram> applicationFactory;
    private readonly HttpClient client;

    public ApiSpecification()
    {
        applicationFactory = new WebApplicationFactory<TProgram>();
        client = applicationFactory.CreateClient();
    }

    public GivenApiSpecificationBuilder<TRequest> Given<TRequest>(Func<TRequest> define)
    {
        return new GivenApiSpecificationBuilder<TRequest>(client, define);
    }

    // public Func<HttpClient, object, ResponseEntity> POST = POST("");
    //
    // public Func<HttpClient, object, ResponseEntity> POST(string urlSuffix) {
    //   return (api, request) => this.restTemplate
    //     .postForEntity(getApiUrl() + urlSuffix, request, Void.class);
    // }
    //
    // public Func<HttpClient, object, ResponseEntity> POST(string urlSuffix, ETag eTag) {
    //   return (api, request) => this.restTemplate
    //     .postForEntity(
    //       getApiUrl() + urlSuffix,
    //       new HttpEntity<>(request, getIfMatchHeader(eTag)),
    //       Void.class
    //     );
    // }
    //
    // public Func<HttpClient, object, ResponseEntity> PUT(ETag eTag) {
    //   return PUT("", eTag);
    // }
    //
    // public Func<HttpClient, object, ResponseEntity> PUT(string urlSuffix, ETag eTag) {
    //   return PUT(urlSuffix, eTag, true);
    // }
    //
    // public Func<HttpClient, object, ResponseEntity> PUT(string urlSuffix, ETag eTag, boolean withEmptyBody) {
    //   return (api, request) => this.restTemplate
    //     .exchange(
    //       getApiUrl() + urlSuffix + (withEmptyBody ? request : ""),
    //       HttpMethod.PUT,
    //       new HttpEntity<>(!withEmptyBody ? request : null, getIfMatchHeader(eTag)),
    //       Void.class
    //     );
    // }
    //
    // public Func<HttpClient, object, ResponseEntity> DELETE(ETag eTag) {
    //   return DELETE("", eTag);
    // }
    //
    // public Func<HttpClient, object, ResponseEntity> DELETE(string urlSuffix, ETag eTag) {
    //   return (api, request) => this.restTemplate
    //     .exchange(
    //       getApiUrl() + urlSuffix + request,
    //       HttpMethod.DELETE,
    //       new HttpEntity<>(null, getIfMatchHeader(eTag)),
    //       Void.class
    //     );
    // }
    //
    // HttpHeaders getHeaders(Consumer<HttpHeaders> consumer) {
    //   var headers = new HttpHeaders();
    //
    //   headers.setContentType(MediaType.APPLICATION_JSON);
    //   consumer.accept(headers);
    //
    //   return headers;
    // }
    //
    // HttpHeaders getIfMatchHeader(ETag eTag) {
    //   return getHeaders(headers => headers.setIfMatch(eTag.value()));
    // }
    //
    // HttpHeaders getIfNoneMatchHeader(ETag eTag) {
    //   return getHeaders(headers => {
    //     if (eTag != null)
    //       headers.setIfNoneMatch(eTag.value());
    //   });
    // }
    //
    //

    /////////////////////
    ////   BUILDER   ////
    /////////////////////

    public class GivenApiSpecificationBuilder<TRequest>
    {
        private readonly Func<TRequest> given;
        private readonly HttpClient client;

        internal GivenApiSpecificationBuilder(HttpClient client, Func<TRequest> given)
        {
            this.client = client;
            this.given = given;
        }

        public WhenApiSpecificationBuilder<TRequest> When(Func<HttpClient, TRequest, Task<HttpResponseMessage>> when) =>
            new(client, given, when);
    }

    public class WhenApiSpecificationBuilder<TRequest>
    {
        private readonly Func<TRequest> given;
        private readonly Func<HttpClient, TRequest, Task<HttpResponseMessage>> when;
        private readonly HttpClient client;

        internal WhenApiSpecificationBuilder(HttpClient client, Func<TRequest> given,
            Func<HttpClient, TRequest, Task<HttpResponseMessage>> when)
        {
            this.client = client;
            this.given = given;
            this.when = when;
        }

        public async Task<ThenApiSpecificationBuilder> Then(Action<HttpResponseMessage> then)
        {
            var request = given();

            var response = await when(client, request);

            then(response);

            return new ThenApiSpecificationBuilder(response);
        }
    }

    public class ThenApiSpecificationBuilder
    {
        private readonly HttpResponseMessage response;

        internal ThenApiSpecificationBuilder(HttpResponseMessage response)
        {
            this.response = response;
        }

        public ThenApiSpecificationBuilder And(Action<HttpResponseMessage> then)
        {
            then(response);

            return this;
        }
    }

    public void Dispose()
    {
        applicationFactory.Dispose();
        client.Dispose();
    }
}

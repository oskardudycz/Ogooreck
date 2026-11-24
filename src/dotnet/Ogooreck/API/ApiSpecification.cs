using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ogooreck.Assertions;

namespace Ogooreck.API;

/// <summary>
/// The step and expectation factories for API specifications. Import them with <c>using static Ogooreck.API.ApiSpecification;</c>.
/// </summary>
public partial class ApiSpecification
{
    /// <summary>Creates an in-process specification for <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> with all defaults.</summary>
    /// <example>
    /// <code>
    /// await using var api = ApiSpecification.For&lt;Program&gt;();
    /// </code>
    /// </example>
    public static ApiSpecification<TProgram> For<TProgram>() where TProgram : class => new();

    /// <summary>Creates an in-process specification whose app is customized by <paramref name="host"/>, for example with <c>WithWebHostBuilder</c>.</summary>
    /// <example>
    /// <code>
    /// await using var api = ApiSpecification.For&lt;Program&gt;(factory =&gt; factory.WithWebHostBuilder(builder =&gt;
    ///     builder.ConfigureTestServices(services =&gt; services.AddSingleton&lt;TimeProvider&gt;(clock))));
    /// </code>
    /// </example>
    public static ApiSpecification<TProgram> For<TProgram>(
        Func<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<TProgram>, Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<TProgram>> host)
        where TProgram : class => new(host);

    /// <summary>Joins a named group of steps with more steps into a new array.</summary>
    /// <example>
    /// <code>
    /// static readonly TestStep[] paidOrder = Scenario(placedOrder, Pay(orderId));
    /// </code>
    /// </example>
    public static TestStep[] Scenario(TestStep[] scenario, params TestStep[] more) => [.. scenario, .. more];

    /// <summary>An HTTP request sent with the specification's client.</summary>
    /// <example>
    /// <code>
    /// Request(client =&gt; client.PostAsync($"/carts/{cartId}/orders", null))
    /// </code>
    /// </example>
    public static TestStep Request(Func<HttpClient, Task<HttpResponseMessage>> send) =>
        new RequestStep((client, _) => send(client));

    /// <summary>An HTTP request that can read the responses of the earlier steps.</summary>
    /// <example>
    /// <code>
    /// Request((client, ctx) =&gt; client.GetAsync($"/api/products/{ctx.CreatedId&lt;Guid&gt;()}"))
    /// </code>
    /// </example>
    public static TestStep Request(Func<HttpClient, StepContext, Task<HttpResponseMessage>> send) =>
        new RequestStep(send);

    /// <summary>A Given request that is allowed to fail.</summary>
    /// <example>
    /// <code>
    /// api.Given(Attempt(GetCart(cartId))).When(CreateCart())
    /// </code>
    /// </example>
    public static TestStep Attempt(TestStep request) => new AttemptStep(request);

    /// <summary>Any code, such as advancing a fake clock or triggering a job. It produces no response.</summary>
    /// <example>
    /// <code>
    /// Action(ctx =&gt; { clock.Advance(TimeSpan.FromDays(1)); return Task.CompletedTask; })
    /// </code>
    /// </example>
    public static TestStep Action(Func<StepContext, Task> run) => new ActionStep(run);

    /// <summary>
    /// Wraps the When request so that Ogooreck re-sends it, back to back, while any <c>ExpectResponse</c> or <c>ExpectError</c> expectation fails.
    /// Use it only for an app with no idle signal, such as a read model that lags behind; when the app has one, plug it in with <c>CatchUp(...)</c> instead.
    /// </summary>
    /// <remarks>
    /// Each attempt gets a fresh trace id. Spans and measurements of every attempt count for the test; messages count only from the final attempt.
    /// When the response expectations pass, catch-up runs and the other expectations run once. At the deadline the test fails with the last response.
    /// It is allowed only as the When step, and it takes only a <c>Request(...)</c> step.
    /// </remarks>
    /// <example>
    /// <code>
    /// await api.Given(AddProduct(orderId, "socks"), PlaceOrder(orderId))
    ///     .When(Eventually(GetOrder(orderId)))
    ///     .Then([ExpectResponse(OK, Body(new { status = "Placed" }))], ct);
    /// </code>
    /// </example>
    public static TestStep Eventually(TestStep request) =>
        request is RequestStep step
            ? new EventuallyStep(step)
            : throw new ArgumentException(
                "Eventually(...) takes a Request(...) step; Attempt, Action, Publish and Eventually steps can't be re-sent.",
                nameof(request));

    /// <summary>Puts the messages on the app's bus through the function configured with <c>Publishing(...)</c>, as another module or system would.</summary>
    /// <example>
    /// <code>
    /// api.Given(Publish(new ShoppingCartCheckedOut(cartId))).When(GetOrder(cartId))
    /// </code>
    /// </example>
    public static TestStep Publish(params object[] messages) => new PublishStep(messages);

    /// <summary>Checks the When step's status, showing the body on failure, then each response check.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(Created, Location($"/orders/{cartId}"), Body(new { status = "Placed" }))
    /// </code>
    /// </example>
    public static Expectation ExpectResponse(HttpStatusCode status, params ResponseExpectation[] expectations) =>
        new(async outcome =>
        {
            var response = outcome.Response
                           ?? throw new AssertionException(
                               $"{outcome.When}: ExpectResponse({Describe(status)}) needs a response, but the When step is an {outcome.WhenKind}, which sends no request.");

            if (response.StatusCode != status)
                throw new AssertionException(
                    $"{outcome.When}: status: expected <{Describe(status)}>, but found <{Describe(response.StatusCode)}>. Body: {await response.Content.ReadAsStringAsync()}");

            foreach (var expectation in expectations)
                await expectation.Verify(response, outcome);
        }, checksResponse: true);

    /// <summary>Checks the When step's error status.</summary>
    /// <example>
    /// <code>
    /// ExpectError(NotFound)
    /// </code>
    /// </example>
    public static Expectation ExpectError(HttpStatusCode status) => ExpectResponse(status);

    /// <summary>Checks the When step's error status and matches the problem details partially.</summary>
    /// <example>
    /// <code>
    /// ExpectError(Conflict, new { title = "Cart is closed" })
    /// </code>
    /// </example>
    public static Expectation ExpectError(HttpStatusCode status, object problemDetails) =>
        ExpectResponse(status, Body(problemDetails));

    /// <summary>
    /// Matches the response JSON partially: every field of <paramref name="expected"/>, serialized with the specification's JSON options, must match.
    /// Extra response fields are allowed. Array order and length matter at every level.
    /// </summary>
    /// <example>
    /// <code>
    /// ExpectResponse(OK, Body(new { cartId, products = new[] { "socks" } }))
    /// </code>
    /// </example>
    public static ResponseExpectation Body(object expected) =>
        new(async (response, outcome) =>
        {
            var expectedJson = JsonSerializer.Serialize(expected, expected.GetType(), outcome.Json);
            var actualJson = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(actualJson))
                throw new AssertionException(
                    $"{outcome.When}: response body: expected {expectedJson}, but the response body is empty.");

            using var expectedDocument = JsonDocument.Parse(expectedJson);
            using var actualDocument = ParseResponse(actualJson, expectedJson, outcome);
            Expect.EquivalentWithEveryCollectionInOrder(
                ResponseJson.FieldsOf(actualDocument.RootElement, expectedDocument.RootElement),
                ResponseJson.Value(expectedDocument.RootElement),
                $"{outcome.When}: response body {actualJson} differs from the expected {expectedJson} at");
        });

    /// <summary>Requires exact values of the given response headers.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(OK, Headers(("X-Version", "4")))
    /// </code>
    /// </example>
    public static ResponseExpectation Headers(params (string Name, string Value)[] expected) =>
        Check((response, outcome) =>
        {
            foreach (var (name, value) in expected)
                Expect.Equal(HeaderValue(response, name) ?? "no such header", value, $"{outcome.When}: header {name}");
        });

    /// <summary>Requires the <c>ETag</c> header, for example <c>ETag(WeakETag(2))</c>.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(OK, ETag(WeakETag(2)))
    /// </code>
    /// </example>
    public static ResponseExpectation ETag(string expected) =>
        Check((response, outcome) =>
            Expect.Equal(response.Headers.ETag?.ToString() ?? "no ETag header", expected, $"{outcome.When}: ETag"));

    /// <summary>Formats a weak entity tag, <c>W/"n"</c>.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(OK, ETag(WeakETag(2)))
    /// </code>
    /// </example>
    public static string WeakETag(long version) => new EntityTagHeaderValue($"\"{version}\"", true).ToString();

    /// <summary>Requires the <c>Location</c> header.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(Created, Location($"/orders/{cartId}"))
    /// </code>
    /// </example>
    public static ResponseExpectation Location(string expected) =>
        Check((response, outcome) =>
            Expect.Equal(response.Headers.Location?.OriginalString ?? "no Location header", expected,
                $"{outcome.When}: Location"));

    /// <summary>Requires a <c>Location</c> header that matches <paramref name="matches"/>, for example a server-generated one.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(Created, Location(l =&gt; l.StartsWith("/api/products/")))
    /// </code>
    /// </example>
    public static ResponseExpectation Location(Func<string, bool> matches) =>
        Check((response, outcome) =>
        {
            var location = response.Headers.Location?.OriginalString;
            Expect.That(location is not null,
                $"{outcome.When}: expected a Location that matches the condition, but the response has no Location header.");
            Expect.That(matches(location), $"{outcome.When}: Location <{location}> does not match the expected condition.");
        });

    /// <summary>Runs a custom check of the response.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(OK, Satisfying(response =&gt; Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType)))
    /// </code>
    /// </example>
    public static ResponseExpectation Satisfying(Action<HttpResponseMessage> check) =>
        Check((response, _) => check(response));

    /// <summary>Runs a custom asynchronous check of the response.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(OK, Satisfying(async response =&gt; ids.Add(await response.Content.ReadAsStringAsync(ct))))
    /// </code>
    /// </example>
    public static ResponseExpectation Satisfying(Func<HttpResponseMessage, Task> check) =>
        new((response, _) => check(response));

    /// <summary>Requires an HTML response that contains every fragment.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(OK, HtmlContaining("Order placed", orderId.ToString()))
    /// </code>
    /// </example>
    public static ResponseExpectation HtmlContaining(params string[] fragments) =>
        new(async (response, outcome) =>
        {
            var html = await HtmlOf(response, outcome);
            var missing = fragments.Where(fragment => !html.Contains(fragment, StringComparison.Ordinal)).ToArray();
            Expect.That(missing.Length == 0,
                $"{outcome.When}: expected the HTML to contain <{string.Join(">, <", missing)}>, but it does not. HTML: {html}");
        });

    /// <summary>Requires an HTML response that contains none of the fragments.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(OK, HtmlNotContaining("Payment failed"))
    /// </code>
    /// </example>
    public static ResponseExpectation HtmlNotContaining(params string[] fragments) =>
        new(async (response, outcome) =>
        {
            var html = await HtmlOf(response, outcome);
            var found = fragments.Where(fragment => html.Contains(fragment, StringComparison.Ordinal)).ToArray();
            Expect.That(found.Length == 0,
                $"{outcome.When}: expected the HTML not to contain <{string.Join(">, <", found)}>, but it does. HTML: {html}");
        });

    /// <summary>Requires a <c>Location</c> whose path, without <c>;</c> parameters and query, is <paramref name="path"/>.</summary>
    /// <example>
    /// <code>
    /// ExpectResponse(Redirect, RedirectsTo($"/orders/{orderId}"))
    /// </code>
    /// </example>
    public static ResponseExpectation RedirectsTo(string path) =>
        Check((response, outcome) =>
        {
            var location = response.Headers.Location;
            Expect.That(location is not null,
                $"{outcome.When}: expected a redirect to <{path}>, but the response has no Location header.");
            var locationPath = new Uri(AnyBase, location).AbsolutePath.Split(';')[0];
            Expect.Equal(locationPath, path, $"{outcome.When}: path of Location {location.OriginalString}");
        });

    internal static string Describe(HttpStatusCode status) => $"{(int)status} {status}";

    internal static string Describe(HttpRequestMessage? request) =>
        $"{request?.Method} {request?.RequestUri?.PathAndQuery}";

    private static ResponseExpectation Check(Action<HttpResponseMessage, Outcome> check) =>
        new((response, outcome) =>
        {
            check(response, outcome);
            return Task.CompletedTask;
        });

    private static JsonDocument ParseResponse(string actualJson, string expectedJson, Outcome outcome)
    {
        try
        {
            return JsonDocument.Parse(actualJson);
        }
        catch (JsonException exception)
        {
            throw new AssertionException(
                $"{outcome.When}: response body: expected {expectedJson}, but the response body is not JSON: {actualJson}. The JSON parser reported: {exception.Message}",
                exception);
        }
    }

    private static readonly Uri AnyBase = new("http://localhost");

    private static string? HeaderValue(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : null;

    private static async Task<string> HtmlOf(HttpResponseMessage response, Outcome outcome)
    {
        var contentType = response.Content.Headers.ContentType?.ToString();
        Expect.That(string.Equals(response.Content.Headers.ContentType?.MediaType, "text/html", StringComparison.OrdinalIgnoreCase),
            $"{outcome.When}: expected an HTML response, but the content type is {contentType ?? "missing"}.");
        return await response.Content.ReadAsStringAsync();
    }
}

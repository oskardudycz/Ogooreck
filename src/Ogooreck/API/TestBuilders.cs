namespace Ogooreck.API;

#pragma warning disable CS1591

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

public record MadeApiCall(HttpRequestMessage Request, HttpResponseMessage Response);

public class TestContext
{
    public List<MadeApiCall> Calls { get; } = new();

    public void Record(HttpRequestMessage request, HttpResponseMessage response) =>
        Calls.Add(new MadeApiCall(request, response));

    public T GetCreatedId<T>() where T : notnull =>
        Calls.First().Response.GetCreatedId<T>();
}

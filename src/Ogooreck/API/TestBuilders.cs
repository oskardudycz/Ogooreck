using System.Net;

namespace Ogooreck.API;

#pragma warning disable CS1591

public class GivenApiSpecificationBuilder
{
    private readonly ApiTestStep[] given;
    private readonly Func<HttpClient> createClient;
    private readonly TestContext testContext;

    internal GivenApiSpecificationBuilder
    (
        TestContext testContext,
        Func<HttpClient> createClient,
        ApiTestStep[] given
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
    ): this(testContext, createClient, Array.Empty<ApiTestStep>())
    {
    }

    public WhenApiSpecificationBuilder When(params RequestTransform[] when) =>
        When("", when);

    public WhenApiSpecificationBuilder When(string description, params RequestTransform[] when) =>
        When("", new RequestDefinition(when, description));

    public WhenApiSpecificationBuilder When(RequestDefinition when) => When(when.Description, when);

    public WhenApiSpecificationBuilder When(string description, RequestDefinition when) =>
        new(
            createClient,
            testContext,
            given,
            new ApiTestStep(TestPhase.When, when, description)
        );
}

public class WhenApiSpecificationBuilder
{
    private readonly ApiTestStep[] given;
    private readonly ApiTestStep when;
    private readonly Func<HttpClient> createClient;
    private readonly TestContext testContext;
    private RetryPolicy retryPolicy;

    internal WhenApiSpecificationBuilder(
        Func<HttpClient> createClient,
        TestContext testContext,
        ApiTestStep[] given,
        ApiTestStep when
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
            await Send(client, RetryPolicy.NoRetry, givenBuilder, testContext, ct);

        // When
        var response = await Send(client, retryPolicy, when, testContext, ct);

        // Then
        foreach (var then in thens)
        {
            await then(response, testContext);
        }

        return new ApiSpecification.Result(response, testContext, createClient);
    }

    private static Task<HttpResponseMessage> Send(
        HttpClient client,
        RetryPolicy retryPolicy,
        ApiTestStep testStep,
        TestContext testContext,
        CancellationToken ct
    ) =>
        retryPolicy
            .Perform(async t =>
            {
                var request = TestApiRequest.For(testContext, testStep.RequestDefinition);
                var response = await client.SendAsync(request, t);

                testContext.Record(testStep, request, response);

                return response;
            }, testContext, ct);
}

public enum TestPhase
{
    Given,
    When,
    Then
}

public record RequestDefinition(RequestTransform[] Transformations, string Description = "");

public record ApiTestStep(TestPhase Phase, RequestDefinition RequestDefinition, string Description = "");

public record MadeApiCall(TestPhase TestPhase, HttpRequestMessage Request, HttpResponseMessage Response,
    string Description = "");

public class TestContext
{
    public List<MadeApiCall> Calls { get; } = new();

    public void Record(ApiTestStep testStep, HttpRequestMessage request, HttpResponseMessage response) =>
        Calls.Add(new MadeApiCall(testStep.Phase, request, response, testStep.RequestDefinition.Description));


    public string GetCreatedId() =>
        Calls.First(c => c.Response.StatusCode == HttpStatusCode.Created).Response.GetCreatedId();

    public T GetCreatedId<T>() where T : notnull =>
        Calls.First(c => c.Response.StatusCode == HttpStatusCode.Created).Response.GetCreatedId<T>();
}

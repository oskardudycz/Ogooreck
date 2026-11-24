using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests;

public class StepTests(ApiSpecification<Program> api): IClassFixture<ApiSpecification<Program>>
{
    private readonly Guid cartId = Guid.NewGuid();

    [Fact]
    public Task PostRequestStep_SendsTheBodyToTheApp() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { products = new[] { "socks" } }))], TestContext.Current.CancellationToken);

    [Fact]
    public async Task CreatedIdFromAnEarlierResponse_IsUsedInTheNextRequestUrl()
    {
        string? location = null;

        await api.Given(CreateCart())
            .When(Request((client, ctx) =>
            {
                location = ctx.Responses[0].Headers.Location!.OriginalString;
                return client.GetAsync($"/carts/{ctx.CreatedId()}", TestContext.Current.CancellationToken);
            }))
            .Then([ExpectResponse(OK,
                Satisfying(response => Assert.Equal(location, response.RequestMessage!.RequestUri!.AbsolutePath)))], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreatedIdOfT_ParsesTheLocation()
    {
        int? createdId = null;
        CartCreated? created = null;

        await api.Given(CreateCart())
            .When(Request(async (client, ctx) =>
            {
                createdId = ctx.CreatedId<int>();
                created = await ctx.Responses[0].Content.ReadFromJsonAsync<CartCreated>(TestContext.Current.CancellationToken);
                return await client.GetAsync($"/carts/{createdId}", TestContext.Current.CancellationToken);
            }))
            .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken);

        Assert.Equal(created!.Id, createdId);
    }

    [Fact]
    public async Task CreatedId_WithoutACreatedResponse_FailsSayingNoCreatedResponseWasFound()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(Request((client, ctx) => client.GetAsync($"/carts/{ctx.CreatedId()}", TestContext.Current.CancellationToken)))
                .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        Assert.Contains("When request: no created response was found among the 1 earlier responses", error.Message);
        Assert.Contains($"response 1: 200 OK from POST /carts/{cartId}/products", error.Message);
    }

    [Fact]
    public async Task CreatedId_OnA201WithoutLocation_FailsNamingTheStepAndTheResponse()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(Request(client => client.PostAsync("/drafts", null, TestContext.Current.CancellationToken)))
                .When(Request((client, ctx) => client.GetAsync($"/carts/{ctx.CreatedId()}", TestContext.Current.CancellationToken)))
                .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        Assert.Contains(
            "When request: the first 201 Created response (response 1: 201 Created from POST /drafts) has no Location header, so there is no created id.",
            error.Message);
    }

    [Fact]
    public async Task CreatedIdOfT_WhenTheIdDoesNotConvert_FailsNamingTheStepTheLocationAndTheType()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(AddProduct(cartId, "socks"), PlaceOrder(cartId))
                .When(Request((client, ctx) => client.GetAsync($"/orders/{ctx.CreatedId<int>()}", TestContext.Current.CancellationToken)))
                .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        Assert.Contains(
            $"When request: the first 201 Created response (response 2: 201 Created from POST /carts/{cartId}/orders) has Location </orders/{cartId}>, whose id can't be read as Int32:",
            error.Message);
        Assert.NotNull(error.InnerException);
        Assert.Contains(error.InnerException.Message, error.Message);
    }

    [Fact]
    public async Task CancellingThen_AfterTheGivenSteps_SaysTheWhenStepDidNotStart()
    {
        using var cancellation = new CancellationTokenSource();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.Given(Action(async _ => await cancellation.CancelAsync()))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK)], cancellation.Token));

        Assert.Equal("Then was cancelled before the When step started.", error.Message);
    }

    [Fact]
    public async Task CancellingThen_DuringAnExpectation_SaysWhichExpectationDidNotRun()
    {
        using var cancellation = new CancellationTokenSource();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK, Satisfying(_ => cancellation.Cancel())), ExpectResponse(OK)],
                    cancellation.Token));

        Assert.Equal("Then was cancelled before expectation 2 of 2 ran.", error.Message);
    }

    [Fact]
    public async Task CreatedId_ReadsOnlyTheFirst201Response()
    {
        var bodies = new List<string>();
        string? createdId = null;

        await api.Given(CreateCart(), CreateCart())
            .When(Request(async (client, ctx) =>
            {
                createdId = ctx.CreatedId();
                foreach (var response in ctx.Responses)
                    bodies.Add(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                return await client.GetAsync($"/carts/{createdId}", TestContext.Current.CancellationToken);
            }))
            .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken);

        Assert.Equal($$"""{"id":{{createdId}}}""", bodies[0]);
        Assert.NotEqual(bodies[0], bodies[1]);
    }

    [Fact]
    public async Task Responses_ListsEarlierResponsesInStepOrder()
    {
        var bodies = new List<string>();

        await api.Given(AddProduct(cartId, "socks"), AddProduct(cartId, "shoes"))
            .When(Request(async (client, ctx) =>
            {
                foreach (var response in ctx.Responses)
                    bodies.Add(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                return await client.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken);
            }))
            .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken);

        Assert.Equal(2, bodies.Count);
        Assert.Contains("""["socks"]""", bodies[0]);
        Assert.Contains("""["socks","shoes"]""", bodies[1]);
    }

    [Fact]
    public async Task FailingGivenRequest_FailsBeforeWhen_ShowingStatusAndBody()
    {
        var whenRuns = 0;

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(GetCart(cartId))
                .When(Request(client =>
                {
                    whenRuns++;
                    return client.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken);
                }))
                .Then([ExpectResponse(NotFound)], TestContext.Current.CancellationToken));

        Assert.Contains("404", error.Message);
        Assert.Contains("""{"title":"Cart not found"}""", error.Message);
        Assert.Equal(0, whenRuns);
    }

    [Fact]
    public async Task FailingGivenRequest_AtEachPosition_FailsWithTheFailedStepsStatus()
    {
        var thirdRuns = 0;

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(
                    AddProduct(cartId, "socks"),
                    Request(client => client.GetAsync("/failure", TestContext.Current.CancellationToken)),
                    Request(client =>
                    {
                        thirdRuns++;
                        return client.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken);
                    }))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        Assert.Contains("Given step 2 request GET /failure failed with 500 InternalServerError", error.Message);
        Assert.Equal(0, thirdRuns);
    }

    [Fact]
    public async Task GivenRequestFailure_NamesTheStepRequestStatusAndBody()
    {
        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(GetCart(cartId))
                .When(GetCart(cartId))
                .Then([ExpectResponse(NotFound)], TestContext.Current.CancellationToken));

        Assert.Contains($"Given step 1 request GET /carts/{cartId} failed with 404 NotFound", error.Message);
        Assert.Contains("""Body: {"title":"Cart not found"}""", error.Message);
    }

    [Fact]
    public async Task AttemptedGivenRequest_ThatFails_LetsTheWhenStepRun()
    {
        var whenRuns = 0;
        var unknownCartId = Guid.NewGuid();
        TestStep[] rejectedOrder = [Attempt(PlaceOrder(unknownCartId))];
        var getCart = Request(client =>
        {
            whenRuns++;
            return client.GetAsync($"/carts/{unknownCartId}", TestContext.Current.CancellationToken);
        });

        await api.Given(rejectedOrder).When(getCart).Then([ExpectResponse(NotFound)], TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given(rejectedOrder).When(getCart).Then([ExpectResponse(OK)], TestContext.Current.CancellationToken));

        Assert.Equal(2, whenRuns);
    }

    [Fact]
    public Task GivenWithAStepArrayAndMoreSteps_RunsBothInOrder()
    {
        TestStep[] cartWithSocks = [AddProduct(cartId, "socks")];

        return api.Given(cartWithSocks, ConfirmCart(cartId))
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { products = new[] { "socks" }, status = "Confirmed" }))], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ScenarioJoiningAnArrayAndSteps_RunsBothInOrder()
    {
        TestStep[] cartWithSocks = [AddProduct(cartId, "socks")];

        var confirmedCart = Scenario(cartWithSocks, ConfirmCart(cartId));

        Assert.Equal(2, confirmedCart.Length);
        Assert.Single(cartWithSocks);
        await api.Given(confirmedCart)
            .When(GetCart(cartId))
            .Then([ExpectResponse(OK, Body(new { products = new[] { "socks" }, status = "Confirmed" }))], TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EveryRequest_CarriesATraceparentWithAUniqueTraceIdPerStep()
    {
        var traceparents = new List<string>();

        await api.Given(GetTraceparent(), GetTraceparent())
            .When(Request(async (client, ctx) =>
            {
                foreach (var response in ctx.Responses)
                    traceparents.Add(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                return await client.GetAsync("/traceparent", TestContext.Current.CancellationToken);
            }))
            .Then([ExpectResponse(OK,
                Satisfying(async response => traceparents.Add(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))))], TestContext.Current.CancellationToken);

        Assert.Equal(3, traceparents.Count);
        Assert.All(traceparents, traceparent =>
            Assert.Matches(new Regex("^00-[0-9a-f]{32}-[0-9a-f]{16}-01$"), traceparent));
        Assert.Equal(3, traceparents.Select(traceparent => traceparent.Split('-')[1]).Distinct().Count());
    }

    [Fact]
    public async Task Then_WithACancelledToken_EndsWithOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.Given(AddProduct(cartId, "socks"))
                .When(GetCart(cartId))
                .Then([ExpectResponse(OK)], cancellation.Token));

        Assert.Equal("Then was cancelled before Given step 1 started.", error.Message);
    }

    [Theory]
    [InlineData("/slow", HttpCompletionOption.ResponseContentRead)]
    [InlineData("/stream", HttpCompletionOption.ResponseHeadersRead)]
    public async Task CancellingThen_DuringTheWhenRequest_NamesTheRequest(string endpoint,
        HttpCompletionOption completion)
    {
        using var cancellation = new CancellationTokenSource();
        var neverEnding = Request(client =>
        {
            cancellation.Cancel();
            return client.GetAsync(endpoint, completion, TestContext.Current.CancellationToken);
        });

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.Given()
                .When(neverEnding)
                .Then([ExpectResponse(OK)], cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.StartsWith($"Then was cancelled while the When request GET {endpoint} was running.", error.Message);
        if (error.InnerException is { } stepFailure and not OperationCanceledException)
            Assert.Contains(stepFailure.Message, error.Message);
    }

    [Fact]
    public async Task CancellingThen_WhileAWhenActionWaits_StopsTheAction()
    {
        using var cancellation = new CancellationTokenSource();
        var waitingAction = Action(async ctx =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, ctx.CancellationToken);
        });

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            api.Given()
                .When(waitingAction)
                .Then([ExpectResponse(OK)], cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.StartsWith("Then was cancelled while the When action was running.", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposing_StopsTheHost(bool async)
    {
        WebApplicationFactory<Program>? factory = null;
        var spec = For<Program>(f => factory = f);
        await spec.Given().When(GetCart(cartId)).Then([ExpectResponse(NotFound)], TestContext.Current.CancellationToken);
        using var clientOfTheHost = factory!.CreateClient();
        var beforeDisposal = await clientOfTheHost.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken);
        Assert.Equal(NotFound, beforeDisposal.StatusCode);

        if (async)
            await spec.DisposeAsync();
        else
            spec.Dispose();

        await Assert.ThrowsAnyAsync<ObjectDisposedException>(() => clientOfTheHost.GetAsync($"/carts/{cartId}", TestContext.Current.CancellationToken));
    }

    [Fact]
    public Task Redirect_IsNotFollowed_SoRedirectsToSeesThe302() =>
        api.Given(AddProduct(cartId, "socks"))
            .When(Request(client => client.GetAsync($"/carts/{cartId}/summary", TestContext.Current.CancellationToken)))
            .Then([ExpectResponse(Found, RedirectsTo($"/carts/{cartId}"))], TestContext.Current.CancellationToken);

    [Fact]
    public async Task ClientDefaults_ChangedInOneStep_DoNotReachAParallelTest()
    {
        var otherCartId = Guid.NewGuid();
        var echoHeaders = Request(client => client.GetAsync("/headers", TestContext.Current.CancellationToken));
        var noTenant = ExpectResponse(OK, Satisfying(async response =>
            Assert.DoesNotContain("X-Tenant", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))));

        await Task.WhenAll(
            api.Given(Request(client =>
                {
                    client.DefaultRequestHeaders.Add("X-Tenant", "a");
                    return client.PostAsJsonAsync($"/carts/{cartId}/products", new { product = "socks" }, TestContext.Current.CancellationToken);
                }))
                .When(echoHeaders)
                .Then([noTenant], TestContext.Current.CancellationToken),
            api.Given(AddProduct(otherCartId, "shoes"))
                .When(echoHeaders)
                .Then([noTenant], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HostCallThatCustomisesTheFactory_IsUsedByTheApp()
    {
        var placedAt = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        using var spec = For<Program>(factory => WithFixedTime(factory, placedAt));
        using var subclass = new FixedTimeApi(placedAt);

        foreach (var api in new[] { spec, subclass })
            await api.Given(AddProduct(cartId, "socks"))
                .When(PlaceOrder(cartId))
                .Then([ExpectResponse(Created, Body(new { placedAt }))], TestContext.Current.CancellationToken);
    }

    private static WebApplicationFactory<Program> WithFixedTime(WebApplicationFactory<Program> factory,
        DateTimeOffset now) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new FixedTime(now))));

    private sealed class FixedTimeApi(DateTimeOffset now): ApiSpecification<Program>(
        factory => WithFixedTime(factory, now), spec => spec);

    private sealed class FixedTime(DateTimeOffset now): TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

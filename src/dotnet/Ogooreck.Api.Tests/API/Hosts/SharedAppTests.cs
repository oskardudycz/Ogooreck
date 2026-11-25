using Ogooreck.Api.Tests.API.Messages.Fixtures;
using Ogooreck.API;
using Ogooreck.Assertions;
using Ogooreck.Testing;
using Xunit;
using static System.Net.HttpStatusCode;
using static Ogooreck.Api.Tests.Fixtures.ShopRequests;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Api.Tests.API.Hosts;

public class SharedAppTests
{
    private readonly Guid cartId = Guid.NewGuid();
    private readonly CancellationToken ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task With_RunsOnTheSameApp()
    {
        var starts = 0;
        await using var app = For<Program>(factory =>
        {
            starts++;
            return factory;
        });
        var variant = app.With(spec => spec.Deadline(TimeSpan.FromSeconds(5)));

        var first = await InstanceOf(app);
        var second = await InstanceOf(variant);

        Assert.Equal(first, second);
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task With_StartsTheAppWhenItRunsFirst()
    {
        await using var app = For<Program>();
        var variant = app.With(spec => spec);

        var first = await InstanceOf(variant);
        var second = await InstanceOf(app);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task With_ChangesTheDeadlineOfTheVariantOnly()
    {
        await using var app = For<Program>();
        var strict = app.With(spec => spec.Clock(new PassingClock()).Deadline(TimeSpan.FromSeconds(1)));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            strict.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("never"))], ct));

        Assert.Contains("when the deadline of 00:00:01 passed", error.Message);
    }

    [Fact]
    public async Task With_KeepsTheTestSetupItDoesNotChange()
    {
        var waits = 0;
        await using var app = For<Program>().CatchUp(_ =>
        {
            Interlocked.Increment(ref waits);
            return Task.CompletedTask;
        });
        var variant = app.With(spec => spec.Deadline(TimeSpan.FromSeconds(5)));

        await variant.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Equal(1, waits);
    }

    [Fact]
    public async Task With_AddsTestSetupToTheVariantOnly()
    {
        var waits = 0;
        await using var app = For<Program>();
        var variant = app.With(spec => spec.CatchUp(_ =>
        {
            Interlocked.Increment(ref waits);
            return Task.CompletedTask;
        }));

        await app.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);
        await variant.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Equal(1, waits);
    }

    [Fact]
    public async Task With_DisposingTheVariant_LeavesTheAppRunning()
    {
        await using var app = For<Program>();
        var first = await InstanceOf(app);

        await using (var variant = app.With(spec => spec))
            await InstanceOf(variant);

        Assert.Equal(first, await InstanceOf(app));
    }

    [Fact]
    public async Task With_DisposedSynchronously_LeavesTheAppRunning()
    {
        await using var app = For<Program>();
        var first = await InstanceOf(app);

        using (var variant = app.With(spec => spec))
            await InstanceOf(variant);

        Assert.Equal(first, await InstanceOf(app));
    }

    [Fact]
    public async Task With_DisposingTheVariant_LeavesTheAppsTelemetryRecording()
    {
        await using var app = For<Program>();

        await using (var variant = app.With(spec => spec))
            await variant.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("GET /carts/{id}"))], ct);

        await app.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("GET /carts/{id}"))], ct);
    }

    [Fact]
    public async Task With_DisposedSynchronously_LeavesTheAppsTelemetryRecording()
    {
        await using var app = For<Program>();

        using (var variant = app.With(spec => spec))
            await variant.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("GET /carts/{id}"))], ct);

        await app.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("GET /carts/{id}"))], ct);
    }

    [Fact]
    public async Task With_PluginThatChangesTheAppsServices_FailsNamingWithNewApp()
    {
        await using var app = For<Program>();

        var error = Assert.Throws<InvalidOperationException>(() => app.With(spec => spec.Use(new ServicePlugin())));

        Assert.Equal(
            "Plugin ServicePlugin changes the app's services, so it can't run on the shared app. Add it to the app's setup, or use WithNewApp(...) to start a separate app for it.",
            error.Message);
    }

    [Fact]
    public async Task With_PluginOfTheApp_RunsItsStartingOnlyOnce()
    {
        var plugin = new ServicePlugin();
        await using var app = For<Program>().Use(plugin);
        var variant = app.With(spec => spec.Use(plugin));

        await app.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);
        await variant.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Equal(1, plugin.Starts);
        Assert.Equal(1, plugin.Configured);
    }

    [Fact]
    public async Task With_PluginThatOnlyStarts_RunsItsStartingForTheVariant()
    {
        var plugin = new StartingPlugin();
        await using var app = For<Program>();
        var variant = app.With(spec => spec.Use(plugin));

        await variant.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);
        await variant.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Equal(1, plugin.Starts);
    }

    [Fact]
    public async Task With_VariantOfAVariant_RunsOnTheSameApp()
    {
        await using var app = For<Program>();
        var variant = app.With(spec => spec.Deadline(TimeSpan.FromSeconds(5))).With(spec => spec);

        Assert.Equal(await InstanceOf(app), await InstanceOf(variant));
    }

    [Fact]
    public async Task With_VariantOfAVariantReusingAPlugin_RunsItsStartingOnlyOnce()
    {
        var plugin = new StartingPlugin();
        await using var app = For<Program>();
        var variant = app.With(spec => spec.Use(plugin)).With(spec => spec.Use(plugin));

        await variant.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Equal(1, plugin.Starts);
    }

    [Fact]
    public async Task WithNewApp_OfAVariantOfAVariant_KeepsThePluginsOfEveryVariant()
    {
        var plugin = new StartingPlugin();
        await using var app = For<Program>();
        var variant = app.With(spec => spec.Use(plugin)).With(spec => spec);
        await using var separate = variant.WithNewApp(spec => spec);

        await separate.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Equal(1, plugin.Starts);
    }

    [Fact]
    public async Task WithNewApp_StartsASeparateAppWithTheSameSetup()
    {
        var plugin = new ServicePlugin();
        var starts = 0;
        await using var app = For<Program>(factory =>
        {
            starts++;
            return factory;
        }).Use(plugin);
        await using var separate = app.WithNewApp(spec => spec.Deadline(TimeSpan.FromSeconds(5)));

        var first = await InstanceOf(app);
        var second = await InstanceOf(separate);

        Assert.NotEqual(first, second);
        Assert.Equal(2, starts);
        Assert.Equal(2, plugin.Configured);
    }

    [Fact]
    public async Task WithNewApp_OfAVariant_KeepsTheVariantsTestSetup()
    {
        var waits = 0;
        await using var app = For<Program>();
        var variant = app.With(spec => spec.CatchUp(_ =>
        {
            Interlocked.Increment(ref waits);
            return Task.CompletedTask;
        }));
        await using var separate = variant.WithNewApp(spec => spec);

        await separate.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Equal(1, waits);
        Assert.NotEqual(await InstanceOf(app), await InstanceOf(separate));
    }

    [Fact]
    public async Task WithNewApp_CustomizesTheHostAfterTheAppsOwnCustomization()
    {
        var customizations = new List<string>();
        await using var app = For<Program>(factory =>
        {
            customizations.Add("app");
            return factory;
        });
        await using var separate = app.WithNewApp(factory =>
        {
            customizations.Add("separate");
            return factory;
        }, spec => spec);

        await separate.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Equal(["app", "separate"], customizations);
    }

    [Fact]
    public async Task With_ChecksTracesWithTheAppsTelemetry()
    {
        await using var app = For<Program>();
        var variant = app.With(spec => spec.Deadline(TimeSpan.FromSeconds(5)));

        await variant.Given(AddProduct(cartId, "socks"))
            .When(PlaceOrder(cartId))
            .Then([ExpectResponse(Created), ExpectTrace(Span("POST /carts/{id}/orders", Span("publish OrderPlaced")))], ct);
    }

    [Fact]
    public async Task With_OwnTelemetryOff_TurnsTelemetryOffForTheVariantOnly()
    {
        await using var app = For<Program>();
        var variant = app.With(spec => spec.Telemetry(TelemetrySources.Off));

        await Assert.ThrowsAsync<AssertionException>(() =>
            variant.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("GET /carts/{id}"))], ct));
        await app.Given().When(GetCart(cartId)).Then([ExpectTrace(Span("GET /carts/{id}"))], ct);
    }

    [Fact]
    public async Task With_OwnPublishing_ReplacesTheAppsPublishing()
    {
        var published = new List<string>();
        await using var app = For<Program>().Publishing((_, _) =>
        {
            published.Add("app");
            return Task.CompletedTask;
        });
        var variant = app.With(spec => spec.Publishing((_, _) =>
        {
            published.Add("variant");
            return Task.CompletedTask;
        }));

        await variant.Given().When(Publish(new object())).Then([], ct);

        Assert.Equal(["variant"], published);
    }

    [Fact]
    public async Task With_SecondPublishingOfTheVariant_Throws()
    {
        await using var app = For<Program>();

        Assert.Throws<InvalidOperationException>(() => app.With(spec => spec
            .Publishing((_, _) => Task.CompletedTask)
            .Publishing((_, _) => Task.CompletedTask)));
    }

    [Fact]
    public async Task With_AfterTheVariantsFirstStep_RejectsConfiguration()
    {
        await using var app = For<Program>();
        var variant = app.With(spec => spec);
        await variant.Given().When(GetCart(cartId)).Then([ExpectError(NotFound)], ct);

        Assert.Throws<InvalidOperationException>(() => variant.Deadline(TimeSpan.FromSeconds(1)));
    }

    private async Task<string> InstanceOf(ApiSpecification<Program> spec)
    {
        var instance = "";
        await spec.Given()
            .When(Request(client => client.GetAsync("/instance", ct)))
            .Then([ExpectResponse(OK, Satisfying(async response => instance = await response.Content.ReadAsStringAsync(ct)))], ct);
        return instance;
    }

    private sealed class ServicePlugin: ISpecificationPlugin
    {
        public int Starts;
        public int Configured;

        public void Register(SpecificationPluginRegistration registration) =>
            registration.Services(_ => Interlocked.Increment(ref Configured)).Starting(_ =>
            {
                Interlocked.Increment(ref Starts);
                return Task.CompletedTask;
            });
    }

    private sealed class StartingPlugin: ISpecificationPlugin
    {
        public int Starts;

        public void Register(SpecificationPluginRegistration registration) =>
            registration.Starting(_ =>
            {
                Interlocked.Increment(ref Starts);
                return Task.CompletedTask;
            });
    }
}

using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests.API.Plugins;

public class PluginTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ApiSpecification Api() => For(() => Task.FromResult(new Uri("http://localhost")));

    private static Task<PublishRequest> PublishedBy(ApiSpecification api)
    {
        var published = new TaskCompletionSource<PublishRequest>();
        api.Use(new PublishingPlugin((request, _) =>
        {
            published.SetResult(request);
            return Task.CompletedTask;
        }));
        return published.Task;
    }

    private sealed class PublishingPlugin(Func<PublishRequest, CancellationToken, Task> publish): ISpecificationPlugin
    {
        public void Register(SpecificationPluginRegistration registration) => registration.Publishing(publish);
    }

    private sealed class OtherPublishingPlugin(Func<PublishRequest, CancellationToken, Task> publish): ISpecificationPlugin
    {
        public void Register(SpecificationPluginRegistration registration) => registration.Publishing(publish);
    }

    private sealed class ServicesPlugin: ISpecificationPlugin
    {
        public void Register(SpecificationPluginRegistration registration) =>
            registration.Services(services => services.AddSingleton("registered"));
    }

    private sealed class DeadlinePlugin: ISpecificationPlugin
    {
        public TimeSpan Seen { get; private set; }

        public void Register(SpecificationPluginRegistration registration) =>
            registration.CatchUp(_ =>
            {
                Seen = registration.Deadline;
                return Task.CompletedTask;
            });
    }

    private sealed class AppServicesPlugin: ISpecificationPlugin
    {
        public Func<IServiceProvider> AppServices { get; private set; } = null!;

        public void Register(SpecificationPluginRegistration registration) => AppServices = () => registration.AppServices;
    }

    private sealed class StartingPlugin(string name, List<string> events, bool fails = false): ISpecificationPlugin
    {
        public void Register(SpecificationPluginRegistration registration) =>
            registration.Starting(_ =>
            {
                events.Add($"{name} starts");
                return fails ? throw new TimeZoneNotFoundException($"{name} is unreachable") : Task.CompletedTask;
            });
    }

    private class OrderedPlugin(string name, List<string> events): ISpecificationPlugin
    {
        public virtual void Register(SpecificationPluginRegistration registration) =>
            registration.CatchUp(_ =>
            {
                events.Add(name);
                return Task.CompletedTask;
            });
    }

    private sealed class DerivedOrderedPlugin(OrderedPlugin origin, string name, List<string> events)
        : OrderedPlugin(name, events), IDerivedPlugin
    {
        public object Origin => origin;
    }

    [Fact]
    public async Task secondPublishingPlugin_throwsNamingBothPlugins()
    {
        await using var api = Api();
        api.Use(new PublishingPlugin((_, _) => Task.CompletedTask));

        var error = Assert.Throws<InvalidOperationException>(() =>
            api.Use(new OtherPublishingPlugin((_, _) => Task.CompletedTask)));

        Assert.Equal(
            "OtherPublishingPlugin can't set the publish function, because PublishingPlugin already did: a specification has one publish function.",
            error.Message);
    }

    [Fact]
    public async Task directPublishingAfterAPlugin_throwsNamingBothSources()
    {
        await using var api = Api();
        api.Use(new PublishingPlugin((_, _) => Task.CompletedTask));

        var error = Assert.Throws<InvalidOperationException>(() => api.Publishing((_, _) => Task.CompletedTask));

        Assert.Equal(
            "a direct Publishing(...) call can't set the publish function, because PublishingPlugin already did: a specification has one publish function.",
            error.Message);
    }

    [Fact]
    public async Task publishingPluginReplacedByAPluginMadeFromIt_publishesThroughTheReplacement()
    {
        await using var api = Api();
        var first = new List<string>();
        var second = new List<string>();
        var original = new ReplaceablePublishingPlugin(first);
        api.Use(original).Use(original.With(second));

        await api.Given().When(Publish("payment")).Then([], Ct);

        Assert.Empty(first);
        Assert.Equal(["payment"], second);
    }

    private sealed class ReplaceablePublishingPlugin(List<string> into, ReplaceablePublishingPlugin? origin = null)
        : IDerivedPlugin
    {
        public object Origin => origin ?? this;

        public ReplaceablePublishingPlugin With(List<string> other) => new(other, (ReplaceablePublishingPlugin)Origin);

        public void Register(SpecificationPluginRegistration registration) =>
            registration.Publishing((request, _) =>
            {
                into.AddRange(request.Messages.Cast<string>());
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task pluginWithServicesOnAnOutOfProcessSpecification_throwsNamingThePlugin()
    {
        await using var api = Api();

        var error = Assert.Throws<InvalidOperationException>(() => api.Use(new ServicesPlugin()));

        Assert.StartsWith("Plugin ServicesPlugin calls Services(...), but decorating the app's services needs ApiSpecification<TProgram>",
            error.Message);
    }

    [Fact]
    public async Task appServices_onAnOutOfProcessSpecification_throwsSayingItNeedsTheGenericSpecification()
    {
        await using var api = Api();
        var plugin = new AppServicesPlugin();
        api.Use(plugin);

        var error = Assert.Throws<InvalidOperationException>(() => plugin.AppServices());

        Assert.StartsWith("AppServices needs ApiSpecification<TProgram>", error.Message);
    }

    [Fact]
    public async Task deadline_isTheSpecificationsDeadline_evenWhenSetAfterThePlugin()
    {
        await using var api = Api();
        var plugin = new DeadlinePlugin();

        await api.Use(plugin).Deadline(TimeSpan.FromSeconds(7)).Given().When(Action(_ => Task.CompletedTask)).Then([], Ct);

        Assert.Equal(TimeSpan.FromSeconds(7), plugin.Seen);
    }

    [Fact]
    public async Task starting_runsOnceAfterTheHostStartsAndBeforeTheFirstStep_inTheOrderAdded()
    {
        await using var api = Api();
        var events = new List<string>();
        api.Use(new StartingPlugin("first", events)).Use(new StartingPlugin("second", events));

        await api.Given(Action(_ =>
            {
                events.Add("step 1");
                return Task.CompletedTask;
            }))
            .When(Action(_ =>
            {
                events.Add("step 2");
                return Task.CompletedTask;
            }))
            .Then([], Ct);
        await api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct);

        Assert.Equal(["first starts", "second starts", "step 1", "step 2"], events);
    }

    [Fact]
    public async Task starting_thatFails_failsTheFirstStepNamingThePlugin_andEveryStepAfterIt()
    {
        await using var api = Api();
        var events = new List<string>();
        api.Use(new StartingPlugin("broker", events, fails: true));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct));
        var again = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct));

        Assert.Equal("When action: plugin StartingPlugin failed to start: broker is unreachable", error.Message);
        Assert.IsType<TimeZoneNotFoundException>(error.InnerException);
        Assert.Same(error, again);
        Assert.Equal(["broker starts"], events);
    }

    [Fact]
    public async Task pluginMadeFromAnother_replacesItInPlace_keepingTheOrderOfTheOthers()
    {
        await using var api = Api();
        var events = new List<string>();
        var original = new OrderedPlugin("original", events);

        api.Use(original)
            .Use(new OrderedPlugin("other", events))
            .Use(new DerivedOrderedPlugin(original, "derived", events));
        await api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct);

        Assert.Equal(["derived", "other"], events);
    }

    [Fact]
    public async Task pluginMadeFromAnotherThatIsUsedFirst_isReplacedByTheOriginalUsedLater()
    {
        await using var api = Api();
        var events = new List<string>();
        var original = new OrderedPlugin("original", events);

        api.Use(new DerivedOrderedPlugin(original, "derived", events)).Use(original);
        await api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct);

        Assert.Equal(["original"], events);
    }
}

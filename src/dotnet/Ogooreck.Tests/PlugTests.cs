using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Assertions;
using Xunit;
using static Ogooreck.API.ApiSpecification;

namespace Ogooreck.Tests;

public class PlugTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ApiSpecification Api() => For(() => Task.FromResult(new Uri("http://localhost")));

    private static Task<PublishRequest> PublishedBy(ApiSpecification api)
    {
        var published = new TaskCompletionSource<PublishRequest>();
        api.Use(new PublishingPlug((request, _) =>
        {
            published.SetResult(request);
            return Task.CompletedTask;
        }));
        return published.Task;
    }

    private sealed class PublishingPlug(Func<PublishRequest, CancellationToken, Task> publish): IPlug
    {
        public void Plug(PlugRegistration registration) => registration.Publishing(publish);
    }

    private sealed class OtherPublishingPlug(Func<PublishRequest, CancellationToken, Task> publish): IPlug
    {
        public void Plug(PlugRegistration registration) => registration.Publishing(publish);
    }

    private sealed class ServicesPlug: IPlug
    {
        public void Plug(PlugRegistration registration) =>
            registration.Services(services => services.AddSingleton("registered"));
    }

    private sealed class DeadlinePlug: IPlug
    {
        public TimeSpan Seen { get; private set; }

        public void Plug(PlugRegistration registration) =>
            registration.CatchUp(_ =>
            {
                Seen = registration.Deadline;
                return Task.CompletedTask;
            });
    }

    private sealed class AppServicesPlug: IPlug
    {
        public Func<IServiceProvider> AppServices { get; private set; } = null!;

        public void Plug(PlugRegistration registration) => AppServices = () => registration.AppServices;
    }

    private sealed class StartingPlug(string name, List<string> events, bool fails = false): IPlug
    {
        public void Plug(PlugRegistration registration) =>
            registration.Starting(_ =>
            {
                events.Add($"{name} starts");
                return fails ? throw new TimeZoneNotFoundException($"{name} is unreachable") : Task.CompletedTask;
            });
    }

    private class OrderedPlug(string name, List<string> events): IPlug
    {
        public virtual void Plug(PlugRegistration registration) =>
            registration.CatchUp(_ =>
            {
                events.Add(name);
                return Task.CompletedTask;
            });
    }

    private sealed class DerivedOrderedPlug(OrderedPlug origin, string name, List<string> events)
        : OrderedPlug(name, events), IDerivedPlug
    {
        public object Origin => origin;
    }

    [Fact]
    public async Task secondPublishingPlug_throwsNamingBothPlugs()
    {
        await using var api = Api();
        api.Use(new PublishingPlug((_, _) => Task.CompletedTask));

        var error = Assert.Throws<InvalidOperationException>(() =>
            api.Use(new OtherPublishingPlug((_, _) => Task.CompletedTask)));

        Assert.Equal(
            "OtherPublishingPlug can't set the publish function, because PublishingPlug already did: a specification has one publish function.",
            error.Message);
    }

    [Fact]
    public async Task directPublishingAfterAPlug_throwsNamingBothSources()
    {
        await using var api = Api();
        api.Use(new PublishingPlug((_, _) => Task.CompletedTask));

        var error = Assert.Throws<InvalidOperationException>(() => api.Publishing((_, _) => Task.CompletedTask));

        Assert.Equal(
            "a direct Publishing(...) call can't set the publish function, because PublishingPlug already did: a specification has one publish function.",
            error.Message);
    }

    [Fact]
    public async Task publishingPlugReplacedByAPlugMadeFromIt_publishesThroughTheReplacement()
    {
        await using var api = Api();
        var first = new List<string>();
        var second = new List<string>();
        var original = new ReplaceablePublishingPlug(first);
        api.Use(original).Use(original.With(second));

        await api.Given().When(Publish("payment")).Then([], Ct);

        Assert.Empty(first);
        Assert.Equal(["payment"], second);
    }

    private sealed class ReplaceablePublishingPlug(List<string> into, ReplaceablePublishingPlug? origin = null)
        : IDerivedPlug
    {
        public object Origin => origin ?? this;

        public ReplaceablePublishingPlug With(List<string> other) => new(other, (ReplaceablePublishingPlug)Origin);

        public void Plug(PlugRegistration registration) =>
            registration.Publishing((request, _) =>
            {
                into.AddRange(request.Messages.Cast<string>());
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task plugWithServicesOnAnOutOfProcessSpecification_throwsNamingThePlug()
    {
        await using var api = Api();

        var error = Assert.Throws<InvalidOperationException>(() => api.Use(new ServicesPlug()));

        Assert.StartsWith("Plug ServicesPlug calls Services(...), but decorating the app's services needs ApiSpecification<TProgram>",
            error.Message);
    }

    [Fact]
    public async Task appServices_onAnOutOfProcessSpecification_throwsSayingItNeedsTheGenericSpecification()
    {
        await using var api = Api();
        var plug = new AppServicesPlug();
        api.Use(plug);

        var error = Assert.Throws<InvalidOperationException>(() => plug.AppServices());

        Assert.StartsWith("AppServices needs ApiSpecification<TProgram>", error.Message);
    }

    [Fact]
    public async Task deadline_isTheSpecificationsDeadline_evenWhenSetAfterThePlug()
    {
        await using var api = Api();
        var plug = new DeadlinePlug();

        await api.Use(plug).Deadline(TimeSpan.FromSeconds(7)).Given().When(Action(_ => Task.CompletedTask)).Then([], Ct);

        Assert.Equal(TimeSpan.FromSeconds(7), plug.Seen);
    }

    [Fact]
    public async Task starting_runsOnceAfterTheHostStartsAndBeforeTheFirstStep_inTheOrderAdded()
    {
        await using var api = Api();
        var events = new List<string>();
        api.Use(new StartingPlug("first", events)).Use(new StartingPlug("second", events));

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
    public async Task starting_thatFails_failsTheFirstStepNamingThePlug_andEveryStepAfterIt()
    {
        await using var api = Api();
        var events = new List<string>();
        api.Use(new StartingPlug("broker", events, fails: true));

        var error = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct));
        var again = await Assert.ThrowsAsync<AssertionException>(() =>
            api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct));

        Assert.Equal("When action: plug StartingPlug failed to start: broker is unreachable", error.Message);
        Assert.IsType<TimeZoneNotFoundException>(error.InnerException);
        Assert.Same(error, again);
        Assert.Equal(["broker starts"], events);
    }

    [Fact]
    public async Task plugMadeFromAnother_replacesItInPlace_keepingTheOrderOfTheOthers()
    {
        await using var api = Api();
        var events = new List<string>();
        var original = new OrderedPlug("original", events);

        api.Use(original)
            .Use(new OrderedPlug("other", events))
            .Use(new DerivedOrderedPlug(original, "derived", events));
        await api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct);

        Assert.Equal(["derived", "other"], events);
    }

    [Fact]
    public async Task plugMadeFromAnotherThatIsUsedFirst_isReplacedByTheOriginalUsedLater()
    {
        await using var api = Api();
        var events = new List<string>();
        var original = new OrderedPlug("original", events);

        api.Use(new DerivedOrderedPlug(original, "derived", events)).Use(original);
        await api.Given().When(Action(_ => Task.CompletedTask)).Then([], Ct);

        Assert.Equal(["original"], events);
    }
}

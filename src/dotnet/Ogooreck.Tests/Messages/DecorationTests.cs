using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Xunit;

namespace Ogooreck.Tests.Messages;

public class DecorationTests
{
    public interface IGreeter
    {
        public string Greet(string name);
    }

    public sealed class Greeter: IGreeter
    {
        public string Greet(string name) => $"Hello {name}";
    }

    public sealed class Loud(IGreeter inner): IGreeter
    {
        public string Greet(string name) => inner.Greet(name).ToUpperInvariant();
    }

    public sealed class GreeterWithDependency(Prefix prefix): IGreeter
    {
        public string Greet(string name) => $"{prefix.Value} {name}";
    }

    public sealed record Prefix(string Value);

    public interface IShop
    {
        public void Close();

        public int Count();

        public List<string> Names();

        public Task OpenAsync(string name);

        public Task<int> PlaceAsync(string order);

        public ValueTask ShipAsync(string order);

        public ValueTask<int> ReturnAsync(string order);

        public Task BreakAsync(string name);
    }

    public sealed class Shop: IShop
    {
        public void Close() => throw new TimeZoneNotFoundException("closed for good");

        public int Count() => 3;

        public List<string> Names() => ["socks"];

        public async Task OpenAsync(string name) => await Task.Yield();

        public async Task<int> PlaceAsync(string order)
        {
            await Task.Yield();
            return order.Length;
        }

        public async ValueTask ShipAsync(string order) => await Task.Yield();

        public async ValueTask<int> ReturnAsync(string order)
        {
            await Task.Yield();
            return order.Length;
        }

        public async Task BreakAsync(string name)
        {
            await Task.Yield();
            throw new TimeZoneNotFoundException("the shop is on fire");
        }
    }

    private static string GreetWith(IServiceCollection services, string name)
    {
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IGreeter>().Greet(name);
    }

    [Fact]
    public void decorate_wrapsAnInstanceAFactoryAndATypeRegistration_keepingTheLifetime()
    {
        var instance = new ServiceCollection().AddSingleton<IGreeter>(new Greeter());
        var factory = new ServiceCollection().AddScoped<IGreeter>(_ => new Greeter());
        var type = new ServiceCollection().AddTransient<IGreeter, Greeter>();

        foreach (var services in new[] { instance, factory, type })
            services.Decorate<IGreeter>(inner => new Loud(inner));

        Assert.Equal("HELLO ANN", GreetWith(instance, "Ann"));
        Assert.Equal("HELLO ANN", GreetWith(factory, "Ann"));
        Assert.Equal("HELLO ANN", GreetWith(type, "Ann"));
        Assert.Equal([ServiceLifetime.Singleton, ServiceLifetime.Scoped, ServiceLifetime.Transient],
            new[] { instance, factory, type }.Select(services => services.Single().Lifetime));
    }

    [Fact]
    public void decorate_scopedService_staysScoped()
    {
        var services = new ServiceCollection().AddScoped<IGreeter, Greeter>();
        services.Decorate<IGreeter>(inner => new Loud(inner));
        using var provider = services.BuildServiceProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.Same(first.ServiceProvider.GetRequiredService<IGreeter>(), first.ServiceProvider.GetRequiredService<IGreeter>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IGreeter>(), second.ServiceProvider.GetRequiredService<IGreeter>());
    }

    [Fact]
    public void decorate_resolvesTheOriginalsDependenciesThroughTheContainer_andPassesTheProviderToTheDecorator()
    {
        var services = new ServiceCollection().AddSingleton(new Prefix("Hi")).AddSingleton<IGreeter, GreeterWithDependency>();
        services.Decorate<IGreeter>((inner, provider) => provider.GetRequiredService<Prefix>().Value == "Hi" ? new Loud(inner) : inner);

        Assert.Equal("HI ANN", GreetWith(services, "Ann"));
    }

    [Fact]
    public void decorate_replacesTheLastRegistration_andLeavesKeyedOnesAlone()
    {
        var services = new ServiceCollection()
            .AddSingleton<IGreeter, Greeter>()
            .AddKeyedSingleton<IGreeter>("later", new Greeter())
            .AddSingleton<IGreeter>(new GreeterWithDependency(new Prefix("Last")));

        services.Decorate<IGreeter>(inner => new Loud(inner));

        Assert.Equal("LAST ANN", GreetWith(services, "Ann"));
        Assert.Equal(typeof(Greeter), services[0].ImplementationType);
        Assert.Equal("later", services[1].ServiceKey);
    }

    [Fact]
    public void decorate_withoutARegistration_failsNamingTheType()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().Decorate<IGreeter>(inner => inner));

        Assert.Equal(
            "Decorate<IGreeter> found no registration of Ogooreck.Tests.Messages.DecorationTests+IGreeter: the app must register it for a plug to decorate it.",
            error.Message);
    }

    [Fact]
    public async Task callRecorder_recordsSuccessfulCallsOnly_andRethrowsTheOriginalException()
    {
        var recorded = new List<string>();
        var shop = CallRecorder.Wrap<IShop>(new Shop(), nameof(IShop.BreakAsync), args => recorded.Add((string)args[0]!));
        var syncRecorded = new List<string>();
        var syncShop = CallRecorder.Wrap<IShop>(new Shop(), nameof(IShop.Close), args => syncRecorded.Add("closed"));

        var asyncError = await Assert.ThrowsAsync<TimeZoneNotFoundException>(() => shop.BreakAsync("socks"));
        var syncError = Assert.Throws<TimeZoneNotFoundException>(syncShop.Close);

        Assert.Equal("the shop is on fire", asyncError.Message);
        Assert.Equal("closed for good", syncError.Message);
        Assert.Empty(recorded);
        Assert.Empty(syncRecorded);
    }

    [Fact]
    public async Task callRecorder_recordsTheArgumentsOfTaskMethodsOnceWhenTheyComplete()
    {
        var recorded = new List<object?[]>();
        var shop = CallRecorder.Wrap<IShop>(new Shop(), nameof(IShop.OpenAsync), recorded.Add);

        await shop.OpenAsync("socks");
        var placed = await CallRecorder.Wrap<IShop>(new Shop(), nameof(IShop.PlaceAsync), recorded.Add).PlaceAsync("order-1");

        Assert.Equal(7, placed);
        Assert.Equal([["socks"], ["order-1"]], recorded);
    }

    [Fact]
    public async Task callRecorder_valueTaskMethods_recordOnSuccessAndAwaitOnce()
    {
        var recorded = new List<object?[]>();

        await CallRecorder.Wrap<IShop>(new Shop(), nameof(IShop.ShipAsync), recorded.Add).ShipAsync("order-1");
        var returned = await CallRecorder.Wrap<IShop>(new Shop(), nameof(IShop.ReturnAsync), recorded.Add).ReturnAsync("order-22");

        Assert.Equal(8, returned);
        Assert.Equal([["order-1"], ["order-22"]], recorded);
    }

    [Fact]
    public void callRecorder_otherMethods_areForwardedUnrecorded_andSynchronousOnesRecordOnReturn()
    {
        var recorded = new List<object?[]>();
        var shop = CallRecorder.Wrap<IShop>(new Shop(), nameof(IShop.Count), recorded.Add);
        var names = CallRecorder.Wrap<IShop>(new Shop(), nameof(IShop.Count), recorded.Add);

        Assert.Equal(3, shop.Count());
        Assert.Equal(["socks"], names.Names());
        Assert.Single(recorded);
    }

    [Fact]
    public void callRecorder_genericNonTaskReturn_isRecordedAsASynchronousCall()
    {
        var recorded = new List<object?[]>();
        var shop = CallRecorder.Wrap<IShop>(new Shop(), nameof(IShop.Names), recorded.Add);

        Assert.Equal(["socks"], shop.Names());
        Assert.Single(recorded);
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace Ogooreck.API;

/// <summary>Decorates the app's own services from a plugin, so recording needs no change to production code.</summary>
/// <example>
/// <code>
/// registration.Services(services =&gt; services.Decorate&lt;IClock&gt;(inner =&gt; new RecordingClock(inner)))
/// </code>
/// </example>
public static class ServiceCollectionDecoration
{
    /// <summary>
    /// Replaces the last non-keyed registration of <typeparamref name="TService"/> with one that resolves the original,
    /// as its instance, its factory or its implementation type, and passes it to <paramref name="decorate"/>. The lifetime stays the same.
    /// When the app doesn't register <typeparamref name="TService"/>, the host fails to start, naming the type.
    /// </summary>
    /// <example>
    /// <code>
    /// services.Decorate&lt;IProducer&lt;string, string&gt;&gt;(inner =&gt; CallRecorder.Wrap(inner, "ProduceAsync", Record))
    /// </code>
    /// </example>
    public static IServiceCollection Decorate<TService>(this IServiceCollection services,
        Func<TService, TService> decorate) where TService : class =>
        services.Decorate<TService>((inner, _) => decorate(inner));

    /// <summary>The same as <see cref="Decorate{TService}(IServiceCollection, Func{TService, TService})"/>, with the app's service provider for the decorator's own dependencies.</summary>
    /// <example>
    /// <code>
    /// services.Decorate&lt;IClock&gt;((inner, provider) =&gt; new RecordingClock(inner, provider.GetRequiredService&lt;Recorded&lt;string&gt;&gt;()))
    /// </code>
    /// </example>
    public static IServiceCollection Decorate<TService>(this IServiceCollection services,
        Func<TService, IServiceProvider, TService> decorate) where TService : class
    {
        var index = services.ToList().FindLastIndex(descriptor =>
            descriptor.ServiceType == typeof(TService) && !descriptor.IsKeyedService);
        if (index < 0)
            throw new InvalidOperationException(
                $"Decorate<{typeof(TService).Name}> found no registration of {typeof(TService).FullName}: the app must register it for a plugin to decorate it.");

        var original = services[index];
        services[index] = new ServiceDescriptor(typeof(TService),
            provider => decorate((TService)Resolve(original, provider), provider), original.Lifetime);
        return services;
    }

    private static object Resolve(ServiceDescriptor original, IServiceProvider provider) =>
        original.ImplementationInstance
        ?? original.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!);
}

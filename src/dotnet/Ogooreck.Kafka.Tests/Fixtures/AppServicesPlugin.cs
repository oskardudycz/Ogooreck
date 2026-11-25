using Ogooreck.API;

namespace Ogooreck.Kafka.Tests.Fixtures;

public sealed class AppServicesPlugin: ISpecificationPlugin
{
    public IServiceProvider Services { get; private set; } = null!;

    public void Register(SpecificationPluginRegistration registration) =>
        registration.Starting(_ =>
        {
            Services = registration.AppServices;
            return Task.CompletedTask;
        });
}

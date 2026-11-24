using Ogooreck.API;

namespace Ogooreck.Kafka.Tests;

public sealed class AppServicesPlug: IPlug
{
    public IServiceProvider Services { get; private set; } = null!;

    public void Plug(PlugRegistration registration) =>
        registration.Starting(_ =>
        {
            Services = registration.AppServices;
            return Task.CompletedTask;
        });
}

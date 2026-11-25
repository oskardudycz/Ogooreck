using Ogooreck.API;

namespace Ogooreck.RabbitMQ.Tests.Fixtures;

public sealed class AppServices: ISpecificationPlugin
{
    private SpecificationPluginRegistration? registration;

    public IServiceProvider Provider =>
        (registration ?? throw new InvalidOperationException("The host has not started.")).AppServices;

    public void Register(SpecificationPluginRegistration pluginRegistration) => registration = pluginRegistration;
}

using Ogooreck.API;

namespace Ogooreck.RabbitMQ.Tests;

public sealed class AppServices: IPlug
{
    private PlugRegistration? registration;

    public IServiceProvider Provider =>
        (registration ?? throw new InvalidOperationException("The host has not started.")).AppServices;

    public void Plug(PlugRegistration plugRegistration) => registration = plugRegistration;
}

using Ogooreck.API;
using Wolverine;

namespace Ogooreck.Wolverine.Tests;

public sealed class SampleApp: IPlug
{
    private PlugRegistration? registration;

    public IServiceProvider Services => (registration ?? throw new InvalidOperationException("The host has not started.")).AppServices;

    public void Plug(PlugRegistration plugRegistration)
    {
        registration = plugRegistration;
        plugRegistration.Services(services =>
        {
            PublishedMessagesMiddleware.RegisterIn(services);
            services.ConfigureWolverine(options => options.Discovery.IncludeType(typeof(ShipOnOrderPlaced)));
        });
    }
}

using Ogooreck.API;
using Wolverine;

namespace Ogooreck.Wolverine.Tests.Fixtures;

public sealed class SampleApp: ISpecificationPlugin
{
    private SpecificationPluginRegistration? registration;

    public IServiceProvider Services => (registration ?? throw new InvalidOperationException("The host has not started.")).AppServices;

    public void Register(SpecificationPluginRegistration pluginRegistration)
    {
        registration = pluginRegistration;
        pluginRegistration.Services(services =>
        {
            PublishedMessagesMiddleware.RegisterIn(services);
            services.ConfigureWolverine(options => options.Discovery.IncludeType(typeof(ShipOnOrderPlaced)));
        });
    }
}

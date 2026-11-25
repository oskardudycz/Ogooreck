using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Sample.Kafka.Shipping;

namespace Ogooreck.Kafka.Tests.Fixtures;

public sealed class SharedFaultsPlugin(ShippingFaults faults): ISpecificationPlugin
{
    public void Register(SpecificationPluginRegistration registration) =>
        registration.Services(services => services.AddSingleton(faults));
}

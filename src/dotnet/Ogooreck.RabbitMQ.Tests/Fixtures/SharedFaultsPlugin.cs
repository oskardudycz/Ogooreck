using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Sample.RabbitMQ.Shipping;

namespace Ogooreck.RabbitMQ.Tests.Fixtures;

public sealed class SharedFaultsPlugin(ShippingFaults faults): ISpecificationPlugin
{
    public void Register(SpecificationPluginRegistration registration) =>
        registration.Services(services => services.AddSingleton(faults));
}

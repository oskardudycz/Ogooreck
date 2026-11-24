using Microsoft.Extensions.DependencyInjection;
using Ogooreck.API;
using Ogooreck.Sample.RabbitMQ;

namespace Ogooreck.RabbitMQ.Tests;

public sealed class SharedFaultsPlug(ShippingFaults faults): IPlug
{
    public void Plug(PlugRegistration registration) =>
        registration.Services(services => services.AddSingleton(faults));
}

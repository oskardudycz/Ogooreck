using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ogooreck.API;

namespace Ogooreck.Kafka.Tests;

public sealed class WithoutServicePlug<TService>: IPlug where TService : class
{
    public void Plug(PlugRegistration registration) => registration.Services(services => services.RemoveAll<TService>());
}

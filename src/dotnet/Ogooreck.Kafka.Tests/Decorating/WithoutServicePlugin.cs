using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ogooreck.API;

namespace Ogooreck.Kafka.Tests.Decorating;

public sealed class WithoutServicePlugin<TService>: ISpecificationPlugin where TService : class
{
    public void Register(SpecificationPluginRegistration registration) => registration.Services(services => services.RemoveAll<TService>());
}

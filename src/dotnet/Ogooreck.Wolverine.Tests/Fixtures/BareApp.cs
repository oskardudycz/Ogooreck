using Ogooreck.API;

namespace Ogooreck.Wolverine.Tests.Fixtures;

public sealed class BareApp: ApiSpecification<Program>
{
    private readonly ServicesOfTheApp services = new();

    public BareApp() : base(spec => spec.Use(((BareApp)spec).services)) { }

    public IServiceProvider Services => services.Provider;

    private sealed class ServicesOfTheApp: ISpecificationPlugin
    {
        private SpecificationPluginRegistration? registration;

        public IServiceProvider Provider =>
            (registration ?? throw new InvalidOperationException("The host has not started.")).AppServices;

        public void Register(SpecificationPluginRegistration pluginRegistration) => registration = pluginRegistration;
    }
}

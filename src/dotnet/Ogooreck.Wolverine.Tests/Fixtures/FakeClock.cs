using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Ogooreck.API;

namespace Ogooreck.Wolverine.Tests.Fixtures;

#region FakeClockPlugin

public sealed class FakeClock: ISpecificationPlugin
{
    private readonly FakeTimeProvider time = new();

    public void Advance(TimeSpan by) => time.Advance(by);

    public void Register(SpecificationPluginRegistration registration) =>
        registration.Services(services =>
            services.AddSingleton<TimeProvider>(time));
}

#endregion

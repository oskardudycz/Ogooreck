using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Ogooreck.API;

namespace Ogooreck.Wolverine.Tests;

public sealed class FakeClock: IPlug
{
    private readonly FakeTimeProvider time = new();

    public void Advance(TimeSpan by) => time.Advance(by);

    public void Plug(PlugRegistration registration) =>
        registration.Services(services => services.AddSingleton<TimeProvider>(time));
}

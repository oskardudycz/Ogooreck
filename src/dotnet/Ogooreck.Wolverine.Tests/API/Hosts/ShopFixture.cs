using Ogooreck.API;
using Ogooreck.Wolverine.Tests.Fixtures;

namespace Ogooreck.Wolverine.Tests.API.Hosts;

public sealed class ShopFixture: IAsyncDisposable
{
    public ShopFixture() =>
        Spec = ApiSpecification.For<Program>().Use(Messaging.Wolverine).Use(new SampleApp());

    public ApiSpecification<Program> Spec { get; }

    public ValueTask DisposeAsync() => Spec.DisposeAsync();
}

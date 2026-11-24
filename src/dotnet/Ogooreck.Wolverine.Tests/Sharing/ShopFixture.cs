using Ogooreck.API;

namespace Ogooreck.Wolverine.Tests.Sharing;

public sealed class ShopFixture: IAsyncDisposable
{
    public ShopFixture() =>
        Spec = ApiSpecification.For<Program>().Use(Messaging.Wolverine).Use(new SampleApp());

    public ApiSpecification<Program> Spec { get; }

    public ValueTask DisposeAsync() => Spec.DisposeAsync();
}

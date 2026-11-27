using Ogooreck.API;

namespace Ogooreck.Wolverine.Tests.Fixtures;

#region WolverineShopApi

public class ShopApi: ApiSpecification<Program>
{
    public ShopApi()
        : base(spec => spec
            .Use(Messaging.Wolverine)
            .Use(((ShopApi)spec).AppClock))
    { }

    public FakeClock AppClock { get; } = new();
}

#endregion

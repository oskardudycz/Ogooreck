using Ogooreck.BusinessLogic;

namespace Ogooreck.BusinessLogic.Tests.Deciders;

public class BasketTests
{
    private readonly DeciderSpecification<Basket> Spec = Specification.For<Basket>(
        (command, basket) => command switch
        {
            AddToBasket add => DecideResult.For<object, Basket>(basket with { Products = [.. basket.Products, add.Product] }),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
        });

    [Fact]
    public void DeciderThen_StillComparesNestedCollectionsWithoutOrder() =>
        Spec.Given(new Basket(["socks"]))
            .When(new AddToBasket("shoes"))
            .Then(new Basket(["shoes", "socks"]));

    public record Basket(string[] Products);

    public record AddToBasket(string Product);
}

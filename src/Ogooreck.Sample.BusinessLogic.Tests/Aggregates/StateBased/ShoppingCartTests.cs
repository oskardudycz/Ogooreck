using FluentAssertions;
using Ogooreck.BusinessLogic;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased.Pricing;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased.Products;

namespace Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased;

using static ShoppingCartEventsBuilder;
using static ProductItemBuilder;

public class ShoppingCartTests
{
    private readonly Random random = new();

    private readonly HandlerSpecification<ShoppingCart> Spec = Specification.For<ShoppingCart>();

    private class DummyProductPriceCalculator: IProductPriceCalculator
    {
        private readonly decimal price;

        public DummyProductPriceCalculator(decimal price) => this.price = price;

        public IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems) =>
            productItems.Select(pi => PricedProductItem.For(pi, price)).ToList();
    }

    [Fact]
    public void GivenNonExistingShoppingCart_WhenOpenWithValidParams_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        Spec.Given()
            .When(() => ShoppingCart.Open(shoppingCartId, clientId))
            .Then((state, _) =>
            {
                state.Id.Should().Be(shoppingCartId);
                state.ClientId.Should().Be(clientId);
                state.ProductItems.Should().BeEmpty();
                state.Status.Should().Be(ShoppingCartStatus.Pending);
                state.TotalPrice.Should().Be(0);
            });
    }

    [Fact]
    public void GivenOpenShoppingCart_WhenAddProductWithValidParams_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();

        var productItem = ValidProductItem();
        var price = random.Next(1, 1000);
        var priceCalculator = new DummyProductPriceCalculator(price);

        Spec.Given(OpenedShoppingCart(shoppingCartId))
            .When(cart => cart.AddProduct(priceCalculator, productItem))
            .Then((state, _) =>
            {
                state.ProductItems.Should().NotBeEmpty();
                state.ProductItems.Single().Should().Be(PricedProductItem.For(productItem, price));
            });
    }
}

public static class ShoppingCartEventsBuilder
{
    public static ShoppingCart OpenedShoppingCart(Guid shoppingCartId)
    {
        var clientId = Guid.NewGuid();

        return ShoppingCart.Open(shoppingCartId, clientId);
    }
}

public static class ProductItemBuilder
{
    private static readonly Random Random = new();

    public static ProductItem ValidProductItem() =>
        ProductItem.From(Guid.NewGuid(), Random.Next(1, 100));
}

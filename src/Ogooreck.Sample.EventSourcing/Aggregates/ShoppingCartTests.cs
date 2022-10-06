using Ogooreck.BusinessLogic;
using Ogooreck.Sample.EventSourcing.Aggregates.Core;
using Ogooreck.Sample.EventSourcing.Aggregates.Pricing;
using Ogooreck.Sample.EventSourcing.Aggregates.Products;

namespace Ogooreck.Sample.EventSourcing.Aggregates;

using static BusinessLogicSpecification;
using static AggregateTestExtensions;

public class ShoppingCartTests
{
    private readonly Random random = new();

    private readonly Func<ShoppingCart, object, ShoppingCart> evolve =
        (cart, @event) =>
        {
            switch (@event)
            {
                case ShoppingCartOpened opened:
                    cart.Apply(opened);
                    break;
                case ProductAdded productAdded:
                    cart.Apply(productAdded);
                    break;
                case ProductRemoved productRemoved:
                    cart.Apply(productRemoved);
                    break;
                case ShoppingCartConfirmed confirmed:
                    cart.Apply(confirmed);
                    break;
                case ShoppingCartCanceled canceled:
                    cart.Apply(canceled);
                    break;
            }

            return cart;
        };

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

        Given<ShoppingCart>()
            .When(() => ShoppingCart.Open(shoppingCartId, clientId))
            .Then(EVENT(new ShoppingCartOpened(shoppingCartId, clientId)));
    }


    [Fact]
    public void GivenOpenShoppingCart_WhenAddProductWithValidParams_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var productItem = ProductItem.From(Guid.NewGuid(), random.Next(100));
        var price = random.Next(1000);
        var priceCalculator = new DummyProductPriceCalculator(price);

        Given(evolve, () => new ShoppingCartOpened(shoppingCartId, clientId))
            .When(cart => cart.AddProduct(priceCalculator, productItem))
            .Then(EVENT(new ProductAdded(shoppingCartId, PricedProductItem.For(productItem, price))));
    }
}

public static class AggregateTestExtensions
{
    public static WhenBusinessLogicSpecificationBuilder<TAggregate, object> When<TAggregate>(
        this GivenBusinessLogicSpecificationBuilder<TAggregate, object> builder,
        Func<TAggregate> perform
    ) where TAggregate : Aggregate =>
        builder.When(_ =>
        {
            var aggregate = perform();
            return (aggregate, aggregate.DequeueUncommittedEvents());
        });

    public static WhenBusinessLogicSpecificationBuilder<TAggregate, object> When<TAggregate>(
        this GivenBusinessLogicSpecificationBuilder<TAggregate, object> builder,
        Action<TAggregate> perform
    ) where TAggregate : Aggregate =>
        builder.When(aggregate =>
        {
            perform(aggregate);
            return (aggregate, aggregate.DequeueUncommittedEvents());
        });
}

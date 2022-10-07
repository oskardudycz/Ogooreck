using Ogooreck.BusinessLogic;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced.Core;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced.Pricing;
using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced.Products;
using Ogooreck.Sample.BusinessLogic.Tests.Functions.EventSourced;

namespace Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced;

using static ShoppingCartEventsBuilder;
using static ProductItemBuilder;
using static AggregateTestExtensions<ShoppingCart>;

public class ShoppingCartTests
{
    private readonly Random random = new();

    private static readonly Func<ShoppingCart, object, ShoppingCart> evolve =
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

    private readonly HandlerSpecification<ShoppingCart> Spec = Specification.For(Handle, evolve);

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
            .Then(new ShoppingCartOpened(shoppingCartId, clientId));
    }

    [Fact]
    public void GivenOpenShoppingCart_WhenAddProductWithValidParams_ThenSucceeds()
    {
        var shoppingCartId = Guid.NewGuid();

        var productItem = ValidProductItem();
        var price = random.Next(1, 1000);
        var priceCalculator = new DummyProductPriceCalculator(price);

        Spec.Given(ShoppingCartOpened(shoppingCartId))
            .When(cart => cart.AddProduct(priceCalculator, productItem))
            .Then(new ProductAdded(shoppingCartId, PricedProductItem.For(productItem, price)));
    }
}

public static class ShoppingCartEventsBuilder
{
    public static ShoppingCartOpened ShoppingCartOpened(Guid shoppingCartId)
    {
        var clientId = Guid.NewGuid();

        return new ShoppingCartOpened(shoppingCartId, clientId);
    }
}

public static class ProductItemBuilder
{
    private static readonly Random Random = new();

    public static ProductItem ValidProductItem() =>
        ProductItem.From(Guid.NewGuid(), Random.Next(1, 100));
}

public static class AggregateTestExtensions<TAggregate> where TAggregate : Aggregate
{
    public static DecideResult<object, TAggregate> Handle(Handler<object, TAggregate> handle, TAggregate aggregate)
    {
        var result = handle(aggregate);
        var updatedAggregate = result.CurrentState ?? aggregate;
        return DecideResult.For(updatedAggregate, updatedAggregate.DequeueUncommittedEvents());
    }
}
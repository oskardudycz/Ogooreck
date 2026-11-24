using Ogooreck.BusinessLogic.Tests.Aggregates.EventSourced.Products;

namespace Ogooreck.BusinessLogic.Tests.Aggregates.EventSourced.Pricing;

public interface IProductPriceCalculator
{
    public IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

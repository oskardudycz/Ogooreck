using Ogooreck.Sample.BusinessLogic.Aggregates.EventSourced.Products;

namespace Ogooreck.Sample.BusinessLogic.Aggregates.EventSourced.Pricing;

public interface IProductPriceCalculator
{
    IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

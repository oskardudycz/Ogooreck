using Ogooreck.Sample.BusinessLogic.Aggregates.StateBased.Products;

namespace Ogooreck.Sample.BusinessLogic.Aggregates.StateBased.Pricing;

public interface IProductPriceCalculator
{
    IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

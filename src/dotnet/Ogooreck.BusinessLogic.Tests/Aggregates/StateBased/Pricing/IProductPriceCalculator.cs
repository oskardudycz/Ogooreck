using Ogooreck.BusinessLogic.Tests.Aggregates.StateBased.Products;

namespace Ogooreck.BusinessLogic.Tests.Aggregates.StateBased.Pricing;

public interface IProductPriceCalculator
{
    public IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased.Products;

namespace Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased.Pricing;

public interface IProductPriceCalculator
{
    IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased.Products;

namespace Ogooreck.Sample.BusinessLogic.Tests.Aggregates.StateBased.Pricing;

public interface IProductPriceCalculator
{
    public IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

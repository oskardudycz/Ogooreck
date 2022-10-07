using Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced.Products;

namespace Ogooreck.Sample.BusinessLogic.Tests.Aggregates.EventSourced.Pricing;

public interface IProductPriceCalculator
{
    IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

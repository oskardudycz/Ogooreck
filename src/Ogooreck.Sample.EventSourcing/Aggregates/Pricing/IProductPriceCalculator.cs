using Ogooreck.Sample.EventSourcing.Aggregates.Products;

namespace Ogooreck.Sample.EventSourcing.Aggregates.Pricing;

public interface IProductPriceCalculator
{
    IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

using Ogooreck.Sample.EventSourcing.Aggregates.StateBased.Products;

namespace Ogooreck.Sample.EventSourcing.Aggregates.StateBased.Pricing;

public interface IProductPriceCalculator
{
    IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

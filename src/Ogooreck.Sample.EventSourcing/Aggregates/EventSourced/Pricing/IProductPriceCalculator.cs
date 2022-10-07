using Ogooreck.Sample.EventSourcing.Aggregates.EventSourced.Products;

namespace Ogooreck.Sample.EventSourcing.Aggregates.EventSourced.Pricing;

public interface IProductPriceCalculator
{
    IReadOnlyList<PricedProductItem> Calculate(params ProductItem[] productItems);
}

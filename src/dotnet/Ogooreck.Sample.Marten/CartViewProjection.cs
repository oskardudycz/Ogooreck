using Marten.Events.Aggregation;
using Ogooreck.Sample.Marten.Events;

namespace Ogooreck.Sample.Marten;

public class CartViewProjection: SingleStreamProjection<CartView, Guid>
{
    public CartView Create(ProductAdded added) => new() { Id = added.CartId, Products = [added.Product] };

    public void Apply(ProductAdded added, CartView view) => view.Products.Add(added.Product);
}

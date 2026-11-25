using Ogooreck.Sample.Wolverine.Events;

namespace Ogooreck.Sample.Wolverine.Carts.GettingCart;

public static class CartViewHandler
{
    public static async Task Handle(ProductAdded productAdded, CartViews views)
    {
        await Task.Yield();
        views.Apply(productAdded.CartId, productAdded.Product);
    }
}

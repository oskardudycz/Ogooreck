using Ogooreck.Sample.EventSourcing.Aggregates.EventSourced.Products;

namespace Ogooreck.Sample.EventSourcing.Aggregates.EventSourced;

public record ShoppingCartOpened(
    Guid CartId,
    Guid ClientId
)
{
    public static ShoppingCartOpened Create(Guid cartId, Guid clientId)
    {
        if (cartId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(cartId));
        if (clientId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(clientId));

        return new ShoppingCartOpened(cartId, clientId);
    }
}


public record ProductAdded(
    Guid CartId,
    PricedProductItem ProductItem
)
{
    public static ProductAdded Create(Guid cartId, PricedProductItem productItem)
    {
        if (cartId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(cartId));

        return new ProductAdded(cartId, productItem);
    }
}

public record ProductRemoved(
    Guid CartId,
    PricedProductItem ProductItem
)
{
    public static ProductRemoved Create(Guid cartId, PricedProductItem productItem)
    {
        if (cartId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(cartId));

        return new ProductRemoved(cartId, productItem);
    }
}

public record ShoppingCartConfirmed(
    Guid CartId,
    DateTime ConfirmedAt
)
{
    public static ShoppingCartConfirmed Create(Guid cartId, DateTime confirmedAt)
    {
        if (cartId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(cartId));
        if (confirmedAt == default)
            throw new ArgumentOutOfRangeException(nameof(confirmedAt));

        return new ShoppingCartConfirmed(cartId, confirmedAt);
    }
}

public record ShoppingCartCanceled(
    Guid CartId,
    DateTime CanceledAt
)
{
    public static ShoppingCartCanceled Create(Guid cartId, DateTime canceledAt)
    {
        if (cartId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(cartId));
        if (canceledAt == default)
            throw new ArgumentOutOfRangeException(nameof(canceledAt));

        return new ShoppingCartCanceled(cartId, canceledAt);
    }
}

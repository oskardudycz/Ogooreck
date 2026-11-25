using Ogooreck.Sample.Wolverine.Events;

namespace Ogooreck.Sample.Wolverine.Carts.Expiring;

public record ExpireCart(Guid CartId);

public static class ExpireCartHandler
{
    public static CartExpired Handle(ExpireCart command) => new(command.CartId);
}

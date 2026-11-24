using Ogooreck.Sample.Wolverine.Events;

namespace Ogooreck.Sample.Wolverine;

public static class CartHandler
{
    public static CartExpired Handle(ExpireCart command) => new(command.CartId);

    public static OrderPaid Handle(PaymentConfirmed payment) => new(payment.OrderId);
}

using Ogooreck.Sample.Wolverine.Events;

namespace Ogooreck.Sample.Wolverine.Orders.Paying;

public record PaymentConfirmed(Guid OrderId);

public static class PaymentConfirmedHandler
{
    public static OrderPaid Handle(PaymentConfirmed payment) => new(payment.OrderId);
}

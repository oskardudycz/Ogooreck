using Ogooreck.Sample.Wolverine.Events;

namespace Ogooreck.Sample.Wolverine.Notifications;

public static class NotificationsHandler
{
    public static void Handle(OrderPlaced orderPlaced) { }

    public static void Handle(CartClosed cartClosed) { }

    public static void Handle(StockChecked stockChecked) { }

    public static void Handle(CartExpired cartExpired) { }

    public static void Handle(OrderPaid orderPaid) { }

    public static void Handle(LateArrived lateArrived) { }

    public static void Handle(OrderViewed orderViewed) { }

    public static void Handle(ShipOrder shipOrder) { }
}

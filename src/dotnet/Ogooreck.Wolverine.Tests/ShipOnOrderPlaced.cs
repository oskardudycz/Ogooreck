using System.Collections.Concurrent;
using Ogooreck.Sample.Wolverine.Events;
using Wolverine;

namespace Ogooreck.Wolverine.Tests;

public static class ShipOnOrderPlaced
{
    private static readonly ConcurrentDictionary<Guid, bool> ShippedCarts = new();

    public static void ShipsOrdersOf(Guid cartId) => ShippedCarts[cartId] = true;

    public static OutgoingMessages Handle(OrderPlaced orderPlaced)
    {
        var messages = new OutgoingMessages();
        if (ShippedCarts.ContainsKey(orderPlaced.CartId))
            messages.Add(new ShipOrder(orderPlaced.CartId));
        return messages;
    }
}

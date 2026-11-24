using Ogooreck.Sample.Wolverine.Events;
using Ogooreck.Wolverine;

namespace Ogooreck.Wolverine.Tests;

public static class Messaging
{
    public static readonly WolverinePlug Wolverine =
        new WolverinePlug().MessageTypes(type => type.Namespace == typeof(OrderPlaced).Namespace);
}

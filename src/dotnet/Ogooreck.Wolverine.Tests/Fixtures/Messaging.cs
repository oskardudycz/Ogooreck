using Ogooreck.Sample.Wolverine.Events;
using Ogooreck.Wolverine;

namespace Ogooreck.Wolverine.Tests.Fixtures;

public static class Messaging
{
    public static readonly WolverinePlugin Wolverine =
        new WolverinePlugin().MessageTypes(type => type.Namespace == typeof(OrderPlaced).Namespace);
}

using Ogooreck.Marten;

namespace Ogooreck.Marten.Tests.Fixtures;

#region MartenPlugin

public static class EventStore
{
    public static readonly MartenPlugin Marten = new();
}

#endregion

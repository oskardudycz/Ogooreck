using System.Diagnostics;

namespace Ogooreck.Sample.Marten.Core;

public static class Tracing
{
    public static string CurrentTraceparent() =>
        Activity.Current?.Id ?? throw new InvalidOperationException("The request has no trace context.");
}

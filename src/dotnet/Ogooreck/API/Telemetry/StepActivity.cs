using System.Diagnostics;

namespace Ogooreck.API;

internal static class StepActivity
{
    public static Activity Start(string kind, string traceparent) =>
        new Activity($"Ogooreck {kind}").SetParentId(traceparent).Start();
}

using System.Diagnostics;

namespace Ogooreck.Mcp;

internal static class McpTracing
{
    public static readonly ActivitySource Source = new("Ogooreck.Mcp");

    static McpTracing() => ActivitySource.AddActivityListener(new ActivityListener
    {
        ShouldListenTo = source => source == Source,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
    });
}

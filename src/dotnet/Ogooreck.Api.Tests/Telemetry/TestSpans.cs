using System.Diagnostics;

namespace Ogooreck.Api.Tests.Telemetry;

public static class TestSpans
{
    public const string MessageId = "messaging.message.id";
    public const string CorrelationId = "messaging.message.correlation_id";
    public const string CausationId = "messaging.message.causation_id";

    private static readonly ActivitySource Handlers = new("Ogooreck.Api.Tests");

    public static Task Emit(params (string Name, (string Key, string Value)[] Attributes)[] spans)
    {
        foreach (var (name, attributes) in spans)
        {
            using var span = Handlers.StartActivity(name);
            foreach (var (key, value) in attributes)
                span?.SetTag(key, value);
        }

        return Task.CompletedTask;
    }

    public static void EmitBelow(string traceparent, string name, params (string Key, string Value)[] attributes)
    {
        using var span = Handlers.StartActivity(name, ActivityKind.Consumer, ActivityContext.Parse(traceparent, null));
        foreach (var (key, value) in attributes)
            span?.SetTag(key, value);
    }
}

namespace Ogooreck.Aspire.Tests.Fixtures;

internal static class TestRunCollector
{
    public static Uri? Endpoint =>
        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") is { Length: > 0 } endpoint
            ? new Uri(endpoint)
            : null;
}

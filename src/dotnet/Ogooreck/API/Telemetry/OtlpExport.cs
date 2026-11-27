using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ogooreck.API;

internal static class OtlpExport
{
    private static readonly Dictionary<string, string?> Defaults = new()
    {
        ["OTEL_BSP_SCHEDULE_DELAY"] = "50",
        ["OTEL_BSP_MAX_QUEUE_SIZE"] = "65536",
        ["OTEL_METRIC_EXPORT_INTERVAL"] = "1000"
    };

    public static void Start(IConfiguration configuration, Action<EventHandler> atExit)
    {
        if (!IsOn(configuration))
            return;

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(Defaults)
            .AddConfiguration(configuration)
            .Build());
        services.AddOpenTelemetry()
            .ConfigureResource(Resource)
            .WithTracing(tracing => tracing.AddSource("*"))
            .WithMetrics(metrics => metrics.AddMeter("Ogooreck"))
            .UseOtlpExporter();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TracerProvider>();
        provider.GetRequiredService<MeterProvider>();
        atExit((_, _) => provider.Dispose());
    }

    private static bool IsOn(IConfiguration configuration) =>
        new[] { "OTEL_EXPORTER_OTLP_ENDPOINT", "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT" }
            .Any(endpoint => !string.IsNullOrEmpty(configuration[endpoint])) &&
        !string.Equals(configuration["OGOORECK_OTLP_EXPORT"], "false", StringComparison.OrdinalIgnoreCase);

    public static Uri? ForwardTo(IConfiguration configuration) =>
        IsOn(configuration) && configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is { Length: > 0 } endpoint &&
        string.Equals(configuration["OTEL_EXPORTER_OTLP_PROTOCOL"], "http/protobuf", StringComparison.OrdinalIgnoreCase)
            ? new Uri(endpoint)
            : null;

    public static string AppSdkDisabled(IConfiguration configuration) =>
        IsOn(configuration) ? "true" : configuration["OTEL_SDK_DISABLED"] ?? "false";

    private static void Resource(ResourceBuilder resource) =>
        resource.Clear().AddService(TestProject()).AddEnvironmentVariableDetector();

    private static string TestProject() =>
        Path.GetFileName(((string)AppContext.GetData("APP_CONTEXT_DEPS_FILES")!).Split(';')[0])
            .Replace(".deps.json", "", StringComparison.Ordinal);
}

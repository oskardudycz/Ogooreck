using System.Runtime.CompilerServices;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ogooreck.Testing;

internal static class TestTelemetryExport
{
    private static TracerProvider? traces;
    private static MeterProvider? metrics;

    [ModuleInitializer]
    internal static void StartWhenAnEndpointIsSet()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
            return;

        var resource = ResourceBuilder.CreateDefault().AddService(typeof(TestTelemetryExport).Assembly.GetName().Name!);
        traces = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource("*")
            .AddOtlpExporter(options => options.ExportProcessorType = ExportProcessorType.Simple)
            .Build();
        metrics = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resource)
            .AddMeter("Ogooreck")
            .AddOtlpExporter((_, reader) => reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000)
            .Build();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            metrics?.Dispose();
            traces?.Dispose();
        };
    }
}

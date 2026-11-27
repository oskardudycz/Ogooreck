namespace Ogooreck.Testing

open System
open OpenTelemetry
open OpenTelemetry.Metrics
open OpenTelemetry.Resources
open OpenTelemetry.Trace

type TestTelemetryExport() =
    let exporting =
        not (
            String.IsNullOrEmpty(
                Environment.GetEnvironmentVariable "OTEL_EXPORTER_OTLP_ENDPOINT"
            )
        )

    let resource () =
        ResourceBuilder
            .CreateDefault()
            .AddService(
                nonNull (typeof<TestTelemetryExport>.Assembly.GetName().Name)
            )

    let traces =
        if exporting then
            Some(
                Sdk
                    .CreateTracerProviderBuilder()
                    .SetResourceBuilder(resource ())
                    .AddSource("*")
                    .AddOtlpExporter(fun options ->
                        options.ExportProcessorType <-
                            ExportProcessorType.Batch

                        options.BatchExportProcessorOptions.ScheduledDelayMilliseconds <-
                            50

                        options.BatchExportProcessorOptions.MaxQueueSize <-
                            65536)
                    .Build()
            )
        else
            None

    let metrics =
        if exporting then
            Some(
                Sdk
                    .CreateMeterProviderBuilder()
                    .SetResourceBuilder(resource ())
                    .AddMeter("Ogooreck")
                    .AddOtlpExporter(fun _ reader ->
                        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds <-
                            1000)
                    .Build()
            )
        else
            None

    interface IDisposable with
        member _.Dispose() =
            metrics |> Option.iter _.Dispose()
            traces |> Option.iter _.Dispose()

module TestTelemetryExportFixture =
    [<assembly: Xunit.AssemblyFixture(typeof<TestTelemetryExport>)>]
    do ()

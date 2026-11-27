using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Ogooreck.API;
using Xunit;

namespace Ogooreck.Tests.API.Telemetry;

public class OtlpExportTests
{
    private readonly ActivitySource source = new($"Ogooreck.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task Start_WithAnOtlpEndpoint_ExportsEverySpanAndOgooreckMetrics_WhenTheProcessExits()
    {
        await using var collector = OtlpReceiver.Start(new OtlpReceiverOptions { ForwardTo = null });
        var (spans, measurements) = Attached(collector);

        var exit = Started(("OTEL_EXPORTER_OTLP_ENDPOINT", collector.Endpoint.ToString().TrimEnd('/')));
        Record();
        exit!(null, EventArgs.Empty);

        Assert.Contains(spans.Items(), span => span.Name == source.Name);
        Assert.Contains(measurements.Items(), measurement => measurement.Instrument == "ogooreck.step.duration");
    }

    [Fact]
    public async Task Start_WithOnlyTheTracesAndMetricsEndpoints_ExportsEverySpanAndOgooreckMetrics_WhenTheProcessExits()
    {
        await using var collector = OtlpReceiver.Start(new OtlpReceiverOptions { ForwardTo = null });
        var (spans, measurements) = Attached(collector);
        var endpoint = collector.Endpoint.ToString().TrimEnd('/');

        var exit = Started(("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", $"{endpoint}/v1/traces"),
            ("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", $"{endpoint}/v1/metrics"));
        Record();
        exit!(null, EventArgs.Empty);

        Assert.Contains(spans.Items(), span => span.Name == source.Name);
        Assert.Contains(measurements.Items(), measurement => measurement.Instrument == "ogooreck.step.duration");
    }

    [Fact]
    public void Start_WithoutAnOtlpEndpoint_ExportsNothing() =>
        Assert.Null(Started());

    [Fact]
    public void Start_TurnedOff_ExportsNothing() =>
        Assert.Null(Started(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4318"), ("OGOORECK_OTLP_EXPORT", "false")));

    [Fact]
    public void AppSdkDisabled_WhileOgooreckExports_TurnsTheAppsOwnExportOff() =>
        Assert.Equal("true", OtlpExport.AppSdkDisabled(Configuration(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4318"))));

    [Fact]
    public void AppSdkDisabled_WithoutOgooreckExport_KeepsTheAppsOwnSetting() =>
        Assert.Equal("true", OtlpExport.AppSdkDisabled(Configuration(("OTEL_SDK_DISABLED", "true"))));

    [Fact]
    public void AppSdkDisabled_WithoutOgooreckExportOrAnAppSetting_LeavesTheAppsExportOn() =>
        Assert.Equal("false", OtlpExport.AppSdkDisabled(Configuration()));

    [Fact]
    public void ForwardTo_WhileOgooreckExportsOverHttp_IsTheEndpointTheTestsExportTo() =>
        Assert.Equal(new Uri("http://127.0.0.1:4318"), OtlpExport.ForwardTo(Configuration(
            ("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4318"), ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"))));

    [Fact]
    public void ForwardTo_AnEndpointOverGrpc_IsNothing() =>
        Assert.Null(OtlpExport.ForwardTo(Configuration(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4317"))));

    [Fact]
    public void ForwardTo_WithOnlyTheTracesEndpoint_IsNothing() =>
        Assert.Null(OtlpExport.ForwardTo(Configuration(
            ("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", "http://127.0.0.1:4318/v1/traces"), ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"))));

    [Fact]
    public void ForwardTo_WithOgooreckExportTurnedOff_IsNothing() =>
        Assert.Null(OtlpExport.ForwardTo(Configuration(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:4318"),
            ("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"), ("OGOORECK_OTLP_EXPORT", "false"))));

    private static IConfiguration Configuration(params (string Key, string Value)[] variables) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(variables.Select(variable => new KeyValuePair<string, string?>(variable.Key, variable.Value)))
            .Build();

    private static EventHandler? Started(params (string Key, string Value)[] variables)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(variables.Select(variable => new KeyValuePair<string, string?>(variable.Key, variable.Value))
                .Append(new("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")))
            .Build();
        EventHandler? exit = null;
        OtlpExport.Start(configuration, handler => exit = handler);
        return exit;
    }

    private void Record()
    {
        source.StartActivity(source.Name)!.Dispose();
        Diagnostics.Steps.Record(1);
    }

    private static (Recorded<RecordedSpan> Spans, Recorded<RecordedMeasurement> Measurements) Attached(OtlpReceiver receiver)
    {
        var (spans, measurements) = (new Recorded<RecordedSpan>(), new Recorded<RecordedMeasurement>());
        receiver.Attach(spans, measurements);
        return (spans, measurements);
    }
}

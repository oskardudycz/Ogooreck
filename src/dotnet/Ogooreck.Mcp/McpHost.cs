using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ogooreck.Mcp;

internal static class McpHost
{
    public static async Task Run()
    {
        var spans = new CollectedSpans();
        await using var receiver = SpanReceiver.Start(spans);
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton(spans);
        builder.Services.AddSingleton(receiver);
        builder.Services.AddSingleton<SourceFiles>();
        builder.Services.AddSingleton<TestRunner>();
        if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("Ogooreck.Mcp"))
                .WithTracing(tracing => tracing
                    .AddSource(McpTracing.Source.Name, "Experimental.ModelContextProtocol")
                    .AddOtlpExporter(options => options.ExportProcessorType = ExportProcessorType.Simple));
        builder.Services
            .AddMcpServer(options => options.ServerInstructions = """
                Runs .NET tests and explains them with the OpenTelemetry spans of the tests, Ogooreck and the tested app.
                Start with ogooreck_test_run on a test project. Pass arguments, such as a filter, to run only the tests you need. It returns the test counts and each failed test with its message and the file and line of its Ogooreck When step.
                Then call ogooreck_scenario_diagram to draw a test's steps and the messages they caused, ogooreck_find_spans to search the spans by name, error or attribute, ogooreck_slowest_steps to see which Given/When/Then steps, catch-ups, app starts and expectations took longest, or ogooreck_export_spans to save all spans to a JSON file.
                Spans come from test projects that export over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set; ogooreck_test_run sets it.
                """)
            .WithStdioServerTransport()
            .WithToolsFromAssembly();
        await builder.Build().RunAsync();
    }
}

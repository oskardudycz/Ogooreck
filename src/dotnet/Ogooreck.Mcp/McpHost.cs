using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
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
                Start with ogooreck_test_run on a test project. Pass arguments, such as a filter, to run only the tests you need. It returns the test counts, the target frameworks the tests ran on, the slowest tests, build errors, and each failed test with its message and the file and line of its Ogooreck When step.
                Then call ogooreck_test_spans to see the span tree of a test, ogooreck_scenario_diagram to draw a test's steps and the messages they caused, ogooreck_find_spans to search the spans by name, error or attribute, ogooreck_concurrent_spans to see what else ran during a span, for races between tests, ogooreck_slowest_steps to see which Given/When/Then steps, catch-ups, app starts and expectations took longest, or ogooreck_export_spans to save all spans to a JSON file.
                To read the spans of a CI run, call ogooreck_import_spans with the file that the CI run's OpenTelemetry Collector wrote; the other tools then read those spans.
                For a flaky test, pass repeat to ogooreck_test_run, so it runs until the first failure. For a hanging test, pass hangTimeout, so the run is aborted and names the test that was running and the Ogooreck spans whose parent never ended. To find code no test reaches, pass coverage with the library name, for example "Ogooreck", and the run returns the library's uncovered lines and partly covered branches; coverageFiles, coverageTop and coverageSort narrow and order them.
                Ogooreck exports the spans of a test project over OTLP by itself when OTEL_EXPORTER_OTLP_ENDPOINT is set; ogooreck_test_run sets it. Ogooreck then listens to every ActivitySource, so if a test passes here but fails in CI, run it with plain dotnet test too.
                """)
            .WithStdioServerTransport()
            .WithToolsFromAssembly(serializerOptions: new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            });
        await builder.Build().RunAsync();
    }
}

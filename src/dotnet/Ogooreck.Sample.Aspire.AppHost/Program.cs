var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Ogooreck_Sample_Aspire_Api>("api").WithHttpEndpoint(name: "http").WithHttpHealthCheck("/echo");

if (builder.Configuration["WithKafka"] != "false")
{
    var kafka = builder.AddKafka("kafka");
    api.WithReference(kafka).WaitFor(kafka);
}

var otlpEndpoint = builder.Configuration["OtlpEndpoint"];
if (otlpEndpoint is not null)
    api.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
        .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
        .WithEnvironment("OTEL_BSP_SCHEDULE_DELAY", "200")
        .WithEnvironment("OTEL_METRIC_EXPORT_INTERVAL", "500");

builder.Build().Run();

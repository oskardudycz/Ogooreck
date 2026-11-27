using System.Diagnostics;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;

namespace Ogooreck.Mcp.Tests.Fixtures;

internal static class Collector
{
    public const string Image = "otel/opentelemetry-collector-contrib:0.164.0";
    private const int OtlpHttp = 4318;
    private const string SpansFile = "/spans.pb";

    private static readonly string Config = $"""
        receivers:
          otlp:
            protocols:
              http:
                endpoint: 0.0.0.0:{OtlpHttp}
        exporters:
          file:
            path: {SpansFile}
            format: proto
        service:
          pipelines:
            traces:
              receivers: [otlp]
              exporters: [file]
        """;

    public static async Task<byte[]> SpansOfAFixtureRunAnd(string otlpJson) =>
        await SpansWritten(async (endpoint, ct) =>
        {
            await RunTheFixture(endpoint, ct);
            using var http = new HttpClient();
            using var exported = await http.PostAsync(new Uri(endpoint, "/v1/traces"),
                new StringContent(otlpJson, Encoding.UTF8, "application/json"), ct);
            exported.EnsureSuccessStatusCode();
        });

    private static async Task<byte[]> SpansWritten(Func<Uri, CancellationToken, Task> export)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var collector = new ContainerBuilder(Image)
            .WithResourceMapping(Encoding.UTF8.GetBytes(Config), "/etc/otelcol-contrib/config.yaml")
            .WithCreateParameterModifier(container => container.User = "0")
            .WithPortBinding(OtlpHttp, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Everything is ready"))
            .Build();
        await collector.StartAsync(ct);

        await export(new Uri($"http://{collector.Hostname}:{collector.GetMappedPublicPort(OtlpHttp)}"), ct);

        await collector.StopAsync(ct);
        return await collector.ReadFileAsync(SpansFile, ct);
    }

    private static async Task RunTheFixture(Uri endpoint, CancellationToken ct)
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", ["test", Fixture, "--no-build"])
        {
            WorkingDirectory = SourceDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint.ToString().TrimEnd('/'),
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["MSBUILDDISABLENODEREUSE"] = "1"
            }
        })!;
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        Assert.Matches(@"Total: +[1-9]", (await output) + (await errors));
    }
}

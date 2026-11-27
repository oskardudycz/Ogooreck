using System.Net;
using System.Net.Sockets;
using System.Text;
using ModelContextProtocol.Client;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;

namespace Ogooreck.Mcp.Tests;

public class OwnSpansTests
{
    [Fact]
    public async Task TheServer_ExportsItsOwnSpans_ToTheCollectorThatTheEnvironmentNames()
    {
        var port = FreePort();
        using var collector = new HttpListener { Prefixes = { $"http://127.0.0.1:{port}/" } };
        collector.Start();
        var testRunExported = new TaskCompletionSource();
        var answering = AnswerEveryExport(collector, testRunExported);
        await using (var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ogooreck",
            Command = "dotnet",
            Arguments = [Server],
            WorkingDirectory = SourceDirectory,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = $"http://127.0.0.1:{port}",
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf"
            }
        }), cancellationToken: TestContext.Current.CancellationToken))
        {
            await client.CallToolAsync("ogooreck_test_run", new Dictionary<string, object?> { ["project"] = Fixture, ["arguments"] = new[] { "--no-build" } },
                cancellationToken: TestContext.Current.CancellationToken);

            await testRunExported.Task.WaitAsync(TestContext.Current.CancellationToken);
        }

        collector.Stop();
        await answering;
    }

    private static async Task AnswerEveryExport(HttpListener collector, TaskCompletionSource testRunExported)
    {
        while (true)
        {
            HttpListenerContext exported;
            try
            {
                exported = await collector.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException && !collector.IsListening)
            {
                return;
            }

            using var body = new StreamReader(exported.Request.InputStream, Encoding.Latin1);
            if (exported.Request.Url!.AbsolutePath == "/v1/traces" && (await body.ReadToEndAsync()).Contains("test_run"))
                testRunExported.TrySetResult();
            exported.Response.Close();
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

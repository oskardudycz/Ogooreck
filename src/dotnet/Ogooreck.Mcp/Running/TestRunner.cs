using System.Diagnostics;
using ModelContextProtocol;

namespace Ogooreck.Mcp;

public sealed class TestRunner(SpanReceiver receiver)
{
    public const string VSTest = "VSTest";
    public const string TestingPlatform = "Microsoft.Testing.Platform";

    public async Task<TestProcess> Run(string project, string[] arguments, string results, int? hangTimeout,
        CancellationToken ct)
    {
        if (!File.Exists(project) && !Directory.Exists(project))
            throw new McpException($"No test project at {Path.GetFullPath(project)}.");

        var runner = await RunnerOf(project, ct);
        if (runner == TestingPlatform && hangTimeout is not null)
            throw new McpException(
                "hangTimeout works only for VSTest projects. For a Microsoft.Testing.Platform project, reference Microsoft.Testing.Extensions.HangDump and pass [\"--hangdump\", \"--hangdump-timeout\", \"30s\"] in arguments.");
        string[] blame = hangTimeout is { } seconds
            ? ["--blame-hang-timeout", $"{seconds}s", "--blame-hang-dump-type", "none"]
            : [];
        string[] command = runner == TestingPlatform
            ? ["run", "--project", project, "--", "--report-trx", "--results-directory", results, .. arguments]
            : ["test", project, "--logger", "trx", "--results-directory", results, .. blame, .. arguments];
        var (exitCode, output) = await Dotnet(command, ct);
        return new TestProcess(runner, exitCode, output);
    }

    private async Task<string> RunnerOf(string project, CancellationToken ct)
    {
        var (exitCode, output) = await Dotnet(["msbuild", project, "-getProperty:IsTestingPlatformApplication"], ct);
        if (exitCode != 0)
            throw new McpException($"Cannot read the test runner of {project}: {string.Join('\n', output.TakeLast(10))}");
        return output.Any(line => line.Trim() == "true") ? TestingPlatform : VSTest;
    }

    private async Task<(int ExitCode, string[] Output)> Dotnet(string[] arguments, CancellationToken ct)
    {
        var start = new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = receiver.Endpoint.ToString().TrimEnd('/'),
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["MSBUILDDISABLENODEREUSE"] = "1"
            }
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, ((await output) + (await errors)).Split('\n', StringSplitOptions.TrimEntries));
    }
}

public sealed record TestProcess(string Runner, int ExitCode, string[] Output);

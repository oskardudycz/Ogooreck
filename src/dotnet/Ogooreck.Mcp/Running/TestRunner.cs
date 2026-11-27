using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace Ogooreck.Mcp;

public sealed class TestRunner(SpanReceiver receiver, CollectedSpans collected)
{
    public const string VSTest = "VSTest";
    public const string TestingPlatform = "Microsoft.Testing.Platform";

    public async Task<TestProcess> Run(string project, string[] arguments, string results, int? hangTimeout,
        CancellationToken ct)
    {
        if (!File.Exists(project) && !Directory.Exists(project))
            throw new McpException(
                $"No test project at {Path.GetFullPath(project)}. The path is relative to {Directory.GetCurrentDirectory()}.");

        var (runner, projectDirectory, assemblyName) = await RunnerOf(project, ct);
        if (runner == TestingPlatform && hangTimeout is not null)
            throw new McpException(
                "hangTimeout works only for VSTest projects. For a Microsoft.Testing.Platform project, reference Microsoft.Testing.Extensions.HangDump and pass [\"--hangdump\", \"--hangdump-timeout\", \"30s\"] in arguments.");
        string[] blame = hangTimeout is { } seconds
            ? ["--blame-hang-timeout", $"{seconds}s", "--blame-hang-dump-type", "none"]
            : [];
        string[] command;
        string? workingDirectory = null;
        if (runner == TestingPlatform)
        {
            command = ["test", "--project", project, "--report-trx", "--results-directory", results, .. arguments];
            workingDirectory = await OptedIntoTestingPlatform(projectDirectory, results, ct);
        }
        else
            command = ["test", project, "--logger", "trx", "--results-directory", results, .. blame, .. arguments];
        var started = DateTime.Now;
        var (exitCode, output) = await Dotnet(command, ct, workingDirectory);
        if (hangTimeout is not null)
            StopTestProcessesLeftAfterAbort(assemblyName, started);
        return new TestProcess(runner, exitCode, output);
    }

    private async Task<string> OptedIntoTestingPlatform(string projectDirectory, string results, CancellationToken ct)
    {
        var (_, version) = await Dotnet(["--version"], ct, projectDirectory);
        var directory = Directory.CreateDirectory(Path.Combine(results, "dotnet-test")).FullName;
        await File.WriteAllTextAsync(Path.Combine(directory, "global.json"),
            $$"""{ "sdk": { "version": "{{version[0]}}", "rollForward": "disable" }, "test": { "runner": "{{TestingPlatform}}" } }""",
            ct);
        return directory;
    }

    private static void StopTestProcessesLeftAfterAbort(string assemblyName, DateTime started)
    {
        foreach (var process in Process.GetProcessesByName(assemblyName))
            using (process)
                if (process.StartTime >= started)
                    process.Kill(entireProcessTree: true);
    }

    private async Task<(string Runner, string ProjectDirectory, string AssemblyName)> RunnerOf(string project,
        CancellationToken ct)
    {
        var (exitCode, output) = await Dotnet(
        [
            "msbuild", project, "-getProperty:IsTestingPlatformApplication", "-getProperty:MSBuildProjectDirectory",
            "-getProperty:AssemblyName"
        ], ct);
        if (exitCode != 0)
            throw new McpException($"Cannot read the test runner of {project}: {string.Join('\n', output.TakeLast(10))}");
        var properties = JsonDocument.Parse(string.Join('\n', output)).RootElement.GetProperty("Properties");
        return (properties.GetProperty("IsTestingPlatformApplication").GetString() == "true" ? TestingPlatform : VSTest,
            properties.GetProperty("MSBuildProjectDirectory").GetString()!,
            properties.GetProperty("AssemblyName").GetString()!);
    }

    private async Task<(int ExitCode, string[] Output)> Dotnet(string[] arguments, CancellationToken ct,
        string? workingDirectory = null)
    {
        var start = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = receiver.Endpoint.ToString().TrimEnd('/'),
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["MSBUILDDISABLENODEREUSE"] = "1",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
            }
        };
        string[] commandArguments = ["dotnet", .. arguments];
        using var activity = McpTracing.Source.StartActivity($"dotnet {arguments[0]}")!;
        activity.SetTag("process.command_args", commandArguments);
        using var process = Process.Start(start)!;
        await using var _ = ct.Register(() => process.Kill(entireProcessTree: true));
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var lines = ((await output) + (await errors)).Split('\n', StringSplitOptions.TrimEntries);
        activity.SetTag("process.exit.code", process.ExitCode);
        activity.Stop();
        collected.Add([SpanOf(activity, commandArguments, process.ExitCode)]);
        return (process.ExitCode, lines);
    }

    private static CollectedSpan SpanOf(Activity activity, string[] commandArguments, int exitCode) =>
        new("Ogooreck.Mcp", "", McpTracing.Source.Name, activity.TraceId.ToHexString(), activity.SpanId.ToHexString(),
            activity.ParentSpanId.ToHexString(), activity.DisplayName, "internal", activity.StartTimeUtc,
            activity.StartTimeUtc + activity.Duration, null,
            new Dictionary<string, JsonNode>
            {
                ["process.command_args"] = new JsonArray(commandArguments.Select(argument => (JsonNode)argument).ToArray()),
                ["process.exit.code"] = exitCode.ToString(CultureInfo.InvariantCulture)
            });
}

public sealed record TestProcess(string Runner, int ExitCode, string[] Output);

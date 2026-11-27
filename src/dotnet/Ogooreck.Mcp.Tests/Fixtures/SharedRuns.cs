using System.Collections.Concurrent;
using Xunit;

[assembly: AssemblyFixture(typeof(Ogooreck.Mcp.Tests.Fixtures.SharedRuns))]

namespace Ogooreck.Mcp.Tests.Fixtures;

public sealed class SharedRuns: IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<SharedRun>>> runs = new();

    public Task<SharedRun> Of(string project, params string[] arguments) =>
        runs.GetOrAdd(string.Join(' ', [project, .. arguments]),
            _ => new Lazy<Task<SharedRun>>(() => Started(project, arguments))).Value;

    public Task<SharedRun> Covering(string library, string project, params string[] arguments) =>
        Covering(new CoverageScope(library), project, arguments);

    public Task<SharedRun> Covering(CoverageScope scope, string project, params string[] arguments) =>
        runs.GetOrAdd(string.Join(' ', [$"{scope}", project, .. arguments]),
            _ => new Lazy<Task<SharedRun>>(() => Started(project, arguments, scope))).Value;

    private static async Task<SharedRun> Started(string project, string[] arguments, CoverageScope? coverage = null)
    {
        var server = new McpServer();
        await server.InitializeAsync();
        return new SharedRun(server, await server.TestRun(project, arguments, coverage: coverage?.Library,
            coverageFiles: coverage?.Files, coverageTop: coverage?.Top ?? 25, coverageSort: coverage?.Sort ?? "uncovered"));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var run in runs.Values.Where(run => run.IsValueCreated))
            await (await run.Value).Server.DisposeAsync();
    }
}

public sealed record SharedRun(McpServer Server, TestRunResult Result);

public sealed record CoverageScope(string Library, string? Files = null, int Top = 25, string Sort = "uncovered");

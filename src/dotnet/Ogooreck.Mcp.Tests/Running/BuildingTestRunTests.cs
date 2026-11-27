using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.Running;

public class BuildingTestRunTests(McpServer server): IClassFixture<McpServer>
{
    [Fact]
    public async Task TestRun_OfABuiltUnchangedProject_CompilesNothing()
    {
        string[] arguments = ["--filter", "FullyQualifiedName~RespondsWithCreated"];
        var assembly = new FileInfo(Path.Combine(SourceDirectory, Fixture, "bin", "Debug", "net10.0", $"{Fixture}.dll"));
        var built = assembly.LastWriteTimeUtc;

        await server.TestRun(Fixture, arguments, build: true);

        assembly.Refresh();
        Assert.Equal(built, assembly.LastWriteTimeUtc);
    }

    [Fact]
    public async Task TestRun_OfADeterministicBuild_MapsTheSourcePathsToTheRepository()
    {
        var artifacts = Directory.CreateTempSubdirectory("ogooreck-mcp-artifacts-");
        try
        {
            var run = await server.TestRun(Fixture,
                ["--artifacts-path", artifacts.FullName, "-p:ContinuousIntegrationBuild=true", "--filter", "FullyQualifiedName~RespondsWithOk"],
                build: true);

            Assert.Equal(FailingWhen, Assert.Single(Assert.Single(run.FailedTests).Runs).Location);
            Assert.Contains("/_/", Assert.Single(run.FailedTests).StackTrace);
        }
        finally
        {
            artifacts.Delete(true);
        }
    }

    [Fact]
    public async Task TestRun_OfAProjectThatDoesNotCompile_ReturnsEachBuildErrorOnce()
    {
        var project = Directory.CreateTempSubdirectory("ogooreck-mcp-broken-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(project.FullName, "Broken.Tests.csproj"),
                """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFrameworks>net10.0;net11.0</TargetFrameworks></PropertyGroup></Project>""",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(project.FullName, "Broken.cs"), "public class Broken { int Count => \"many\"; }",
                TestContext.Current.CancellationToken);

            var run = await server.TestRun(project.FullName, build: true);

            Assert.NotEqual(0, run.ExitCode);
            var error = Assert.Single(run.BuildErrors);
            Assert.Equal(new BuildError("Broken.cs:1", "CS0029", "Cannot implicitly convert type 'string' to 'int'"), error);
            Assert.Empty(run.OutputTail);
        }
        finally
        {
            project.Delete(true);
        }
    }
}

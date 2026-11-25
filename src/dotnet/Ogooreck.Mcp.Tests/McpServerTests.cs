using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;
using static Ogooreck.Mcp.Tests.Sources;

namespace Ogooreck.Mcp.Tests;

[Collection(BuildsTheFixture)]
public class McpServerTests(McpServer server): IClassFixture<McpServer>
{
    [Fact]
    public void Instructions_TellAnAgentWhereToStart()
    {
        var instructions = server.Client.ServerInstructions;

        Assert.NotNull(instructions);
        Assert.Contains("ogooreck_test_run", instructions);
        Assert.Contains("ogooreck_find_spans", instructions);
        Assert.Contains("ogooreck_scenario_diagram", instructions);
        Assert.Contains("ogooreck_slowest_steps", instructions);
    }

    [Fact]
    public async Task Tools_ShareTheOgooreckPrefix_HaveTitles_AndSayWhichOnlyRead()
    {
        var tools = (await server.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken))
            .ToDictionary(tool => tool.Name, tool => tool.ProtocolTool.Annotations);

        Assert.Equal(["ogooreck_export_spans", "ogooreck_find_spans", "ogooreck_scenario_diagram", "ogooreck_slowest_steps", "ogooreck_test_run"],
            tools.Keys.Order());
        Assert.All(tools.Values, annotations => Assert.False(string.IsNullOrEmpty(annotations?.Title)));
        Assert.True(tools["ogooreck_slowest_steps"]!.ReadOnlyHint);
        Assert.True(tools["ogooreck_find_spans"]!.ReadOnlyHint);
        Assert.True(tools["ogooreck_scenario_diagram"]!.ReadOnlyHint);
        Assert.False(tools["ogooreck_test_run"]!.ReadOnlyHint);
    }

    [Fact]
    public async Task TestRun_NamesEachFailedTestWithTheLocationOfItsRun()
    {
        var run = await server.TestRun(Fixture);

        Assert.Equal(new TestCounts(4, 2, 1, 1), run.Tests);
        var failed = Assert.Single(run.FailedTests);
        Assert.Equal(FailingTest, failed.Name);
        Assert.Contains("expected <200 OK>, but found <201 Created>", failed.Message);
        var failedRun = Assert.Single(failed.Runs);
        Assert.Equal(FailingWhen, failedRun.Location);
    }

    [Fact]
    public async Task TestRun_PassesTheArgumentsToDotnetTest()
    {
        var run = await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~RespondsWithCreated"]);

        Assert.Equal(new TestCounts(1, 1, 0, 0), run.Tests);
        Assert.Empty(run.FailedTests);
        Assert.Empty(run.OutputTail);
    }

    [Fact]
    public async Task TestRun_ThatRunsNoTests_ReturnsTheEndOfTheOutput()
    {
        var run = await server.TestRun(Fixture, ["--no-such-option"]);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Equal(new TestCounts(0, 0, 0, 0), run.Tests);
        Assert.Contains(run.OutputTail, line => line.Contains("--no-such-option"));
    }

    [Fact]
    public async Task TestRun_OfADeterministicBuild_MapsTheSourcePathsToTheRepository()
    {
        var run = await server.TestRun(Fixture, ["-p:ContinuousIntegrationBuild=true", "--filter", "FullyQualifiedName~RespondsWithOk"]);

        Assert.Equal(FailingWhen, Assert.Single(Assert.Single(run.FailedTests).Runs).Location);
        Assert.Contains("/_/", Assert.Single(run.FailedTests).StackTrace);
    }

    [Fact]
    public async Task TestRun_RunsATestingPlatformProjectAsAnApplication()
    {
        var run = await server.TestRun("Ogooreck.TUnit.Tests");

        Assert.Equal("Microsoft.Testing.Platform", run.Runner);
        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Tests.Passed > 0);
        Assert.True(run.Spans > 0);
    }

    [Fact]
    public async Task TestRun_OfAnFSharpProject_NamesTheTestAndTheLineOfEachStep()
    {
        var run = await server.TestRun("Ogooreck.Wolverine.FSharp.Tests");

        Assert.Equal(0, run.ExitCode);
        var steps = Structured<SlowSteps>(await server.Call("ogooreck_slowest_steps", new() { ["top"] = 50 }));
        Assert.Contains(steps.Steps, step => step.Test == "PlacingAnOrderTests.placing an order publishes OrderPlaced" &&
                                             step.Location.StartsWith("src/dotnet/Ogooreck.Wolverine.FSharp.Tests/PlacingAnOrderTests.fs:"));
    }

    [Fact]
    public async Task TestRun_OfAMissingProject_IsAnError()
    {
        var missing = Path.Combine(Path.GetTempPath(), "Ogooreck.Missing.Tests");

        var result = await server.Call("ogooreck_test_run", new() { ["project"] = missing });

        Assert.True(result.IsError);
        Assert.Contains($"No test project at {missing}", Text(result));
    }

    [Fact]
    public async Task TestRun_OfAFolderWithoutAProject_IsAnError()
    {
        var result = await server.Call("ogooreck_test_run", new() { ["project"] = "Shared" });

        Assert.True(result.IsError);
        Assert.Contains("Cannot read the test runner of", Text(result));
    }

    [Fact]
    public async Task SlowestSteps_NameTheTestAndTheLineOfEachStep()
    {
        await server.TestRun(Fixture);

        var steps = Structured<SlowSteps>(await server.Call("ogooreck_slowest_steps", new() { ["top"] = 50 }));

        Assert.Null(steps.File);
        Assert.Contains(steps.Steps, step => step.Test == FailingTest && step.Location == FailingWhen);
        Assert.Contains(steps.Steps, step => step.App.Length > 0);
    }

    [Fact]
    public async Task SlowestSteps_ShowTheSpansDownToTheDepth()
    {
        await server.TestRun(Fixture);

        var steps = Structured<SlowSteps>(await server.Call("ogooreck_slowest_steps", new() { ["top"] = 50, ["depth"] = 1 }));

        Assert.Contains(steps.Steps, step => step.WaitedOn.Length > 0);
        Assert.All(steps.Steps, step => Assert.All(step.WaitedOn, span => Assert.Empty(span.Children)));
    }

    [Fact]
    public async Task SlowestSteps_WithAFilename_WriteTheStepsToTheFileInsteadOfTheResponse()
    {
        await server.TestRun(Fixture);
        var file = server.FileIn("steps.json");

        var steps = Structured<SlowSteps>(await server.Call("ogooreck_slowest_steps", new() { ["filename"] = file }));

        Assert.Empty(steps.Steps);
        Assert.Equal(file, steps.File);
        var written = JsonSerializer.Deserialize<SlowStep[]>(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;
        Assert.Contains(written, step => step.Test == FailingTest);
    }

    [Fact]
    public async Task FindSpans_ReturnEachMatchWithItsPathTestAndLine()
    {
        await server.TestRun(Fixture);

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = "expected <200 ok>" }));

        Assert.True(found.Total > 0);
        Assert.All(found.Spans, span => Assert.Equal(FailingTest, span.Test));
        Assert.Contains(found.Spans, span => span.Location == FailingWhen && span.Path.Length == 1);
    }

    [Fact]
    public async Task FindSpans_OfTheApp_LeadToTheTestThatSentTheRequest()
    {
        await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~RespondsWithOk"]);

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = "^POST /api/products" }));

        Assert.Contains(found.Spans, span => span.Service == Fixture && span.Test == FailingTest && span.Location == FailingWhen);
    }

    [Fact]
    public async Task FindSpans_OutsideOfAnOgooreckRun_HaveNoTestOrLocation()
    {
        await server.TestRun(Fixture);

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans", new() { ["pattern"] = "^own\\.ratio$" }));

        var span = Assert.Single(found.Spans);
        Assert.Equal(["own work"], span.Path);
        Assert.Null(span.Test);
        Assert.Null(span.Location);
        Assert.Null(span.Error);
        Assert.Equal("true", span.Attributes["own.done"]);
        Assert.Equal("false", span.Attributes["own.retried"]);
        Assert.Equal("0.5", span.Attributes["own.ratio"]);
        Assert.DoesNotContain("own.labels", span.Attributes.Keys);
    }

    [Fact]
    public async Task FindSpans_ReturnOnlyTheLimit_AndCountAllMatches()
    {
        await server.TestRun(Fixture);

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = ".", ["limit"] = 2 }));

        Assert.Equal(2, found.Spans.Length);
        Assert.Equal(found.Total, (await server.TestRunSpans()).Length);
    }

    [Fact]
    public async Task FindSpans_WithAFilename_WriteTheMatchesToTheFileInsteadOfTheResponse()
    {
        await server.TestRun(Fixture);
        var file = server.FileIn("found.json");

        var found = Structured<FoundSpans>(await server.Call("ogooreck_find_spans",
            new() { ["pattern"] = "RegisteringAProduct", ["filename"] = file }));

        Assert.Empty(found.Spans);
        Assert.Equal(file, found.File);
        var written = JsonSerializer.Deserialize<FoundSpan[]>(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;
        Assert.Equal(found.Total, written.Length);
    }

    [Fact]
    public async Task ScenarioDiagram_ShowsTheStepsAndTheMessagesTheAppConsumed()
    {
        await server.TestRun("Ogooreck.Wolverine.Tests", ["--filter", "FullyQualifiedName~ActionAdvancingAFakeClock"]);

        var diagram = Text(await server.Call("ogooreck_scenario_diagram",
            new() { ["test"] = "ActionAdvancingAFakeClock_ExpiresTheCartAndPublishesCartExpired" })).Split('\n');

        Assert.Equal("sequenceDiagram", diagram[0]);
        Assert.Equal("    participant Test", diagram[1]);
        Assert.Contains("    participant p1 as Ogooreck.Wolverine.Tests", diagram);
        Assert.Contains("    participant p2 as local://events/", diagram);
        Assert.Contains("    participant p3 as local://ogooreck.sample.wolverine.expirecart/", diagram);
        Assert.Contains(diagram, line => line.StartsWith("    Test->>p1: Given step 1 request POST /carts/"));
        Assert.Equal(
            ["Test->>p1: Given", "p2->>p1: ProductAdded", "Test->>p1: When action", "p3->>p1: ExpireCart", "p2->>p1: CartExpired"],
            diagram.Where(line => line.Contains("->>")).Select(line => line.Trim()).Select(line => line.StartsWith("Test->>p1: Given") ? "Test->>p1: Given" : line));
    }

    [Fact]
    public async Task ScenarioDiagram_OfAFailedTest_EndsWithTheFailure()
    {
        await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~RespondsWithOk"]);

        var diagram = Text(await server.Call("ogooreck_scenario_diagram", new() { ["test"] = FailingTest })).Split('\n');

        Assert.Contains("    Test->>p1: When request POST /api/products", diagram);
        Assert.StartsWith("    Note over Test: failed: When request POST /api/products: status: expected <200 OK>, but found <201 Created>.",
            diagram[^1]);
    }

    [Fact]
    public async Task ScenarioDiagram_OfATestWithoutAnOgooreckRun_IsAnError()
    {
        await server.TestRun(Fixture, ["--filter", "FullyQualifiedName~WritingSpansOfItsOwn"]);

        var result = await server.Call("ogooreck_scenario_diagram", new() { ["test"] = "WritingSpansOfItsOwn" });

        Assert.True(result.IsError);
        Assert.Contains("No Ogooreck run of WritingSpansOfItsOwn in the last ogooreck_test_run.", Text(result));
    }

    [Fact]
    public async Task ExportSpans_WritesAllSpansOfTheLastRun()
    {
        var run = await server.TestRun(Fixture);

        var spans = await server.TestRunSpans();

        Assert.Equal(run.Spans, spans.Length);
        Assert.Contains(spans, span => span.Attributes.GetValueOrDefault("code.function.name") == FailingTest);
    }

    internal static T Structured<T>(CallToolResult result) =>
        JsonSerializer.Deserialize<T>(result.StructuredContent!.Value, JsonSerializerOptions.Web)!;

    internal static string Text(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}

public sealed class McpServer: IAsyncLifetime
{
    private readonly DirectoryInfo files = Directory.CreateTempSubdirectory("ogooreck-mcp-tests-");

    public McpClient Client { get; private set; } = null!;

    public string FileIn(string name) => Path.Combine(files.FullName, name);

    public async ValueTask InitializeAsync() =>
        Client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "ogooreck",
            Command = "dotnet",
            Arguments = [Server],
            WorkingDirectory = SourceDirectory
        }), cancellationToken: TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        files.Delete(true);
    }

    public Task<CallToolResult> Call(string tool, Dictionary<string, object?> arguments) =>
        Client.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken).AsTask();

    public async Task<TestRunResult> TestRun(string project, string[]? arguments = null)
    {
        var result = await Call("ogooreck_test_run", new() { ["project"] = project, ["arguments"] = arguments });
        Assert.True(result.IsError is null or false, McpServerTests.Text(result));
        return McpServerTests.Structured<TestRunResult>(result);
    }

    public async Task<CollectedSpan[]> TestRunSpans()
    {
        var file = FileIn("spans.json");
        var result = await Call("ogooreck_export_spans", new() { ["path"] = file });
        Assert.Contains(file, McpServerTests.Text(result));
        return JsonSerializer.Deserialize<CollectedSpan[]>(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken),
            JsonSerializerOptions.Web)!;
    }
}

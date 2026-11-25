using System.Text.Json;
using ModelContextProtocol.Protocol;
using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;
using static Ogooreck.Mcp.Tests.Fixtures.Sources;
using static Ogooreck.Mcp.Tests.Fixtures.ToolResults;

namespace Ogooreck.Mcp.Tests.SlowestSteps;

[Collection(BuildsTheFixture)]
public class SlowestStepsTests(McpServer server): IClassFixture<McpServer>
{
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
}

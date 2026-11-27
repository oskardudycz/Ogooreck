using Ogooreck.Mcp.Tests.Fixtures;
using Xunit;

namespace Ogooreck.Mcp.Tests.ImportingSpans;

public sealed class CollectedFile: IAsyncLifetime
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("ogooreck-mcp-collected-");

    public string Path => System.IO.Path.Combine(directory.FullName, "collected.pb");

    public async ValueTask InitializeAsync() =>
        await File.WriteAllBytesAsync(Path, await Collector.SpansOfAFixtureRunAnd(SpanWithAMapAttribute),
            TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        directory.Delete(true);
        return default;
    }

    private const string SpanWithAMapAttribute = """
        {"resourceSpans":[{"scopeSpans":[{"scope":{"name":"own"},"spans":[{
          "traceId":"5b8efff798038103d269b633813fc60c","spanId":"eee19b7ec3c1b174","name":"own map work","kind":1,
          "startTimeUnixNano":"1000","endTimeUnixNano":"2000",
          "attributes":[
            {"key":"own.map","value":{"kvlistValue":{"values":[{"key":"a","value":{"stringValue":"b"}}]}}},
            {"key":"own.name","value":{"stringValue":"kept"}}]}]}]}]}
        """;
}

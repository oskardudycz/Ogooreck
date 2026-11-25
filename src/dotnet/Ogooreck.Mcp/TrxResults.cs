using System.Xml.Linq;

namespace Ogooreck.Mcp;

public static class TrxResults
{
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static IReadOnlyList<TrxResult> Read(string directory) =>
        Directory.EnumerateFiles(directory, "*.trx", SearchOption.AllDirectories)
            .SelectMany(file => ResultsOf(XDocument.Load(file))).ToArray();

    private static IEnumerable<TrxResult> ResultsOf(XDocument trx)
    {
        var names = trx.Descendants(Trx + "TestMethod").ToDictionary(
            method => (string)method.Parent!.Attribute("id")!,
            method => $"{(string)method.Attribute("className")!}.{(string)method.Attribute("name")!}");

        return trx.Descendants(Trx + "UnitTestResult").Select(result => new TrxResult(
            names[(string)result.Attribute("testId")!],
            (string)result.Attribute("outcome")!,
            (string?)result.Descendants(Trx + "Message").FirstOrDefault(),
            (string?)result.Descendants(Trx + "StackTrace").FirstOrDefault()));
    }
}

public sealed record TrxResult(string Name, string Outcome, string? Message, string? StackTrace)
{
    public bool Passed => Outcome == "Passed";
    public bool Skipped => Outcome == "NotExecuted";
    public bool Failed => Outcome is not ("Passed" or "NotExecuted");
}

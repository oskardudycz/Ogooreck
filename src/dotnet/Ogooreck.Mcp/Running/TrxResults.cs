using System.Globalization;
using System.Xml.Linq;

namespace Ogooreck.Mcp;

public static class TrxResults
{
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static IReadOnlyList<TrxResult> Read(string directory) =>
        Directory.EnumerateFiles(directory, "*.trx", SearchOption.TopDirectoryOnly)
            .SelectMany(file => ResultsOf(XDocument.Load(file))).ToArray();

    private static IEnumerable<TrxResult> ResultsOf(XDocument trx)
    {
        var methods = trx.Descendants(Trx + "TestMethod").ToDictionary(
            method => (string)method.Parent!.Attribute("id")!);

        return trx.Descendants(Trx + "UnitTestResult").Select(result =>
        {
            var method = methods[(string)result.Attribute("testId")!];
            return new TrxResult(
                $"{(string)method.Attribute("className")!}.{(string)method.Attribute("name")!}",
                Path.GetFileName(Path.GetDirectoryName((string)method.Attribute("codeBase")!))!,
                (string)result.Attribute("outcome")!,
                (string?)result.Descendants(Trx + "Message").FirstOrDefault(),
                (string?)result.Descendants(Trx + "StackTrace").FirstOrDefault(),
                DateTimeOffset.Parse((string)result.Attribute("startTime")!, CultureInfo.InvariantCulture),
                TimeSpan.Parse((string)result.Attribute("duration")!, CultureInfo.InvariantCulture));
        });
    }
}

public sealed record TrxResult(string Name, string Framework, string Outcome, string? Message, string? StackTrace, DateTimeOffset Start,
    TimeSpan Duration)
{
    public bool Passed => Outcome == "Passed";
    public bool Skipped => Outcome == "NotExecuted";
    public bool Failed => Outcome is not ("Passed" or "NotExecuted");
}

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ogooreck.Mcp;

internal static partial class CoverageReports
{
    public static Coverage Read(string results, string library, SourceFiles sources)
    {
        var lines = Directory.EnumerateFiles(results, "*.cobertura.xml", SearchOption.AllDirectories)
            .SelectMany(report => XDocument.Load(report).Descendants("package"))
            .Where(package => package.Attribute("name")!.Value == library)
            .SelectMany(package => package.Descendants("class"))
            .SelectMany(type => type.Elements("lines").Elements("line")
                .Select(line => (File: sources.Relative(type.Attribute("filename")!.Value), Line: line)))
            .Where(entry => !entry.File.Contains("/obj/", StringComparison.Ordinal))
            .GroupBy(entry => (entry.File, Number: Number(entry.Line, "number")))
            .Select(group => new CoveredLine(group.Key.File, group.Key.Number,
                group.Max(entry => Number(entry.Line, "hits")) > 0,
                group.Select(entry => BranchesOf(entry.Line)).MaxBy(branches => branches.Covered)!))
            .OrderBy(line => line.File, StringComparer.Ordinal).ThenBy(line => line.Number)
            .ToArray();

        return new Coverage(library,
            new CoverageCounts(lines.Count(line => line.Hit), lines.Length),
            new CoverageCounts(lines.Sum(line => line.Branches.Covered), lines.Sum(line => line.Branches.Valid)),
            UncoveredRanges(lines),
            lines.Where(line => line.Branches.Covered < line.Branches.Valid)
                .Select(line => $"{line.File}:{line.Number} ({line.Branches.Covered}/{line.Branches.Valid})").ToArray());
    }

    private static string[] UncoveredRanges(CoveredLine[] lines)
    {
        var ranges = new List<(string File, int First, int Last)>();
        foreach (var line in lines.Where(line => !line.Hit))
            if (ranges.Count > 0 && ranges[^1].File == line.File && ranges[^1].Last == line.Number - 1)
                ranges[^1] = ranges[^1] with { Last = line.Number };
            else
                ranges.Add((line.File, line.Number, line.Number));
        return ranges.Select(range => range.First == range.Last
            ? $"{range.File}:{range.First}"
            : $"{range.File}:{range.First}-{range.Last}").ToArray();
    }

    private static int Number(XElement line, string attribute) =>
        int.Parse(line.Attribute(attribute)!.Value, CultureInfo.InvariantCulture);

    private static CoverageCounts BranchesOf(XElement line)
    {
        var match = ConditionCoverage().Match(line.Attribute("condition-coverage")?.Value ?? "");
        return match.Success
            ? new CoverageCounts(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
            : new CoverageCounts(0, 0);
    }

    [GeneratedRegex(@"\((\d+)/(\d+)\)")]
    private static partial Regex ConditionCoverage();

    private sealed record CoveredLine(string File, int Number, bool Hit, CoverageCounts Branches);
}

public sealed record Coverage(string Library, CoverageCounts Lines, CoverageCounts Branches, string[] UncoveredLines,
    string[] PartlyCoveredBranches);

public sealed record CoverageCounts(int Covered, int Valid);

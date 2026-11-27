using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Ogooreck.Mcp;

internal static partial class CoverageReports
{
    public static Coverage Read(string results, string library, string? files, int top, CoverageSort sort, SourceFiles sources)
    {
        var lines = Directory.EnumerateFiles(results, "*.cobertura.xml", SearchOption.AllDirectories)
            .SelectMany(report => XDocument.Load(report).Descendants("package"))
            .Where(package => package.Attribute("name")!.Value == library)
            .SelectMany(package => package.Descendants("class"))
            .SelectMany(type => type.Elements("lines").Elements("line")
                .Select(line => (File: sources.Relative(type.Attribute("filename")!.Value), Line: line)))
            .Where(entry => !entry.File.Contains("/obj/", StringComparison.Ordinal))
            .Where(entry => entry.File.Contains(files ?? "", StringComparison.Ordinal))
            .GroupBy(entry => (entry.File, Number: Number(entry.Line, "number")))
            .Select(group => new CoveredLine(group.Key.File, group.Key.Number,
                group.Max(entry => Number(entry.Line, "hits")) > 0,
                group.Select(entry => BranchesOf(entry.Line)).MaxBy(branches => branches.Covered)!))
            .OrderBy(line => line.File, StringComparer.Ordinal).ThenBy(line => line.Number)
            .ToArray();

        var uncovered = lines.Where(line => !line.Hit).GroupBy(line => line.File).ToArray();
        var partlyCovered = lines.Where(line => line.Branches.Covered < line.Branches.Valid).GroupBy(line => line.File).ToArray();
        return new Coverage(library,
            new CoverageCounts(lines.Count(line => line.Hit), lines.Length),
            new CoverageCounts(lines.Sum(line => line.Branches.Covered), lines.Sum(line => line.Branches.Valid)),
            Listed(uncovered, top, sort, file => $"{file.Key}: {string.Join(", ", Ranges(file))}"),
            uncovered.Length,
            Listed(partlyCovered, top, sort, file => $"{file.Key}: {string.Join(", ", file.Select(line => $"{line.Number} ({line.Branches.Covered}/{line.Branches.Valid})"))}"),
            partlyCovered.Length);
    }

    private static string[] Listed(IGrouping<string, CoveredLine>[] files, int top, CoverageSort sort,
        Func<IGrouping<string, CoveredLine>, string> describe) =>
        files.OrderByDescending(file => sort == CoverageSort.Uncovered ? file.Count() : 0).ThenBy(file => file.Key, StringComparer.Ordinal)
            .Take(top).Select(describe).ToArray();

    private static IEnumerable<string> Ranges(IEnumerable<CoveredLine> lines)
    {
        var ranges = new List<(int First, int Last)>();
        foreach (var line in lines)
            if (ranges.Count > 0 && ranges[^1].Last == line.Number - 1)
                ranges[^1] = ranges[^1] with { Last = line.Number };
            else
                ranges.Add((line.Number, line.Number));
        return ranges.Select(range => range.First == range.Last ? $"{range.First}" : $"{range.First}-{range.Last}");
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
    int UncoveredFiles, string[] PartlyCoveredBranches, int PartlyCoveredFiles);

public sealed record CoverageCounts(int Covered, int Valid);

[JsonConverter(typeof(JsonStringEnumConverter<CoverageSort>))]
public enum CoverageSort
{
    Uncovered,
    File
}

namespace Ogooreck.Mcp;

public sealed class SourceFiles
{
    private const string DeterministicRoot = "/_/";

    public string Root { get; private set; } = Directory.GetCurrentDirectory();

    public void RootAt(string project) => Root = RepositoryOf(project);

    private static string RepositoryOf(string project)
    {
        var directory = new DirectoryInfo(project);
        if (!directory.Exists)
            directory = directory.Parent!;
        for (var candidate = directory; candidate is not null; candidate = candidate.Parent)
            if (Path.Exists(Path.Combine(candidate.FullName, ".git")))
                return candidate.FullName;
        return directory.FullName;
    }

    public string Location(CollectedSpan span) =>
        $"{Relative(span.Attribute("code.file.path")!)}:{span.Attribute("code.line.number")}";

    public string Relative(string path) =>
        Path.GetRelativePath(Root, path.StartsWith(DeterministicRoot, StringComparison.Ordinal)
            ? Path.Combine(Root, path[DeterministicRoot.Length..])
            : path).Replace(Path.DirectorySeparatorChar, '/');
}

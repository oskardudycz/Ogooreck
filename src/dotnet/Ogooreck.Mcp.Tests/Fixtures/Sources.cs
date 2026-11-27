namespace Ogooreck.Mcp.Tests.Fixtures;

internal static class Sources
{
    public const string Fixture = "Ogooreck.Mcp.Fixture";
    public const string TestingPlatformProject = "Ogooreck.TUnit.Tests";
    public const string FailingTest = "Ogooreck.Mcp.Fixture.ProductTests.RegisteringAProduct_RespondsWithOk";
    public const string HangingTest = "Ogooreck.Mcp.Fixture.ProductTests.WaitingForAResponseThatNeverComes";
    public const string FailingWhen = "src/dotnet/Ogooreck.Mcp.Fixture/ProductTests.cs:28";

    public static readonly string Server = Path.Combine(AppContext.BaseDirectory, "Ogooreck.Mcp.dll");

    public static readonly string SourceDirectory = SolutionDirectory();

    private static string SolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "Ogooreck.slnx")))
            directory = directory.Parent!;
        return directory.FullName;
    }
}

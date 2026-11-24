using Ogooreck.API;
using Xunit;

namespace Ogooreck.Tests;

public class FrameworkIndependenceTests
{
    private static readonly string[] TestFrameworks = ["xunit", "nunit", "mstest", "tunit"];

    [Fact]
    public void OgooreckAssembly_ReferencesNoTestFrameworkAssembly()
    {
        var referenced = typeof(ApiSpecification).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name!);

        Assert.DoesNotContain(referenced, name => TestFrameworks.Any(
            framework => name.Contains(framework, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void OgooreckProject_ListsNoTestFrameworkPackageReference()
    {
        var project = Path.Combine(SolutionDirectory(), "Ogooreck", "Ogooreck.csproj");

        var packages = System.Xml.Linq.XDocument.Load(project)
            .Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? "")
            .ToList();

        Assert.NotEmpty(packages);
        Assert.DoesNotContain(packages, name => TestFrameworks.Any(
            framework => name.Contains(framework, StringComparison.OrdinalIgnoreCase)));
    }

    private static string SolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ogooreck.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Ogooreck.slnx was not found above the test output.");
    }
}

using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Bullseye.Targets;
using static SimpleExec.Command;

var frameworkOption = Array.IndexOf(args, "--framework");
var framework = frameworkOption < 0 ? "net11.0" : args[frameworkOption + 1];
var targets = frameworkOption < 0 ? args : [.. args[..frameworkOption], .. args[(frameworkOption + 2)..]];
const string configuration = "Release";
const string coverageDirectory = "results/coverage";

Target("default", dependsOn: ["compile"]);

Target("clean", () =>
    EnsureDirectoriesDeleted("results", "artifacts"));

Target("compile", dependsOn: ["clean"], () =>
    Run("dotnet", $"build Ogooreck.slnx --configuration {configuration}"));

Target("test-sample-api", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Api.Tests", "Ogooreck"));

Target("test-sample-wolverine", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Wolverine.Tests", "Ogooreck.Wolverine"));

Target("test-sample-kafka", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Kafka.Tests", "Ogooreck.Kafka"));

Target("test-sample-marten", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Marten.Tests", "Ogooreck.Marten"));

Target("test-sample-rabbitmq", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.RabbitMQ.Tests", "Ogooreck.RabbitMQ"));

Target("test-sample-aspire", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Aspire.Tests", "Ogooreck"));

Target("test-business-logic", dependsOn: ["compile"], () => RunTests("Ogooreck.BusinessLogic.Tests", "Ogooreck"));
Target("test-fsharp", dependsOn: ["compile"], () => RunTests("Ogooreck.BusinessLogic.FSharp.Tests", "Ogooreck", "fsproj"));
Target("test-wolverine-fsharp", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Wolverine.FSharp.Tests", "Ogooreck.Wolverine", "fsproj"));
Target("test-nunit", dependsOn: ["compile"], () => RunTests("Ogooreck.NUnit.Tests", "Ogooreck"));
Target("test-mstest", dependsOn: ["compile"], () => RunTests("Ogooreck.MSTest.Tests", "Ogooreck"));
Target("test-tunit", dependsOn: ["compile"], () =>
    Run("dotnet", $"run --no-build --no-restore --configuration {configuration} --framework {framework} --project Ogooreck.TUnit.Tests/Ogooreck.TUnit.Tests.csproj"));
Target("test-library", dependsOn: ["compile"], () => RunTests("Ogooreck.Tests", "Ogooreck"));
Target("test-mcp", dependsOn: ["compile"], () => RunTests("Ogooreck.Mcp.Tests", "Ogooreck.Mcp"));

Target("verify-format", dependsOn: ["restore-tools"], () =>
{
    Run("dotnet", "format Ogooreck.slnx --verify-no-changes");
    Run("dotnet", "fantomas --check .");
});

string[] testTargets = ["test-sample-api", "test-sample-wolverine", "test-sample-kafka", "test-sample-marten", "test-sample-rabbitmq", "test-sample-aspire", "test-business-logic", "test-fsharp", "test-wolverine-fsharp", "test-nunit", "test-mstest", "test-tunit", "test-library", "test-mcp"];

Target("check-coverage", dependsOn: testTargets, () => CheckCoverage(framework));

Target("test", dependsOn: ["verify-format", "check-coverage"]);

Target("pack", dependsOn: ["compile"], forEach: ["./Ogooreck", "./Ogooreck.Wolverine", "./Ogooreck.Marten", "./Ogooreck.Kafka", "./Ogooreck.RabbitMQ", "./Ogooreck.Mcp"], project =>
    Run("dotnet", $"pack {project} -o ./artifacts --configuration Release"));

Target("test-skill", dependsOn: ["pack", "restore-tools"], TestSkill);

Target("restore-tools", () => Run("dotnet", "tool restore"));

Target("docs", dependsOn: ["restore-tools"], () => Run("dotnet", "tool run mdsnippets ../.."));

await RunTargetsAndExitAsync(targets);

void RunTests(string projectName, string library, string extension = "csproj")
{
    var directory = Path.GetFullPath(Path.Combine(coverageDirectory, library));
    var merged = Path.Combine(directory, $"coverage.{framework}.json");
    var mergeWith = File.Exists(merged) ? $" /p:MergeWith=\"{merged}\"" : "";

    Run("dotnet",
        $"test --no-build --no-restore --configuration {configuration} --framework {framework} {projectName}/{projectName}.{extension}" +
        " --blame-hang-timeout 5m --blame-hang-dump-type none" +
        $" /p:CollectCoverage=true /p:Include=\"[{library}]*\" /p:ExcludeByFile=\"**/obj/**/*.cs\" /p:CoverletOutputFormat=\"json%2ccobertura\" /p:CoverletOutput=\"{directory}{Path.DirectorySeparatorChar}\"{mergeWith}");
}

static async Task TestSkill()
{
    const string skill = "ogooreck-api-tests";
    var package = Directory.GetFiles("artifacts", "Ogooreck.*.nupkg")
        .Single(file => Regex.IsMatch(Path.GetFileName(file), @"^Ogooreck\.\d"));
    var version = Path.GetFileNameWithoutExtension(package)["Ogooreck.".Length..];
    var consumer = Directory.CreateTempSubdirectory("ogooreck-skill-").FullName;
    try
    {
        File.WriteAllText(Path.Combine(consumer, "nuget.config"), $"""
            <configuration>
              <packageSources>
                <clear />
                <add key="artifacts" value="{Path.GetFullPath("artifacts")}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
              <packageSourceMapping>
                <packageSource key="artifacts"><package pattern="Ogooreck" /></packageSource>
                <packageSource key="nuget.org"><package pattern="*" /></packageSource>
              </packageSourceMapping>
            </configuration>
            """);
        File.WriteAllText(Path.Combine(consumer, "Consumer.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Ogooreck" Version="{version}" /></ItemGroup>
            </Project>
            """);
        void OwnPackages(IDictionary<string, string?> environment) =>
            environment["NUGET_PACKAGES"] = Path.Combine(consumer, "packages");
        var skills = Path.Combine(consumer, ".agents", "skills");
        var options = $"--source {consumer} --destination {skills}";

        Run("dotnet", "restore", consumer, configureEnvironment: OwnPackages);
        Run("dotnet", $"dotnet-package-skills install {options}", configureEnvironment: OwnPackages);
        var (listed, _) = await ReadAsync("dotnet", $"dotnet-package-skills list {options}", configureEnvironment: OwnPackages);

        using var archive = ZipFile.OpenRead(package);
        using var reader = new StreamReader(archive.GetEntry($"skills/{skill}/SKILL.md")!.Open());
        var packed = await reader.ReadToEndAsync();
        string[] markers = ["<!-- snippet:", "<!-- endSnippet", "<a id='snippet-", "<sup><a href="];

        if (File.ReadAllText(Path.Combine(skills, skill, "SKILL.md")) != packed)
            throw new InvalidOperationException($"dotnet-package-skills install did not copy the {skill} skill of the Ogooreck package.");
        if (packed.Split('\n').Any(line => markers.Any(line.StartsWith)))
            throw new InvalidOperationException($"The packed {skill} skill still has MarkdownSnippets markers.");
        if (!listed.Contains($"{skill} (Ogooreck {version})"))
            throw new InvalidOperationException($"dotnet-package-skills list does not show the {skill} skill:\n{listed}");
    }
    finally
    {
        Directory.Delete(consumer, true);
    }
}

static void CheckCoverage(string framework)
{
    string[] libraries = ["Ogooreck", "Ogooreck.Wolverine", "Ogooreck.Marten", "Ogooreck.Kafka", "Ogooreck.RabbitMQ", "Ogooreck.Mcp"];
    var notCovered = libraries.Where(library => !IsFullyCovered(library, framework)).ToArray();

    if (notCovered.Length > 0)
        throw new InvalidOperationException($"{string.Join(", ", notCovered)} need 100% line and branch coverage from their own test projects. Remove branches no public call can reach, and cover the rest through the public API.");
}

static bool IsFullyCovered(string library, string framework)
{
    var report = XDocument.Load(Path.Combine(coverageDirectory, library, $"coverage.{framework}.cobertura.xml")).Root!;
    var lines = Counts(report, "lines");
    var branches = Counts(report, "branches");

    Console.WriteLine($"{library} coverage: lines {lines.Covered}/{lines.Valid}, branches {branches.Covered}/{branches.Valid}");

    var gaps = report.Descendants("line")
        .Where(line => (int)line.Attribute("hits")! == 0 || IsPartlyCovered(line))
        .Select(line => $"{line.Ancestors("class").First().Attribute("filename")!.Value}:{line.Attribute("number")!.Value}")
        .Distinct();

    foreach (var gap in gaps)
        Console.WriteLine($"  not fully covered: {gap}");

    return lines.Covered == lines.Valid && branches.Covered == branches.Valid;
}

static (int Covered, int Valid) Counts(XElement report, string name) =>
    (int.Parse(report.Attribute($"{name}-covered")!.Value, CultureInfo.InvariantCulture),
        int.Parse(report.Attribute($"{name}-valid")!.Value, CultureInfo.InvariantCulture));

static bool IsPartlyCovered(XElement line) =>
    line.Attribute("branch")?.Value == "True" && !line.Attribute("condition-coverage")!.Value.StartsWith("100%");

static void EnsureDirectoriesDeleted(params string[] paths)
{
    foreach (var path in paths)
    {
        if (!Directory.Exists(path))
            continue;
        var dir = new DirectoryInfo(path);
        DeleteDirectory(dir);
    }
}

static void DeleteDirectory(DirectoryInfo baseDir)
{
    baseDir.Attributes = FileAttributes.Normal;
    foreach (var childDir in baseDir.GetDirectories())
        DeleteDirectory(childDir);

    foreach (var file in baseDir.GetFiles())
        file.IsReadOnly = false;

    baseDir.Delete(true);
}

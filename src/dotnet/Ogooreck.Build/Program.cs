using System.Globalization;
using System.Xml.Linq;
using static Bullseye.Targets;
using static SimpleExec.Command;

const string framework = "net10.0";
const string configuration = "Release";
const string coverageDirectory = "results/coverage";

Target("default", dependsOn: ["compile"]);

Target("clean", () =>
    EnsureDirectoriesDeleted("results", "artifacts"));

Target("compile", dependsOn: ["clean"], () =>
    Run("dotnet", $"build Ogooreck.slnx --configuration {configuration}"));

Target("test-sample-api", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Api.Tests"));

Target("test-sample-wolverine", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Wolverine.Tests"));

Target("test-sample-kafka", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Kafka.Tests"));

Target("test-sample-marten", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Marten.Tests"));

Target("test-sample-rabbitmq", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.RabbitMQ.Tests"));

Target("test-sample-aspire", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Aspire.Tests"));

Target("test-business-logic", dependsOn: ["compile"], () => RunTests("Ogooreck.BusinessLogic.Tests"));
Target("test-fsharp", dependsOn: ["compile"], () => RunTests("Ogooreck.BusinessLogic.FSharp.Tests", "fsproj"));
Target("test-wolverine-fsharp", dependsOn: ["compile"], () => RunTests("Ogooreck.Wolverine.FSharp.Tests", "fsproj"));
Target("test-nunit", dependsOn: ["compile"], () => RunTests("Ogooreck.NUnit.Tests"));
Target("test-mstest", dependsOn: ["compile"], () => RunTests("Ogooreck.MSTest.Tests"));
Target("test-tunit", dependsOn: ["compile"], () =>
    Run("dotnet", $"run --no-build --no-restore --configuration {configuration} --framework {framework} --project Ogooreck.TUnit.Tests/Ogooreck.TUnit.Tests.csproj"));
Target("test-library", dependsOn: ["compile"], () => RunTests("Ogooreck.Tests"));
Target("test-mcp", dependsOn: ["compile"], () => RunTests("Ogooreck.Mcp.Tests"));

Target("verify-format", dependsOn: ["restore-tools"], () =>
{
    Run("dotnet", "format Ogooreck.slnx --verify-no-changes");
    Run("dotnet", "fantomas --check .");
});

string[] testTargets = ["test-sample-api", "test-sample-wolverine", "test-sample-kafka", "test-sample-marten", "test-sample-rabbitmq", "test-sample-aspire", "test-business-logic", "test-fsharp", "test-wolverine-fsharp", "test-nunit", "test-mstest", "test-tunit", "test-library", "test-mcp"];

Target("check-coverage", dependsOn: testTargets, CheckCoverage);

Target("test", dependsOn: ["verify-format", "check-coverage"]);

Target("pack", dependsOn: ["compile"], forEach: ["./Ogooreck", "./Ogooreck.Wolverine", "./Ogooreck.Marten", "./Ogooreck.Kafka", "./Ogooreck.RabbitMQ"], project =>
    Run("dotnet", $"pack {project} -o ./artifacts --configuration Release"));

Target("restore-tools", () => Run("dotnet", "tool restore"));

Target("docs", dependsOn: ["restore-tools"], () => Run("dotnet", "tool run mdsnippets ../.."));

await RunTargetsAndExitAsync(args);

static void RunTests(string projectName, string extension = "csproj")
{
    var directory = Path.GetFullPath(coverageDirectory);
    var merged = Path.Combine(directory, "coverage.json");
    var mergeWith = File.Exists(merged) ? $" /p:MergeWith=\"{merged}\"" : "";

    Run("dotnet",
        $"test --no-build --no-restore --configuration {configuration} --framework {framework} {projectName}/{projectName}.{extension}" +
        $" /p:CollectCoverage=true /p:Include=\"[Ogooreck]*%2c[Ogooreck.Wolverine]*%2c[Ogooreck.Marten]*%2c[Ogooreck.Kafka]*%2c[Ogooreck.RabbitMQ]*%2c[Ogooreck.Mcp]*\" /p:ExcludeByFile=\"**/obj/**/*.cs\" /p:CoverletOutputFormat=\"json%2ccobertura\" /p:CoverletOutput=\"{directory}{Path.DirectorySeparatorChar}\"{mergeWith}");
}

static void CheckCoverage()
{
    var report = XDocument.Load(Path.Combine(coverageDirectory, "coverage.cobertura.xml")).Root!;
    var lines = Counts(report, "lines");
    var branches = Counts(report, "branches");

    Console.WriteLine($"Ogooreck, Ogooreck.Wolverine, Ogooreck.Marten, Ogooreck.Kafka, Ogooreck.RabbitMQ and Ogooreck.Mcp coverage: lines {lines.Covered}/{lines.Valid}, branches {branches.Covered}/{branches.Valid}");

    if (lines.Covered == lines.Valid && branches.Covered == branches.Valid)
        return;

    var gaps = report.Descendants("line")
        .Where(line => (int)line.Attribute("hits")! == 0 || IsPartlyCovered(line))
        .Select(line => $"{line.Ancestors("class").First().Attribute("filename")!.Value}:{line.Attribute("number")!.Value}")
        .Distinct();

    foreach (var gap in gaps)
        Console.WriteLine($"  not fully covered: {gap}");

    throw new InvalidOperationException("The Ogooreck libraries and Ogooreck.Mcp need 100% line and branch coverage from the test projects. Remove branches no public call can reach, and cover the rest through the public API.");
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

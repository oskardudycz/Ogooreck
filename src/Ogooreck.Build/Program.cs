using static Bullseye.Targets;
using static SimpleExec.Command;

const string framework = "net10.0";
const string configuration = "Release";

Target("default", dependsOn: ["compile"]);

Target("clean", () =>
    EnsureDirectoriesDeleted("results", "artifacts"));

Target("compile", dependsOn: ["clean"], () =>
    Run("dotnet", $"build src/Ogooreck.slnx --configuration {configuration}"));

Target("test-sample-api", dependsOn: ["compile"], () =>
    RunTests("Ogooreck.Sample.Api.Tests"));

Target("test-business-logic", dependsOn: ["compile"], () => RunTests("Ogooreck.Sample.BusinessLogic.Tests"));
Target("test-fsharp", dependsOn: ["compile"], () => RunTests("Ogooreck.Sample.BusinessLogic.FSharp.Tests", "fsproj"));
Target("test-library", dependsOn: ["compile"], () => RunTests("Ogooreck.Tests"));

Target("test", dependsOn: ["test-sample-api", "test-business-logic", "test-fsharp", "test-library"]);

Target("pack", dependsOn: ["compile"], forEach: ["./src/Ogooreck"], project =>
    Run("dotnet", $"pack {project} -o ./artifacts --configuration Release"));

Target("restore-tools", () => Run("dotnet", "tool restore"));

Target("docs", dependsOn: ["restore-tools"], () => Run("dotnet", "tool run mdsnippets"));

await RunTargetsAndExitAsync(args);

static void RunTests(string projectName, string extension = "csproj") =>
    Run("dotnet", $"test --no-build --no-restore --configuration {configuration} --framework {framework} src/{projectName}/{projectName}.{extension}");

static void EnsureDirectoriesDeleted(params string[] paths)
{
    foreach (var path in paths)
    {
        if (!Directory.Exists(path)) continue;
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

using static Bullseye.Targets;
using static SimpleExec.Command;

const string framework = "net6.0";
const string configuration = "Release";

Target("default", DependsOn("compile"));

Target("clean", () =>
    EnsureDirectoriesDeleted("results", "artifacts"));

Target("compile", DependsOn("clean"), () =>
    Run("dotnet",  $"build src/Ogooreck.sln --framework {framework} --configuration {configuration}"));

Target("test-sample-api", DependsOn("compile"), () =>
    RunTests("Ogooreck.Sample.Api.Tests"));

Target("test", DependsOn("test-sample-api"));

Target("pack", DependsOn("compile"), ForEach("./src/Ogooreck"), project =>
    Run("dotnet", $"pack {project} -o ./artifacts --configuration Release"));

Target("install-mdsnippets", IgnoreIfFailed(() =>
    Run("dotnet", $"tool install -g MarkdownSnippets.Tool")
));

Target("docs", DependsOn("install-mdsnippets"), () => {
    // Run docs site
    Run("mdsnippets");
});

await RunTargetsAndExitAsync(args);

static void RunTests(string projectName, string directoryName = "src") =>
    Run("dotnet", $"test --no-build --no-restore --configuration {configuration} --framework {framework} {directoryName}/{projectName}/{projectName}.csproj");

Action IgnoreIfFailed(Action action) => () =>
{
    try
    {
        action();
    }
    catch (Exception exception)
    {
        Console.WriteLine(exception.Message);
    }
};

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

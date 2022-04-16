using static Bullseye.Targets;
using static SimpleExec.Command;

Target("install-mdsnippets", IgnoreIfFailed(() =>
    Run("dotnet", $"tool install -g MarkdownSnippets.Tool")
));

Target("docs", DependsOn("install-mdsnippets"), () => {
    // Run docs site
    Run("mdsnippets");
});

await RunTargetsAndExitAsync(args);

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

$ErrorActionPreference = "Stop";
$version = dotnet --version;
if ($version.StartsWith("7.")) {
    $target_framework="net7.0"
} else {
    Write-Output "BUILD FAILURE: .NET 7 SDK required to run build"
    exit 1
}

dotnet run --project src/Ogooreck.Build/Ogooreck.Build.csproj -f $target_framework -c Release -- $args

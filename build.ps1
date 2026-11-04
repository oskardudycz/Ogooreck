$ErrorActionPreference = "Stop";
$version = dotnet --version;
if ($version.StartsWith("10.")) {
    $target_framework="net10.0"
} else {
    Write-Output "BUILD FAILURE: .NET 10 SDK required to run build"
    exit 1
}

dotnet run --project src/Ogooreck.Build/Ogooreck.Build.csproj -f $target_framework -c Release -- $args

exit $LASTEXITCODE

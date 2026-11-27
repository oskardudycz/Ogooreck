$ErrorActionPreference = "Stop";
$version = dotnet --version;
if ($version.StartsWith("11.")) {
    $target_framework="net10.0"
} else {
    Write-Output "BUILD FAILURE: .NET 11 SDK required to run build"
    exit 1
}

dotnet run --project Ogooreck.Build/Ogooreck.Build.csproj -f $target_framework -c Release -- $args

exit $LASTEXITCODE

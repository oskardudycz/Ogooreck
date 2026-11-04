@echo off
FOR /f %%v IN ('dotnet --version') DO set version=%%v
set target_framework=
IF "%version:~0,3%"=="10." (set target_framework=net10.0)

IF [%target_framework%]==[] (
    echo "BUILD FAILURE: .NET 10 SDK required to run build"
    exit /b 1
)

dotnet run --project src/Ogooreck.Build/Ogooreck.Build.csproj -f %target_framework% -c Release -- %*

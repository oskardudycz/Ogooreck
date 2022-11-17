#!/usr/bin/env bash
set -euo pipefail

version="$(dotnet --version)"
if [[ $version = 7.* ]]; then
  target_framework="net7.0"
else
  echo "BUILD FAILURE: .NET 7 SDK required to run build"
  exit 1
fi

dotnet run --project src/Ogooreck.Build/Ogooreck.Build.csproj -f $target_framework -c Release -- "$@"

#!/usr/bin/env bash
set -euo pipefail

version="$(dotnet --version)"
if [[ $version = 10.* ]]; then
  target_framework="net10.0"
else
  echo "BUILD FAILURE: .NET 10 SDK required to run build"
  exit 1
fi

dotnet run --project src/Ogooreck.Build/Ogooreck.Build.csproj -f $target_framework -c Release -- "$@"

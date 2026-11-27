#!/usr/bin/env bash
set -euo pipefail

version="$(dotnet --version)"
if [[ $version = 11.* ]]; then
  target_framework="net10.0"
else
  echo "BUILD FAILURE: .NET 11 SDK required to run build, found $version"
  exit 1
fi

dotnet run --project Ogooreck.Build/Ogooreck.Build.csproj -f $target_framework -c Release -- "$@"

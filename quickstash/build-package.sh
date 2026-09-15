#!/bin/sh
# Builds the DLL and zips a Thunderstore package into dist/
set -e
cd "$(dirname "$0")"
dotnet build -c Release
VER=$(grep version_number package/manifest.json | sed 's/[^0-9.]//g')
mkdir -p dist
rm -f dist/QuickStash-$VER.zip
cd package && zip -q ../dist/QuickStash-$VER.zip manifest.json README.md CHANGELOG.md icon.png && cd ..
zip -qj dist/QuickStash-$VER.zip bin/Release/QuickStash.dll
echo "dist/QuickStash-$VER.zip"

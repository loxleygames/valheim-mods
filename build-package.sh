#!/bin/sh
# Builds the DLL and zips a Thunderstore package into dist/
set -e
cd "$(dirname "$0")"
dotnet build -c Release
VER=$(grep version_number package/manifest.json | sed 's/[^0-9.]//g')
mkdir -p dist
rm -f dist/Moorings-$VER.zip
cd package && zip -q ../dist/Moorings-$VER.zip manifest.json README.md CHANGELOG.md icon.png && cd ..
zip -qj dist/Moorings-$VER.zip bin/Release/Moorings.dll
echo "dist/Moorings-$VER.zip"

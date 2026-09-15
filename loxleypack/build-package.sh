#!/bin/sh
# Zips the modpack (manifest only, no DLL) into dist/
set -e
cd "$(dirname "$0")"
VER=$(grep version_number package/manifest.json | sed 's/[^0-9.]//g')
mkdir -p dist
rm -f dist/LoxleyPack-$VER.zip
cd package && zip -q ../dist/LoxleyPack-$VER.zip manifest.json README.md CHANGELOG.md $( [ -f icon.png ] && echo icon.png ) && cd ..
echo "dist/LoxleyPack-$VER.zip"

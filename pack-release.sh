#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Release}"
VERSION="${VERSION:-0.1.2}"
SKIP_BUILD="${SKIP_BUILD:-0}"

cd "$(dirname "$0")"

if [[ "$SKIP_BUILD" != "1" ]]; then
  dotnet build ./PerkShop.csproj -c "$CONFIGURATION"
fi

DLL="./bin/$CONFIGURATION/net6.0/PerkShop.dll"
if [[ ! -f "$DLL" ]]; then
  echo "Missing $DLL. Build the project first." >&2
  exit 1
fi

for f in manifest.json README.md CHANGELOG.md icon.png; do
  if [[ ! -f "$f" ]]; then
    echo "Missing required Thunderstore file: $f" >&2
    exit 1
  fi
done

rm -rf ./dist/package
mkdir -p ./dist/package

cp "$DLL" ./dist/package/PerkShop.dll
cp ./manifest.json ./dist/package/manifest.json
cp ./README.md ./dist/package/README.md
cp ./CHANGELOG.md ./dist/package/CHANGELOG.md
cp ./icon.png ./dist/package/icon.png

ZIP="./dist/PerkShop-$VERSION-Thunderstore.zip"
rm -f "$ZIP"
( cd ./dist/package && zip -r "../$(basename "$ZIP")" . )

echo "Created $ZIP"

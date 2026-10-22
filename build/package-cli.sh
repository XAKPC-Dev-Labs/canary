#!/usr/bin/env bash
# Publishes the NativeAOT canary binary for one target and packs the release asset.
# Usage: build/package-cli.sh <rid> <version>      e.g. build/package-cli.sh osx-arm64 0.1.0
# Output: artifacts/canary_<os>_<arch>.tar.gz (.zip for Windows). Asset names carry no version,
# so https://github.com/<repo>/releases/latest/download/<asset> always points to the newest build.
set -euo pipefail

rid="$1"
version="$2"

case "$rid" in
  osx-arm64)   os=darwin;  arch=arm64 ;;
  osx-x64)     os=darwin;  arch=x64 ;;
  linux-x64)   os=linux;   arch=x64 ;;
  linux-arm64) os=linux;   arch=arm64 ;;
  win-x64)     os=windows; arch=x64 ;;
  *) echo "unsupported rid: $rid" >&2; exit 1 ;;
esac

root="$(cd "$(dirname "$0")/.." && pwd)"
publish="$root/artifacts/publish/$rid"
mkdir -p "$root/artifacts"
rm -rf "$publish"

dotnet publish "$root/src/Xakpc.McpCanary.Cli/Xakpc.McpCanary.Cli.csproj" \
  -c Release -r "$rid" -o "$publish" -p:Version="$version"

asset="canary_${os}_${arch}"
if [ "$os" = windows ]; then
  rm -f "$root/artifacts/$asset.zip"
  # Relative paths: Git Bash paths like /c/... mean nothing to PowerShell.
  (cd "$publish" && pwsh -NoProfile -Command "Compress-Archive -Path canary.exe -DestinationPath ../../$asset.zip")
  binary="$publish/canary.exe"
  file="$asset.zip"
else
  tar -czf "$root/artifacts/$asset.tar.gz" -C "$publish" canary
  binary="$publish/canary"
  file="$asset.tar.gz"
fi

# The binary must run and report the tag version, or the release is wrong.
reported="$("$binary" --version)"
if [ "$reported" != "$version" ]; then
  echo "canary --version printed '$reported', expected '$version'" >&2
  exit 1
fi
echo "artifacts/$file ($(wc -c < "$root/artifacts/$file") bytes, canary $reported)"

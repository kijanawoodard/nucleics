#!/bin/sh
# Cloudflare Pages build:   build command `bash build.sh`   output directory `output`
# Installs the exact pinned .NET SDK into ./dotnet (nothing global is used), then exports the site to ./output.
# Needs on the build image: sh/bash, curl, tar, gzip. Exits non-zero on ANY failure (set -e).
set -e

DOTNET_VERSION="11.0.100-rc.1.26425.128"   # keep in sync with global.json

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1   # no libicu needed to run the SDK/app (the app is InvariantGlobalization anyway)
export DOTNET_MULTILEVEL_LOOKUP=0                # ignore any other dotnet on the image

cd "$(dirname "$0")"
ROOT="$(pwd)"
DOTNET_DIR="$ROOT/dotnet"
export DOTNET_ROOT="$DOTNET_DIR"

# Install the pinned SDK (exact --version, not a floating channel); skip if this exact version is already in ./dotnet.
if [ "$("$DOTNET_DIR/dotnet" --version 2>/dev/null || true)" != "$DOTNET_VERSION" ]; then
  curl -sSfL https://dot.net/v1/dotnet-install.sh > dotnet-install.sh
  chmod +x dotnet-install.sh
  ./dotnet-install.sh --version "$DOTNET_VERSION" --install-dir "$DOTNET_DIR"
fi

# From here on only ./dotnet/dotnet is used.
"$DOTNET_DIR/dotnet" --version

rm -rf output
"$DOTNET_DIR/dotnet" run --project src/Nucleics.Web -c Release -- export

# Belt and braces: the export must have produced these, or the build fails.
for f in output/index.html output/404.html output/sitemap.xml output/robots.txt; do
  [ -s "$f" ] || { echo "build.sh: missing required output file: $f" >&2; exit 1; }
done
echo "build.sh: OK -> $ROOT/output"

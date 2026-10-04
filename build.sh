#!/bin/sh
# Cloudflare Pages build.   Build command: ./build.sh   (or: bash build.sh / sh build.sh)   Output directory: output
#
# NOTE: a BARE `build.sh` as the build command does not work: the current directory is not on PATH, so the shell
# answers "build.sh: not found" (exit 127). Use `./build.sh`, which also relies on the committed exec bit (100755).
#
# Installs the exact pinned .NET SDK into ./dotnet (nothing global is used), then exports the site to ./output.
# Needs on the build image: a POSIX shell, curl, tar, gzip, outbound HTTPS (dot.net, builds.dotnet.microsoft.com,
# api.nuget.org). Exits non-zero on ANY failure (set -e) and says which step failed.
set -e

DOTNET_VERSION="11.0.100-rc.1.26425.128"   # keep in sync with global.json

cd "$(dirname "$0")"
ROOT="$(pwd)"
DOTNET_DIR="$ROOT/dotnet"
STEP="startup"
trap 'rc=$?; if [ "$rc" -ne 0 ]; then echo "build.sh: FAILED (exit $rc) during step: $STEP" >&2; fi' EXIT

# --- environment: everything the build writes stays inside the checkout; works with HOME unset or unwritable -------------
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1   # no libicu needed to run the SDK/app (the app is InvariantGlobalization anyway)
export DOTNET_MULTILEVEL_LOOKUP=0                # ignore any other dotnet on the image
export DOTNET_ROOT="$DOTNET_DIR"
export DOTNET_CLI_HOME="$ROOT/.dotnet-home"      # replaces ~/.dotnet (first-run sentinel, tool dirs)
export NUGET_PACKAGES="$ROOT/.nuget/packages"    # replaces ~/.nuget/packages
export DOTNET_CLI_USE_MSBUILD_SERVER=0           # no lingering build servers / node reuse: lower memory, clean exit
export MSBUILDDISABLENODEREUSE=1
export UseSharedCompilation=false
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"
if [ -z "${HOME:-}" ] || [ ! -w "${HOME:-/nonexistent}" ]; then
  export HOME="$ROOT/.home"; mkdir -p "$HOME"
fi

# --- diagnostics (never fatal) so a failing CI log is self-explanatory -------------------------------------------------------
STEP="diagnostics"
echo "== build.sh diagnostics =="
echo "date    : $(date -u 2>/dev/null || true)"
echo "uname   : $(uname -a 2>/dev/null || true)"
echo "user    : $(id 2>/dev/null || true)   HOME=$HOME   pwd=$ROOT"
echo "shell   : $(readlink -f /bin/sh 2>/dev/null || echo /bin/sh)"
echo "glibc   : $( (ldd --version 2>&1 | head -1) 2>/dev/null || true)"
echo "curl    : $( (curl --version 2>&1 | head -1) 2>/dev/null || echo MISSING)"
for t in tar gzip; do command -v "$t" >/dev/null 2>&1 || echo "WARNING: '$t' not found on PATH"; done
echo "disk    : $( (df -h . 2>/dev/null | tail -1) || true)"
echo "memory  : $( (free -m 2>/dev/null | sed -n 2p) || true)"
echo "cpus    : $( (nproc 2>/dev/null) || true)"
echo "env vars present (names only): $(env | sed 's/=.*//' | grep -E '^(DOTNET|NUGET|NODE|SKIP_|CI$|CF_PAGES|HOME$)' | sort | tr '\n' ' ')"
for u in https://dot.net/v1/dotnet-install.sh https://builds.dotnet.microsoft.com https://api.nuget.org/v3/index.json; do
  echo "reach   : $u -> $(curl -sS -o /dev/null -I -L --max-time 20 -w '%{http_code}' "$u" 2>&1 || true)"
done
command -v curl >/dev/null 2>&1 || { echo "build.sh: curl is required" >&2; exit 1; }

# --- 1. install the pinned SDK (exact --version, not a channel); skipped if ./dotnet already has this exact version ------------
STEP="install .NET SDK $DOTNET_VERSION"
echo "== [1/3] $STEP =="
if [ "$("$DOTNET_DIR/dotnet" --version 2>/dev/null || true)" != "$DOTNET_VERSION" ]; then
  attempt=1
  while :; do
    if curl -sSfL --retry 3 --retry-delay 2 https://dot.net/v1/dotnet-install.sh > dotnet-install.sh \
       && chmod +x dotnet-install.sh \
       && ./dotnet-install.sh --version "$DOTNET_VERSION" --install-dir "$DOTNET_DIR"; then
      break
    fi
    [ "$attempt" -ge 3 ] && { echo "build.sh: SDK install failed after $attempt attempts" >&2; exit 1; }
    attempt=$((attempt + 1)); echo "build.sh: install failed, retrying ($attempt/3)..." >&2; sleep 5
  done
fi
# From here on only ./dotnet/dotnet is used.
echo "sdk     : $("$DOTNET_DIR/dotnet" --version)"

# --- 2. export ---------------------------------------------------------------------------------------------------------------
STEP="export site (dotnet run ... -- export)"
echo "== [2/3] $STEP =="
rm -rf output
"$DOTNET_DIR/dotnet" run --project src/Nucleics.Web -c Release --no-launch-profile -- export

# --- 3. belt and braces: the export must have produced these, or the build fails --------------------------------------------
STEP="verify output"
echo "== [3/3] $STEP =="
for f in output/index.html output/404.html output/sitemap.xml output/robots.txt; do
  [ -s "$f" ] || { echo "build.sh: missing required output file: $f" >&2; exit 1; }
done
STEP="done"
echo "build.sh: OK -> $ROOT/output"

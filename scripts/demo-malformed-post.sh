#!/usr/bin/env bash
# Shows that a malformed post (here: the typo `dratf:` for `draft:`) fails export and check with exit 1 and a message naming file and line.
# Works on a temp content folder via --Blog:ContentPath; nothing in the repo changes. Needs `dotnet` on PATH.
set -u
cd "$(dirname "$0")/.."
T=$(mktemp -d); mkdir "$T/posts"
printf -- '---\ntitle: Bad\ndate: 2026-10-01\ndratf: true\n---\nbody\n' > "$T/posts/bad.md"
for mode in export check; do
  echo "=== $mode"
  dotnet run --project src/Nucleics.Web -c Release -- $mode --output "$T/out" --Blog:ContentPath="$T/posts" 2>&1 | grep -E '^\[kaizen\] FAILED' | cut -c1-400
  echo "exit=${PIPESTATUS[0]}"
done

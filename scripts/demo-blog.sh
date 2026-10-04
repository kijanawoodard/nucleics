#!/usr/bin/env bash
# Blog demo: the shipped sample post is a draft, so production exports no /blog. This publishes a copy of it into a temp content
# folder (Blog:ContentPath override), exports to a temp dir, and shows /blog, the post and the sitemap. Nothing in the repo changes.
# Usage: scripts/demo-blog.sh [outdir]   (needs `dotnet` on PATH; run from anywhere)
set -eu
cd "$(dirname "$0")/.."
T=$(mktemp -d); OUT=${1:-$T/out}
mkdir -p "$T/posts"
sed '/^draft: true$/d' src/Nucleics.Web/content/posts/why-nuclear.md > "$T/posts/why-nuclear.md"
echo "=== published sample post -> export"
dotnet run --project src/Nucleics.Web -c Release -- export --output "$OUT" --Blog:ContentPath="$T/posts" 2>&1 | grep -E '^\[Export\]|skip|ok  '
echo "=== files"; (cd "$OUT" && find . -path ./assets -prune -o -type f -print | sort)
echo "=== sitemap"; grep '<loc>' "$OUT/sitemap.xml"
echo "output: $OUT"

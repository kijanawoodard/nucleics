#!/usr/bin/env python3
"""Every exported page (except 404.html) must have canonical + og:title/og:url + description; sitemap must list exactly
the exported pages; robots.txt must reference the sitemap. usage: scripts/verify-seo.py [output-dir]"""
import re, sys, json
from pathlib import Path
root = Path(sys.argv[1] if len(sys.argv) > 1 else "output")
bad = []
pages = [p for p in sorted(root.rglob("index.html"))]
sm = (root / "sitemap.xml").read_text()
locs = set(re.findall(r"<loc>([^<]+)</loc>", sm))
expected = set()
for p in pages:
    h = p.read_text(); rel = p.relative_to(root).parent.as_posix()
    url_path = "/" if rel == "." else f"/{rel}/"
    for name, pat in [("canonical", r'<link rel="canonical" href="([^"]+)"'), ("og:title", r'property="og:title"'),
                      ("og:url", r'property="og:url"'), ("description", r'<meta name="description"'), ("ld+json", r'<script type="application/ld\+json">')]:
        if not re.search(pat, h): bad.append(f"{p}: missing {name}")
    m = re.search(r'<link rel="canonical" href="([^"]+)"', h)
    if m and not m.group(1).endswith(url_path): bad.append(f"{p}: canonical {m.group(1)} does not match path {url_path}")
    for name, pat in [("icon /favicon.ico", r'<link rel="icon" href="/favicon.ico"'), ("svg icon", r'<link rel="icon" type="image/svg(?:\+|&#x2B;)xml"'),
                      ("dark svg icon", r'<link rel="icon"[^>]*media="\(prefers-color-scheme: dark\)"'), ("apple-touch-icon", r'<link rel="apple-touch-icon"'),
                      ("original 32px png icon", r'<link rel="icon" type="image/png" sizes="32x32"'), ("original 64px png icon", r'<link rel="icon" type="image/png" sizes="64x64"')]:
        if not re.search(pat, h): bad.append(f"{p}: missing {name} link")
    for blob in re.findall(r'<script type="application/ld\+json">(.*?)</script>', h, re.S):
        try: json.loads(blob)
        except Exception as e: bad.append(f"{p}: invalid JSON-LD: {e}")
    expected.add("https://nucleics.org" + url_path)
if locs != expected: bad.append(f"sitemap mismatch: only-in-sitemap={sorted(locs-expected)} only-in-output={sorted(expected-locs)}")
if "Sitemap: https://nucleics.org/sitemap.xml" not in (root / "robots.txt").read_text(): bad.append("robots.txt lacks Sitemap line")
if not (root / "favicon.ico").exists(): bad.append("favicon.ico missing (must be unfingerprinted at the output root)")
if not (root / "404.html").exists(): bad.append("404.html missing")
print(f"pages checked: {len(pages)}; sitemap urls: {len(locs)}")
for b in bad: print("  FAIL", b)
print("RESULT:", "PASS" if not bad else "FAIL"); sys.exit(1 if bad else 0)

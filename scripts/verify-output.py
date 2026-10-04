#!/usr/bin/env python3
"""Serve output/ with a PLAIN static file server (python http.server, no app code) and check that every URL
referenced by every exported HTML page returns HTTP 200: href/src/srcset/poster, <link>, <script>, <img>, <source>,
importmap targets, and <a> links. External (other-host) URLs are listed as skipped.

usage: scripts/verify-output.py [output-dir] [--site https://nucleics.org]
exit code 0 = all 200, 1 = failures."""
import functools, http.server, json, re, socketserver, sys, threading, urllib.error, urllib.parse, urllib.request
from html.parser import HTMLParser
from pathlib import Path

args = [a for a in sys.argv[1:] if not a.startswith("--")]
site = "https://nucleics.org"
if "--site" in sys.argv:
    site = sys.argv[sys.argv.index("--site") + 1]; args = [a for a in args if a != site]
root = Path(args[0] if args else "output").resolve()
if not root.is_dir():
    print(f"no such dir: {root}"); sys.exit(2)

class Quiet(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *a): pass
    def handle(self):
        try: super().handle()
        except (BrokenPipeError, ConnectionResetError): pass

handler = functools.partial(Quiet, directory=str(root))
socketserver.TCPServer.allow_reuse_address = True
srv = socketserver.ThreadingTCPServer(("127.0.0.1", 0), handler)
port = srv.server_address[1]
threading.Thread(target=srv.serve_forever, daemon=True).start()
origin = f"http://127.0.0.1:{port}"

class P(HTMLParser):
    def __init__(self):
        super().__init__(); self.refs = []; self.base = None; self._imap = False; self._buf = ""
    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "base" and "href" in a: self.base = a["href"]
        for k in ("href", "src", "poster"):
            if k in a and a[k]: self.refs.append((tag, k, a[k]))
        if "srcset" in a and a["srcset"]:
            for part in a["srcset"].split(","):
                self.refs.append((tag, "srcset", part.strip().split()[0]))
        if tag == "script" and a.get("type") == "importmap": self._imap = True; self._buf = ""
    def handle_data(self, d):
        if self._imap: self._buf += d
    def handle_endtag(self, tag):
        if tag == "script" and self._imap:
            self._imap = False
            try:
                for v in json.loads(self._buf).get("imports", {}).values(): self.refs.append(("importmap", "imports", v))
            except Exception as e: self.refs.append(("importmap", "INVALID-JSON", str(e)))

cache, results, skipped = {}, [], []
def get(url):
    if url in cache: return cache[url]
    try:
        with urllib.request.urlopen(url, timeout=10) as r: r.read(); code = r.status
    except urllib.error.HTTPError as e: code = e.code
    except Exception as e: code = f"ERR {e}"
    cache[url] = code; return code

pages = sorted(root.rglob("*.html"))
site_host = urllib.parse.urlparse(site).netloc
for page in pages:
    rel = "/" + page.relative_to(root).as_posix()
    served = rel[:-len("index.html")] if rel.endswith("/index.html") else rel   # how a static host serves it
    page_url = origin + served
    code = get(page_url)
    results.append((rel, page_url, "page", code))
    p = P(); p.feed(page.read_text(encoding="utf-8"))
    base = urllib.parse.urljoin(page_url, p.base) if p.base else page_url
    for tag, attr, raw in p.refs:
        if re.match(r"^(mailto|tel|javascript|data):", raw, re.I) or raw.startswith("#"): continue
        absu = urllib.parse.urljoin(base, raw)
        u = urllib.parse.urlparse(absu)
        if u.netloc == site_host: absu = origin + u.path   # own absolute URLs (canonical, og:url) map onto the local server
        elif u.netloc != urllib.parse.urlparse(origin).netloc:
            skipped.append((rel, absu)); continue
        absu = absu.split("#")[0]
        results.append((rel, absu, f"{tag}[{attr}]", get(absu)))

bad = [r for r in results if r[3] != 200]
uniq = {r[1] for r in results}
print(f"served {root} via python http.server on {origin}")
print(f"html pages: {len(pages)}")
for pg in pages: print("   ", pg.relative_to(root))
print(f"references checked: {len(results)} ({len(uniq)} unique URLs); skipped external: {len(skipped)}")
print(f"HTTP 200: {len(results) - len(bad)}   NOT 200: {len(bad)}")
for rel, u, kind, code in bad: print(f"  FAIL {code}  {kind}  {u}   (in {rel})")
for rel, u in sorted(set(skipped)): print(f"  skipped external {u} (in {rel})")
print("RESULT:", "PASS" if not bad else "FAIL")
srv.shutdown(); sys.exit(1 if bad else 0)

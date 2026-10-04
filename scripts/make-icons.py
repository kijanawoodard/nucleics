#!/usr/bin/env python3
"""Generate favicon.ico (16/32/48) and apple-touch-icon.png (180) from the repo's mark.svg.
Renders the SVG with headless Chrome at 2x (2048 px), crops a square around the mark (the SVG has a wide cream margin),
thickens the hairline strokes per target size (3 px at 1024 would vanish at 16 px), and downsamples with LANCZOS.
Usage: scripts/make-icons.py   (needs google-chrome and Pillow; output is committed, this script is NOT part of build.sh)"""
import re, subprocess, sys, tempfile, pathlib
from PIL import Image, ImageChops
import numpy as np

root = pathlib.Path(__file__).resolve().parent.parent if (pathlib.Path(__file__).resolve().parent.name == "scripts") else pathlib.Path("/workspace/nucleics-spike")
wwwroot = root / "src/Nucleics.Web/wwwroot"
svg_src = (wwwroot / "assets/mark.svg").read_text()
BG = (244, 241, 234)

def render(svg_text, scale=2):
    with tempfile.TemporaryDirectory() as t:
        f = pathlib.Path(t) / "m.svg"; f.write_text(svg_text); out = pathlib.Path(t) / "m.png"
        subprocess.run(["google-chrome", "--headless=new", "--no-sandbox", "--disable-gpu", "--hide-scrollbars",
                        f"--force-device-scale-factor={scale}", "--window-size=1024,1024", f"--screenshot={out}", f.as_uri()],
                       check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=90)
        return Image.open(out).convert("RGB")

base = render(svg_src)
S = base.width // 1024
bb = ImageChops.difference(base, Image.new("RGB", base.size, BG)).point(lambda v: 255 if v > 8 else 0).convert("L").getbbox()
cx, cy = (bb[0] + bb[2]) / 2, (bb[1] + bb[3]) / 2
side = max(bb[2] - bb[0], bb[3] - bb[1]) * 1.06   # 3% breathing room each side

def icon(size, stroke_px):
    """stroke_px: wanted ring stroke thickness in output pixels."""
    units = (side / S) / size * stroke_px              # SVG user units that map to stroke_px output pixels
    svg = re.sub(r'stroke-width="[0-9.]+"', f'stroke-width="{units:.2f}"', svg_src)
    im = render(svg)
    box = tuple(int(round(v)) for v in (cx - side / 2, cy - side / 2, cx + side / 2, cy + side / 2))
    return im.crop(box).resize((size, size), Image.LANCZOS)

# per-size stroke weights chosen by eye: heavier at tiny sizes
sizes = {16: 0.5, 32: 0.6, 48: 0.6}
imgs = {s: icon(s, w) for s, w in sizes.items()}
ico = wwwroot / "favicon.ico"
import struct, io  # Pillow would build every ICO frame from one image; write the frames (one render per size) by hand
frames = []
for s in (16, 32, 48):
    b = io.BytesIO(); imgs[s].convert("RGBA").save(b, format="PNG"); frames.append((s, b.getvalue()))
hdr = struct.pack("<HHH", 0, 1, len(frames)); off = 6 + 16 * len(frames); dirs = b""; data = b""
for s, png in frames:
    dirs += struct.pack("<BBBBHHII", s, s, 0, 0, 1, 32, len(png), off + len(data)); data += png
ico.write_bytes(hdr + dirs + data)

apple = icon(180, 1.4)
# apple-touch-icon must be opaque; the mark already sits on the cream background, so no transparency to fill.
apple.save(wwwroot / "assets/apple-touch-icon.png", optimize=True)
for s in (16, 32, 48): imgs[s].save(f"/tmp/icon-preview-{s}.png")
print("wrote", ico, ico.stat().st_size, "bytes;", wwwroot / "assets/apple-touch-icon.png")

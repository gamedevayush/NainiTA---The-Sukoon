"""Draw the triangulated faces of Assets/Nainital/Data/Fonts3D/*.json (fill only, no anti-aliasing) to check the
triangulation: holes must stay open. py Tools/fonts3d/preview_fonts3d.py <out.png> [text]"""
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "Nainital", "Data", "Fonts3D"))
out = sys.argv[1]
text = sys.argv[2] if len(sys.argv) > 2 else "Hotel Abode 8&B @ Naini Q%"
keys = ["script", "round", "royal", "desi", "serif", "tall", "block", "deco"]
H, W = 90, 1500
img = Image.new("RGB", (W, H * len(keys)), (30, 30, 34))
d = ImageDraw.Draw(img)
for row, key in enumerate(keys):
    data = json.load(open(os.path.join(DATA, key + ".json"), encoding="utf-8"))
    g = {x["c"]: x for x in data["glyphs"]}
    x0, s, base = 10.0, 48.0, row * H + 70
    for ch in text:
        if ch == " ":
            x0 += data["space"] * s
            continue
        gl = g.get(ord(ch))
        if not gl:
            continue
        p = gl["pts"]
        for t in range(0, len(gl["tris"]), 3):
            tri = [(x0 + p[2 * i] * s, base - p[2 * i + 1] * s) for i in gl["tris"][t:t + 3]]
            d.polygon(tri, fill=(240, 220, 160))
        x0 += gl["adv"] * s
    d.text((W - 70, row * H + 5), key, fill=(255, 255, 0))
img.save(out)
print(out, img.size)

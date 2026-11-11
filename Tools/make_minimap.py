"""Render the radar/minimap texture (top = north) from the processed world data.

Usage: python -I make_minimap.py <processed_dir> <out_png>
"""
import json
import math
import pathlib
import sys

import numpy as np
from PIL import Image, ImageDraw

SIZE = 2048


def main():
    src, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    w = json.loads((src / "world.json").read_text(encoding="utf-8"))
    meta = w["meta"]
    res, size_m = meta["res"], meta["sizeM"]
    hm = np.frombuffer((src / "heightmap.bytes").read_bytes(), dtype="<u2").reshape(res, res).astype(np.float32)
    h = meta["hMin"] + hm / 65535.0 * (meta["hMax"] - meta["hMin"])
    cell = size_m / (res - 1)
    gz, gx = np.gradient(h, cell)
    L = np.array([-0.6, 0.6, 0.5]); L /= np.linalg.norm(L)
    nx, nz, ny = -gx, -gz, np.ones_like(h)
    shade = np.clip((nx * L[0] + ny * L[1] + nz * L[2]) / np.sqrt(nx * nx + ny * ny + nz * nz), 0, 1)
    rel = np.clip((h - meta["lakeLevel"]) / 600.0, -0.4, 1.0)
    base = np.stack([52 + 30 * rel, 78 + 25 * rel, 50 + 18 * rel], -1)
    rgb = base * (0.55 + 0.6 * shade[..., None])
    img = Image.fromarray(np.flipud(np.clip(rgb, 0, 255)).astype(np.uint8)).resize((SIZE, SIZE), Image.BILINEAR)
    dr = ImageDraw.Draw(img)
    k = SIZE / size_m
    half = size_m / 2
    cx, cz = meta.get("mapCx", 0.0), meta.get("mapCz", 0.0)   # the map square's centre

    def P(x, z):
        return ((x - cx + half) * k, (cz + half - z) * k)

    def pts(flat):
        return [P(flat[i], flat[i + 1]) for i in range(0, len(flat) - 1, 2)]

    for a in w["areas"]:
        if a["kind"] in ("stadium", "pitch", "park", "garden", "golf_course"):
            dr.polygon(pts(a["pts"]), fill=(96, 140, 80))
    hs = w["houses"]
    for i in range(0, len(hs) - 8, 9):
        x, z, ww, dd, yaw = hs[i:i + 5]
        c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
        corners = []
        for lx, lz in ((-ww / 2, -dd / 2), (ww / 2, -dd / 2), (ww / 2, dd / 2), (-ww / 2, dd / 2)):
            corners.append(P(x + lx * c + lz * s, z - lx * s + lz * c))
        dr.polygon(corners, fill=(128, 112, 100))
    for b in w["osmBuildings"]:
        dr.polygon(pts(b["pts"]), fill=(140, 120, 105))
    minor = {"path", "footway", "steps", "bridleway", "track"}
    for r in sorted(w["roads"], key=lambda r: r["kind"] not in minor, reverse=False):
        p = pts(r["pts"])
        if len(p) < 2:
            continue
        if r["kind"] in minor:
            dr.line(p, fill=(170, 150, 120), width=2)
        else:
            width = max(3, int(r["width"] * k * 1.4))
            dr.line(p, fill=(40, 40, 42), width=width + 3)
            dr.line(p, fill=(215, 210, 196), width=width)
    dr.polygon(pts(w["lake"]["pts"]), fill=(52, 112, 168), outline=(30, 70, 110))
    img.save(out)
    print("minimap", out, img.size)


if __name__ == "__main__":
    main()

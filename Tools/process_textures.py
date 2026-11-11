"""Prepare Poly Haven maps for Unity/URP.

Usage: python -I process_textures.py <raw_textures_dir> <out_dir>
  <name>_albedo.png : RGB diffuse, A = smoothness (from roughness) for URP Lit / Terrain Lit
  <name>_normal.png : OpenGL normal map (Unity convention)
  plaster_detail.png: grey (0.5-centred) detail albedo for URP Lit _DETAIL_MULX2
"""
import pathlib
import sys

import numpy as np
from PIL import Image

SMOOTH_SCALE = {  # how glossy each surface may get (A channel)
    "leafy_grass": 0.25, "forest_ground_04": 0.2, "rocky_terrain_02": 0.35, "dirt": 0.15,
    "asphalt_02": 0.55, "rectangular_paving": 0.45, "white_rough_plaster": 0.3,
    "forrest_ground_01": 0.2, "forest_leaves_02": 0.2, "rocky_trail": 0.3,
}


def main():
    src, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    for d in sorted(p for p in src.iterdir() if p.is_dir()):
        name = d.name
        diff = Image.open(d / "diff.jpg").convert("RGB")
        rough = Image.open(d / "rough.jpg").convert("L").resize(diff.size)
        smooth = (255 - np.asarray(rough, dtype=np.float32)) * SMOOTH_SCALE.get(name, 0.3)
        rgba = np.dstack([np.asarray(diff), np.clip(smooth, 0, 255).astype(np.uint8)])
        Image.fromarray(rgba, "RGBA").save(out / f"{name}_albedo.png")
        Image.open(d / "nor_gl.jpg").convert("RGB").save(out / f"{name}_normal.png")
        print("prepared", name)
    # plaster detail map: luminance centred on mid-grey
    p = src / "white_rough_plaster" / "diff.jpg"
    if p.exists():
        lum = np.asarray(Image.open(p).convert("L"), dtype=np.float32) / 255.0
        lum = 0.5 + (lum - lum.mean()) * 0.9
        Image.fromarray((np.clip(lum, 0, 1) * 255).astype(np.uint8), "L").convert("RGB").save(out / "plaster_detail.png")
        print("prepared plaster_detail")


if __name__ == "__main__":
    main()

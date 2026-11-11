"""Download CC0 PBR textures from Poly Haven (https://polyhaven.com, CC0 licence) at 1k for mobile.

Usage: python -I fetch_textures.py <out_dir>
Writes <out_dir>/<asset>/{diff,nor_gl,rough}.jpg
"""
import pathlib
import sys
import time

import requests

UA = {"User-Agent": "texture-fetch/0.1 (CC0 textures)"}
ASSETS = {
    "leafy_grass": "terrain grass",
    "forest_ground_04": "terrain forest floor",
    "rocky_terrain_02": "terrain rock",
    "dirt": "terrain dirt",
    "asphalt_02": "roads",
    "rectangular_paving": "Mall Road pavers",
    "white_rough_plaster": "building detail map",
    # photo-real pass (2026-11-11): baked into the building atlas and the town ground
    "plastered_wall_04": "weathered plaster for every facade",
    "corrugated_iron_02": "tin roofs",
    "old_stone_wall": "stone walls and plinths",
    "concrete_wall_008": "concrete",
    "concrete_floor_worn_001": "town ground and lake roads",
    "cobblestone_floor_08": "promenade and plazas",
    "brown_planks_04": "wood",
    "bark_brown_02": "tree trunks (oak, deodar)",
    "cliff_side": "cut hillsides and steep banks: grey-brown rock and soil (terrain rock layer)",
    "rock_face": "candidate: grey rock face for cut hillsides",
    "gray_rocks": "candidate: grey rock and soil",
    "rock_ground_02": "candidate: rocky soil",
    # terrain re-texture (user, 2026-11-11): leaf litter under the trees, dry mixed grass in the open, grey stony banks
    "forrest_ground_01": "terrain grass: mixed green/dry grass with twigs (Kumaon hill slopes)",
    "forest_leaves_02": "terrain forest floor: mossy leaf litter under oak and deodar",
    "rocky_trail": "terrain rock: grey stony soil on cut banks and steep slopes",
}
MAPS = {"Diffuse": "diff", "nor_gl": "nor_gl", "Rough": "rough"}


def main():
    out = pathlib.Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    for asset in ASSETS:
        d = out / asset
        d.mkdir(exist_ok=True)
        files = None
        for attempt in range(4):
            try:
                r = requests.get(f"https://api.polyhaven.com/files/{asset}", headers=UA, timeout=60)
                r.raise_for_status()
                files = r.json()
                break
            except Exception as e:
                print(f"  retry {asset}: {e}")
                time.sleep(2)
        if files is None:
            raise RuntimeError(f"could not list files for {asset}")
        for key, short in MAPS.items():
            path = d / f"{short}.jpg"
            if path.exists() and path.stat().st_size > 10000:
                continue
            try:
                url = files[key]["1k"]["jpg"]["url"]
            except KeyError:
                print(f"  {asset}: no {key} 1k jpg")
                continue
            for attempt in range(4):
                try:
                    rr = requests.get(url, headers=UA, timeout=120)
                    rr.raise_for_status()
                    if not rr.content[:3] == b"\xff\xd8\xff":
                        raise RuntimeError("not a JPEG")
                    path.write_bytes(rr.content)
                    break
                except Exception as e:
                    print(f"  retry {asset}/{key}: {e}")
                    time.sleep(2)
            print(f"{asset}/{short}.jpg  {path.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()

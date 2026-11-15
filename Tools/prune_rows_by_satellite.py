"""Drop the generated shop-row houses of world.json that stand on trees or grass in the satellite photo.

The rows (process_data.py mall_row / street_rows) are made up to close the gaps along the town's roads; real
footprints (Google Open Buildings / Microsoft / OSM) come from the satellite and are always kept. A row house - one
whose centre is on no real footprint - goes if most of the satellite ground under it is vegetation: green clearly
above both red and blue (teal tin roofs have as much blue as green, so they do not count as green).

Needs the zoom-18 Esri World Imagery tiles cached by the satellite comparison (Shots/satcompare/_tiles) and the
Overture footprints (<raw>/overture/buildings.json).
Usage: python -I Tools/prune_rows_by_satellite.py <raw dir> [--dry]
"""
import json
import math
import pathlib
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import area  # noqa: E402
from restore_footprints import rings, area_of, inside, rect_corners  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
WORLD = ROOT / "Assets" / "Nainital" / "Data" / "world.json"
TILES = ROOT / "Shots" / "satcompare" / "_tiles"
Z = 18
VEG_LIMIT = 0.6

_tiles = {}


def sat_rgb(x, z):
    lat, lon = area.CENTER_LAT + z / area.M_PER_DEG_LAT, area.CENTER_LON + x / area.M_PER_DEG_LON
    n = 256 * 2 ** Z
    s = math.sin(math.radians(lat))
    gx = (lon + 180) / 360 * n
    gy = (0.5 - math.log((1 + s) / (1 - s)) / (4 * math.pi)) * n
    tx, ty = int(gx // 256), int(gy // 256)
    key = (tx, ty)
    if key not in _tiles:
        f = TILES / f"{Z}_{tx}_{ty}.jpg"
        _tiles[key] = np.asarray(Image.open(f).convert("RGB"), np.int16) if f.exists() else None
    t = _tiles[key]
    if t is None:
        return None
    return t[int(gy) - ty * 256, int(gx) - tx * 256]


def main():
    raw = pathlib.Path(sys.argv[1])
    dry = "--dry" in sys.argv
    W = json.loads(WORLD.read_text(encoding="utf-8"))
    real = []
    for b in json.loads((raw / "overture" / "buildings.json").read_text(encoding="utf-8")):
        for ring in rings(b.get("wkt")):
            P = np.array([area.to_local(lat, lon) for lon, lat in ring], np.float64)
            if area_of(P) >= 12:
                real.append(P)
    rc = np.array([P.mean(0) for P in real])
    rr = np.array([np.max(np.hypot(*(P - P.mean(0)).T)) for P in real])

    # the Mall Road shop row stays whatever the photo shows: it was laid from the owner's Street View, and from above
    # the trees along the Mall hide the shop roofs
    mall = []
    for r in W["roads"]:
        if r.get("name") in ("Mall Road", "Upper Mall Road", "Lower Mall Road"):
            L = np.array([(r["pts"][i], r["pts"][i + 1]) for i in range(0, len(r["pts"]) - 1, 2)])
            for a, b in zip(L[:-1], L[1:]):
                n = max(1, int(np.hypot(*(b - a)) // 2))
                mall += [a + (b - a) * t for t in np.linspace(0, 1, n + 1)]
    mall = np.array(mall)

    H = W["houses"]
    houses = [H[i:i + 9] for i in range(0, len(H) - 8, 9)]
    keep, dropped, rows, unknown, by_mall = [], [], 0, 0, 0
    for h in houses:
        c = np.array(h[:2])
        near = np.flatnonzero(np.hypot(*(rc - c).T) < rr + 1)
        if any(inside(c, real[k]) for k in near):
            keep.append(h)
            continue
        if len(mall) and np.min(np.hypot(*(mall - c).T)) < 25.0:
            by_mall += 1
            keep.append(h)
            continue
        rows += 1
        # sample the rectangle on a 1 m grid
        a = math.radians(h[4])
        ux, uz = np.array([math.cos(a), -math.sin(a)]), np.array([math.sin(a), math.cos(a)])
        veg = tot = 0
        for i in np.arange(-h[2] / 2 + 0.5, h[2] / 2, 1.0):
            for j in np.arange(-h[3] / 2 + 0.5, h[3] / 2, 1.0):
                p = c + ux * i + uz * j
                px = sat_rgb(*p)
                if px is None:
                    continue
                r, g, b = int(px[0]), int(px[1]), int(px[2])
                tot += 1
                if g > r + 6 and g > b + 6:
                    veg += 1
        if tot == 0:
            unknown += 1
            keep.append(h)
            continue
        if veg / tot > VEG_LIMIT:
            dropped.append((round(h[0]), round(h[1]), round(veg / tot, 2)))
        else:
            keep.append(h)
    print(f"row houses {rows + by_mall} ({by_mall} on the Mall, kept), dropped on trees/grass {len(dropped)}, no imagery {unknown}")
    for d in dropped[:25]:
        print("  ", d)
    if dry:
        return
    W["houses"] = [v for h in keep for v in h]
    WORLD.write_text(json.dumps(W, separators=(",", ":")), encoding="utf-8")
    print(f"houses {len(houses)} -> {len(keep)}; written {WORLD}")


if __name__ == "__main__":
    main()

"""Give every real building of world.json the roof colour it has in the satellite photo (user, 2026-11-15: compare
the buildings with the map). Until now the roof colour was drawn at random from the palette, so a green tin roof could
come out red. For each house rectangle whose centre is on a real footprint, the median colour of the photo inside the
rectangle (shrunk by 15 %) is matched by hue to the roof palette of Atlas.RoofPalette:
0 red tin, 1 maroon, 2 green, 3 teal, 4 blue, 5 rust, 6 grey. Shop-row houses (made up) keep theirs.

Needs the zoom-18 Esri tiles of Shots/satcompare/_tiles and the Overture footprints.
Usage: python -I Tools/roof_colours_from_satellite.py <raw dir> [--dry]
"""
import colorsys
import json
import math
import pathlib
import sys

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import area  # noqa: E402
from restore_footprints import rings, area_of, inside  # noqa: E402
from prune_rows_by_satellite import sat_rgb  # noqa: E402

WORLD = pathlib.Path(__file__).resolve().parent.parent / "Assets" / "Nainital" / "Data" / "world.json"
NAMES = ["red tin", "maroon", "green", "teal", "blue", "rust", "grey"]


def roof_index(rgb):
    """The palette index, or None when the photo cannot tell (a tree over the roof, deep shadow): then the roof keeps
    its colour. Dark greens are trees, dark browns are shade or bare ground: only clear, lit colours count."""
    r, g, b = (c / 255.0 for c in rgb)
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    h *= 360.0
    if v < 0.28:
        return None                      # shadow
    if s < 0.14:
        return 6 if v > 0.4 else None    # grey tin or a concrete terrace
    if h < 18 or h >= 330:
        return 0 if v > 0.45 else 1 if s > 0.3 else None
    if h < 45:
        return 5 if s > 0.3 and v > 0.35 else None
    if h < 75:
        return None
    if h < 155:
        return 2 if v > 0.42 and s > 0.28 else None   # dark green: a tree
    if h < 195:
        return 3 if v > 0.36 else None
    if h < 260:
        return 4 if v > 0.36 else None
    return 1 if s > 0.3 else None


def main():
    raw = pathlib.Path(sys.argv[1])
    dry = "--dry" in sys.argv
    W = json.loads(WORLD.read_text(encoding="utf-8"))
    real = []
    for bld in json.loads((raw / "overture" / "buildings.json").read_text(encoding="utf-8")):
        for ring in rings(bld.get("wkt")):
            P = np.array([area.to_local(lat, lon) for lon, lat in ring], np.float64)
            if area_of(P) >= 12:
                real.append(P)
    rc = np.array([P.mean(0) for P in real])
    rr = np.array([np.max(np.hypot(*(P - P.mean(0)).T)) for P in real])

    H = W["houses"]
    n = len(H) // 9
    changed, done = 0, 0
    counts = [0] * 7
    for i in range(n):
        x, z, w, d, yaw = H[i * 9:i * 9 + 5]
        c = np.array([x, z])
        near = np.flatnonzero(np.hypot(*(rc - c).T) < rr + 1)
        if not any(inside(c, real[k]) for k in near):
            continue
        a = math.radians(yaw)
        ux, uz = np.array([math.cos(a), -math.sin(a)]), np.array([math.sin(a), math.cos(a)])
        px = []
        for u in np.linspace(-0.42, 0.42, 7) * w:
            for v in np.linspace(-0.42, 0.42, 7) * d:
                q = sat_rgb(*(c + ux * u + uz * v))
                if q is not None:
                    px.append(q)
        if len(px) < 10:
            continue
        med = np.median(np.array(px), axis=0)
        k = roof_index(med)
        if k is None:
            continue
        counts[k] += 1
        done += 1
        if int(H[i * 9 + 7]) != k:
            H[i * 9 + 7] = float(k)
            changed += 1
        # a coloured roof in the photo is tin: roof type 2 (WorldBuilder: sloped, in this colour, not a G.O.A.N. box);
        # a grey one may be a concrete terrace, so its type stays as it was
        if k != 6:
            H[i * 9 + 8] = 2.0
    print(f"real roofs coloured from the photo: {done}, changed {changed}")
    print("  " + ", ".join(f"{NAMES[k]} {counts[k]}" for k in range(7)))
    if not dry:
        WORLD.write_text(json.dumps(W, separators=(",", ":")), encoding="utf-8")
        print("written", WORLD)


if __name__ == "__main__":
    main()

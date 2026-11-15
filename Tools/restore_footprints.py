"""Put the real building footprints back into Assets/Nainital/Data/world.json (user, 2026-11-15: "google map se har
building ke shape check krke apne waale top view se compare kro").

The satellite comparison (Shots/satcompare) showed the shop rows of process_data.py (mall_row, street_rows) had
replaced real footprints: merge_*_row drops every unnamed footprint a row house lands near, so in Tallital, Mallital
and Bada Bazar big real buildings had turned into rows of identical 5 x 8 m boxes, some of them on open ground or
among trees. This patches the processed data without the 20 GB re-run:

  1. every Overture footprint (Google Open Buildings / Microsoft / OSM) that nothing in world.json stands on is
     restored - unless it stands in a carriageway, in the lake or on a viewpoint plaza (as process_data drops them);
  2. every row house (a house whose centre is on no real footprint) that overlaps a real footprint is dropped;
  3. row houses with no real footprint within 14 m (open ground, forest) are dropped.

Usage: python -I Tools/restore_footprints.py <raw dir with overture/buildings.json> [--dry]
Writes world.json in place (a copy is kept as world.before_restore.json) and prints what changed.
"""
import json
import math
import pathlib
import random
import re
import sys

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import area  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
WORLD = ROOT / "Assets" / "Nainital" / "Data" / "world.json"
MOTOR = {"trunk", "trunk_link", "primary", "primary_link", "secondary", "secondary_link", "tertiary", "tertiary_link",
         "unclassified", "residential", "living_street", "service"}
PLAZA = {"Snow View": 26.0, "Naina Peak summit": 12.0, "Tiffin Top": 12.0, "Land's End": 10.0, "Himalaya Darshan": 13.0,
         "Lovers Point": 14.0, "Barapathar": 15.0, "Khurpatal Lake View Point": 13.0, "Khurpatal View Point": 13.0}
WALL_WEIGHTS = [20, 14, 12, 12, 10, 6, 8, 5, 3, 3, 4, 3]
ROOF_WEIGHTS = [26, 12, 18, 8, 14, 6, 16]


def rings(wkt):
    out = []
    for m in re.finditer(r"\(\(([^()]+)\)", wkt or ""):
        pts = [tuple(map(float, q.split()[:2])) for q in m.group(1).split(",") if q.strip()]
        if len(pts) >= 4:
            out.append(pts)
    return out


def area_of(p):
    x, z = p[:, 0], p[:, 1]
    return 0.5 * abs(np.dot(x, np.roll(z, -1)) - np.dot(z, np.roll(x, -1)))


def inside(p, poly):
    x, z = p
    c = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, zi = poly[i]
        xj, zj = poly[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi + 1e-12) + xi:
            c = not c
        j = i
    return c


def min_area_rect(pts):
    P = sorted(set(map(tuple, np.round(pts, 3))))
    if len(P) < 3:
        return None

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for q in P:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], q) <= 0:
            lo.pop()
        lo.append(q)
    for q in reversed(P):
        while len(up) >= 2 and cross(up[-2], up[-1], q) <= 0:
            up.pop()
        up.append(q)
    hull = np.array(lo[:-1] + up[:-1])
    best = None
    for i in range(len(hull)):
        e = hull[(i + 1) % len(hull)] - hull[i]
        L = math.hypot(*e)
        if L < 1e-6:
            continue
        ux = e / L
        uz = np.array([-ux[1], ux[0]])
        px, pz = hull @ ux, hull @ uz
        w, d = px.max() - px.min(), pz.max() - pz.min()
        if best is None or w * d < best[0]:
            c = ux * (px.max() + px.min()) / 2 + uz * (pz.max() + pz.min()) / 2
            yaw = math.degrees(math.atan2(uz[0], uz[1]))   # local +z (depth) along uz
            best = (w * d, c, w, d, yaw)
    return best[1:] if best else None


def rect_corners(x, z, w, d, yaw):
    a = math.radians(yaw)
    rv = np.array([math.cos(a), -math.sin(a)]) * w / 2
    fv = np.array([math.sin(a), math.cos(a)]) * d / 2
    c = np.array([x, z])
    return np.array([c + rv + fv, c - rv + fv, c - rv - fv, c + rv - fv])


def main():
    raw = pathlib.Path(sys.argv[1])
    dry = "--dry" in sys.argv
    W = json.loads(WORLD.read_text(encoding="utf-8"))
    rng = random.Random(4711)

    # ---- the real footprints, local metres
    real = []
    for b in json.loads((raw / "overture" / "buildings.json").read_text(encoding="utf-8")):
        if b.get("is_underground"):
            continue
        for ring in rings(b.get("wkt")):
            P = np.array([area.to_local(lat, lon) for lon, lat in ring], np.float64)
            if area_of(P) >= 12.0:
                real.append(P)
    print(f"real footprints: {len(real)}")
    rc = np.array([P.mean(0) for P in real])
    rr = np.array([np.max(np.hypot(*(P - P.mean(0)).T)) for P in real])

    def real_near(c, r):
        idx = np.flatnonzero(np.hypot(*(rc - c).T) < rr + r)
        return idx

    # ---- what world.json has
    H = W["houses"]
    houses = [H[i:i + 9] for i in range(0, len(H) - 8, 9)]
    osm = W["osmBuildings"]
    osm_polys = [np.array([(b["pts"][i], b["pts"][i + 1]) for i in range(0, len(b["pts"]) - 1, 2)]) for b in osm]

    # a house is real if its centre stands on a real footprint
    def on_real(c):
        return any(inside(c, real[k]) for k in real_near(c, 1.0))

    house_real = [on_real(np.array(h[:2])) for h in houses]

    # which real footprints are represented (something of world.json has its centre on it, or it on something)
    rep = np.zeros(len(real), bool)
    for h, isr in zip(houses, house_real):
        c = np.array(h[:2])
        for k in real_near(c, 1.0):
            if inside(c, real[k]):
                rep[k] = True
    for P in osm_polys:
        c = P.mean(0)
        for k in real_near(c, 1.0):
            if inside(c, real[k]) or inside(rc[k], P):
                rep[k] = True

    # ---- carriageways, lake and plazas (as process_data.off_the_roads and the plaza rule)
    A, B, Hw = [], [], []
    for r in W["roads"]:
        if r["kind"] not in MOTOR or len(r["pts"]) < 4:
            continue
        L = np.array([(r["pts"][i], r["pts"][i + 1]) for i in range(0, len(r["pts"]) - 1, 2)])
        A.append(L[:-1]); B.append(L[1:]); Hw.append(np.full(len(L) - 1, r["width"] / 2.0))
    A, B, Hw = np.vstack(A), np.vstack(B), np.concatenate(Hw)
    AB = B - A
    L2 = np.maximum((AB * AB).sum(1), 1e-9)

    def into_road(P):
        lo, hi = P.min(0) - 20, P.max(0) + 20
        near = np.all((np.minimum(A, B) < hi) & (np.maximum(A, B) > lo), axis=1)
        if not near.any():
            return -1.0
        a, ab, l2, hw = A[near], AB[near], L2[near], Hw[near]
        t = np.clip(((P[:, None, :] - a[None]) * ab[None]).sum(2) / l2[None], 0, 1)
        d = np.hypot(*(P[:, None, :] - (a[None] + ab[None] * t[..., None])).transpose(2, 0, 1))
        return float(np.max(hw[None] - d))

    lake = np.array([(W["lake"]["pts"][i], W["lake"]["pts"][i + 1]) for i in range(0, len(W["lake"]["pts"]) - 1, 2)])
    plazas = [(np.array([p["x"], p["z"]]), PLAZA[p["name"]]) for p in W["pois"] if p["name"] in PLAZA]

    def blocked(P):
        c = P.mean(0)
        if inside(c, lake):
            return "lake"
        if any(np.hypot(*(c - q)) < r + np.max(np.hypot(*(P - c).T)) for q, r in plazas):
            return "plaza"
        pts = np.vstack([P, c, (P + c) / 2])
        return "road" if into_road(pts) > 0.8 else None

    # ---- 1. restore the missing real footprints
    added_rects, added_polys, why = [], [], {}
    for k in np.flatnonzero(~rep):
        P = real[k]
        b = blocked(P)
        if b:
            why[b] = why.get(b, 0) + 1
            continue
        a = area_of(P)
        levels = rng.choice([1, 1, 2]) if a < 30 else rng.choice([1, 2, 2, 3]) if a < 80 else rng.choice([2, 2, 3, 3, 4]) if a < 200 else rng.choice([2, 3, 3, 4])
        wall = rng.choices(range(len(WALL_WEIGHTS)), weights=WALL_WEIGHTS)[0]
        roof = rng.choices(range(len(ROOF_WEIGHTS)), weights=ROOF_WEIGHTS)[0]
        roof_type = 1 if rng.random() < (0.28 if a < 120 else 0.5) else 0
        r = min_area_rect(P)
        fill = a / max(r[1] * r[2], 1e-3) if r else 0
        if r and fill > 0.72 and max(r[1], r[2]) <= 45 and min(r[1], r[2]) >= 3:
            c, w, d, yaw = r
            # front (+z) towards the nearest carriageway: the shop side
            t = np.clip(((c - A) * AB).sum(1) / L2, 0, 1)
            q = A + AB * t[:, None]
            j = int(np.argmin(np.hypot(*(q - c).T)))
            to = q[j] - c
            best, bd = 0, -9
            for qq in range(4):
                yy = math.radians(yaw + 90 * qq)
                dd = math.sin(yy) * to[0] + math.cos(yy) * to[1]
                if dd > bd:
                    best, bd = qq, dd
            yaw += 90 * best
            if best % 2 == 1:
                w, d = d, w
            added_rects.append([round(float(c[0]), 2), round(float(c[1]), 2), round(float(w), 2), round(float(d), 2), round(float(yaw), 2),
                                float(levels), float(wall), float(roof), float(roof_type)])
        else:
            added_polys.append({"name": "", "kind": "house", "levels": levels,
                                "pts": [round(float(v), 2) for xy in P[:-1] for v in xy]})
    print(f"restored: {len(added_rects)} as rectangles, {len(added_polys)} as footprints; left out: {why}")

    # ---- 2 + 3. row houses on real footprints, or far from any
    new_real = [np.array(rect_corners(*h[:5])) for h in added_rects] + \
               [np.array([(p["pts"][i], p["pts"][i + 1]) for i in range(0, len(p["pts"]) - 1, 2)]) for p in added_polys]
    nr_c = np.array([P.mean(0) for P in new_real]) if new_real else np.zeros((0, 2))
    keep, dropped_overlap, dropped_open = [], 0, 0
    for h, isr in zip(houses, house_real):
        if isr:
            keep.append(h)
            continue
        c = np.array(h[:2])
        corners = rect_corners(*h[:5])
        samples = np.vstack([corners, c, (corners + c) / 2])
        hits = 0
        for k in real_near(c, max(h[2], h[3])):
            hits = max(hits, sum(inside(s, real[k]) for s in samples))
        if hits >= 2:   # any real part of the row house on a real building (walls would cut through each other)
            dropped_overlap += 1
            continue
        if len(real_near(c, 14.0)) == 0:
            dropped_open += 1
            continue
        keep.append(h)
    print(f"row houses: {sum(1 for x in house_real if not x)}; dropped {dropped_overlap} on real buildings, {dropped_open} on open ground")

    houses = keep + added_rects
    print(f"houses {len(H) // 9} -> {len(houses)}, footprint buildings {len(osm)} -> {len(osm) + len(added_polys)}")
    if dry:
        return
    backup = WORLD.with_name("world.before_restore.json")
    if not backup.exists():
        backup.write_text(WORLD.read_text(encoding="utf-8"), encoding="utf-8")
    W["houses"] = [v for h in houses for v in h]
    W["osmBuildings"] = osm + added_polys
    WORLD.write_text(json.dumps(W, separators=(",", ":")), encoding="utf-8")
    print("written", WORLD)


if __name__ == "__main__":
    main()

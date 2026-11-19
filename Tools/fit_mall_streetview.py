"""Move Upper and Lower Mall Road onto the real road, using a Street View route as the truth.

The OSM lines that Mall Road is built from run up to ~19 m east of where the road is (the Street View car's GPS,
which agrees with the lake shore). This takes the route the "Street View 360 Saver" extension saves
(route.csv: lat, lng of every panorama), and for the stretches where we know which road the car was on
(SEGMENTS below, read off the photos) shifts BOTH Mall roads sideways by the same amount, so the driven road's
centre line lies on the car's track and the other road keeps its distance. The shift eases out to nothing
60 m past the ends of the covered stretch.

Things that stand relative to the roads move with them:
  - generated houses on Upper Mall's hill side (the shop row of process_data.py: mall_row)
and things that the moved roads now run over are dropped:
  - generated houses and trees standing in a carriageway.
Real building footprints (osmBuildings) stay where they are; any the new roads touch are listed.

usage:  python -I fit_mall_streetview.py <world.json> <StreetView360 folder> [--dry]
A copy of the old world.json goes to Generated/world.before_svfit.json (only the first time).
Then run Nainital > 2. Build World.
"""
import csv, glob, json, math, os, shutil, sys
import numpy as np

CENTER_LAT, CENTER_LON = 29.3905, 79.4550
M_LAT = 110_860.0
M_LON = 111_320.0 * math.cos(math.radians(CENTER_LAT))
UPPER, LOWER = "Upper Mall Road", "Lower Mall Road"

# Which road the car was on, by panorama index (route.csv), read off the photos (19 Nov 2026 route,
# Tallital chowk -> Mall Road, Aug 2022 imagery). lane: the car drives on the left, so the road's centre is
# this many metres to the right of the track.
SEGMENTS = [
    {"route": "route_2026-11-19-10-11", "road": UPPER, "first": 13, "last": 49, "lane": 1.5},   # 131-524 m: Upper Mall, two-way
    {"route": "route_2026-11-19-10-11", "road": LOWER, "first": 51, "last": 87, "lane": 0.5},   # 552-935 m: Lower Mall
]
R1, R2, R3 = "route_2026-11-19-10-11", "route_2026-11-19-10-33", "route_2026-11-19-10-36"

# What stands beside the road, read off the same photos. side: "right"/"left" of the car's direction of travel;
# near/far: metres from the car's track. drop: remove the generated houses there; low: one storey, white walls,
# green tin (barracks); trees: plant broadleaf (banj oak) and deodar every `trees` metres between near and far.
STREET_EDITS = [
    # Tallital -> Mall Road: the Army Rest & Recoup Centre's wall runs along the road (LandmarkBuilder.ArmyRest.cs),
    # low green-roofed barracks on open ground behind it, no shop row
    {"route": R1, "first": 5, "last": 12, "side": "right", "near": 0.0, "far": 10.0, "drop": True},
    {"route": R1, "first": 5, "last": 12, "side": "right", "near": 10.0, "far": 32.0, "low": True},
    # Upper Mall, 154-230 m and 364-513 m: a steep green bank with stone retaining walls, the hotels high up and set
    # back (Elphinstone, Mansarovar, Evelyn, the Methodist church on its terrace) - no shops at the road
    {"route": R1, "first": 15, "last": 22, "side": "right", "near": 0.0, "far": 26.0, "drop": True, "trees": 4.0},
    {"route": R1, "first": 34, "last": 48, "side": "right", "near": 0.0, "far": 26.0, "drop": True, "trees": 4.0},
    # Mallital, the road west past the Flats: on the south side a garden of big trees behind a low iron fence, no shops
    {"route": R2, "first": 1, "last": 13, "side": "left", "near": 0.0, "far": 11.0, "drop": True},
    {"route": R2, "first": 1, "last": 12, "side": "left", "near": 4.5, "far": 16.0, "trees": 3.5},
    # Bada Bazar, Mallital (route 10-36): on the left the road runs along the top of a wall over the big car park - no
    # buildings at the road; on the right the bazaar, four and five storeys shoulder to shoulder, shops all along
    {"route": R3, "first": 2, "last": 9, "side": "left", "near": 0.0, "far": 14.0, "drop": True},
    {"route": R3, "first": 3, "last": 8, "side": "left", "near": 4.0, "far": 10.0, "trees": 7.0},
    {"route": R3, "first": 1, "last": 10, "side": "right", "near": 0.0, "far": 16.0, "tall": True, "fill": True},
    # past the ICICI Bank corner: low whitewashed sheds with grey tin, then the white cottages with red roofs by the Flats
    {"route": R3, "first": 10, "last": 12, "side": "left", "near": 0.0, "far": 16.0, "low": True, "roof": 6},
    {"route": R3, "first": 13, "last": 16, "side": "left", "near": 0.0, "far": 16.0, "low": True, "roof": 0},
]
BAZAAR_WALLS = [6, 3, 1, 2, 0, 6, 3]   # peach, pale pink, cream, light yellow, white (the photos)
# single houses: (x, z) of the generated house -> new (levels, wall colour, roof colour); Atlas.WallPalette / RoofPalette
HOUSE_EDITS = [
    ((843.2, -1123.2), (1, 1, 5)),    # Tallital, the corner of the road up to Mall Road: an old one-storey cream cottage
    ((-69.7, 145.9), (2, 0, 0)),      # Mallital: the long white building with the red roof and the clock tower
    ((-76.6, 144.7), (2, 0, 0)),
]

EASE = 60.0          # m past the covered stretch over which the shift goes to zero
REACH = 30.0         # a road point further than this from the track is not moved
SMOOTH = 2           # shift averaged over +-2 panoramas (~+-20 m)
ROW_DEPTH = 24.0     # houses this far into Upper Mall's hill side move with it


def to_local(lat, lng):
    return np.array([(lng - CENTER_LON) * M_LON, (lat - CENTER_LAT) * M_LAT])


def pairs(flat):
    return np.array(list(zip(flat[0::2], flat[1::2])), np.float64)


def flat(P):
    return [round(float(v), 2) for p in P for v in p]


def nearest_on(line, q):
    """(distance, point, segment index, t) of the point of a polyline closest to q."""
    a, b = line[:-1], line[1:]
    ab = b - a
    t = np.clip(((q - a) * ab).sum(1) / np.maximum((ab * ab).sum(1), 1e-9), 0.0, 1.0)
    p = a + ab * t[:, None]
    d = np.hypot(*(q - p).T)
    i = int(np.argmin(d))
    return d[i], p[i], i, t[i]


def dist_to(P, line):
    best = np.full(len(P), np.inf)
    for a, b in zip(line[:-1], line[1:]):
        ab = b - a
        t = np.clip(((P - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
        best = np.minimum(best, np.hypot(*(P - (a + t[:, None] * ab)).T))
    return best


def main():
    world_path, sv_dir = sys.argv[1], sys.argv[2]
    dry = "--dry" in sys.argv
    backup = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(world_path))))),
                          "Generated", "world.before_svfit.json")
    # always start from the world as it was before the first fit, so running this again does not shift twice
    src = backup if os.path.exists(backup) else world_path
    print(f"  reading {src}")
    world = json.load(open(src, encoding="utf-8"))
    roads = {r["name"]: r for r in world["roads"] if r["name"] in (UPPER, LOWER)}
    U0, L0 = pairs(roads[UPPER]["pts"]), pairs(roads[LOWER]["pts"])

    # the track and, per panorama, the sideways shift that puts the driven road under the car
    track, shift = [], []
    for seg in SEGMENTS:
        folder = sorted(glob.glob(os.path.join(sv_dir, seg["route"] + "*")))
        if not folder:
            print(f"  route {seg['route']} not found in {sv_dir}: skipped")
            continue
        rows = [r for r in csv.DictReader(open(os.path.join(folder[0], "route.csv"), encoding="utf-8-sig"))
                if not r["file"].startswith("FAILED")]
        P = {int(r["index"]): to_local(float(r["lat"]), float(r["lng"])) for r in rows}
        line = U0 if seg["road"] == UPPER else L0
        idx = [i for i in sorted(P) if seg["first"] <= i <= seg["last"]]
        for i in idx:
            a, b = P.get(i - 1, P[i]), P.get(i + 1, P[i])
            tan = (b - a) / max(np.hypot(*(b - a)), 1e-9)
            right = np.array([tan[1], -tan[0]])
            centre = P[i] + right * seg["lane"]
            d, p, _, _ = nearest_on(line, centre)
            track.append(P[i])
            shift.append(centre - p)
    track, shift = np.array(track), np.array(shift)
    sm = np.array([shift[max(0, k - SMOOTH):k + SMOOTH + 1].mean(0) for k in range(len(shift))])
    print(f"  track: {len(track)} panoramas; shift {np.hypot(*sm.T).min():.1f} .. {np.hypot(*sm.T).max():.1f} m")

    def field(q):
        """Shift for a point beside the covered stretch: the track's shift at the nearest place on it.
        Zero for points past either end of the track (move_road eases those) or too far from it."""
        d, p, i, t = nearest_on(track, q)
        if d > REACH or (i == 0 and t <= 0.0) or (i == len(track) - 2 and t >= 1.0):
            return np.zeros(2)
        return sm[i] * (1 - t) + sm[i + 1] * t

    def move_road(P):
        """Shift a road's points; past the covered stretch the last shift eases to zero along the road."""
        S = np.array([field(q) for q in P])
        moved = np.hypot(*S.T) > 1e-6
        if moved.any():
            arc = np.concatenate([[0.0], np.cumsum(np.hypot(*np.diff(P, axis=0).T))])
            on = np.where(moved)[0]
            # points just past the moved stretch: carry the last shift and ease it to zero over EASE metres
            for side in (on.min(), on.max()):
                v = S[side]
                for j in range(len(P)):
                    if moved[j]:
                        continue
                    gap = abs(arc[j] - arc[side])
                    if gap < EASE and ((side == on.min() and j < side) or (side == on.max() and j > side)):
                        x = gap / EASE
                        S[j] = v * (1.0 - x * x * (3 - 2 * x))
        return P + S, S

    U1, SU = move_road(U0)
    L1, SL = move_road(L0)
    print(f"  {UPPER}: moved {int((np.hypot(*SU.T) > 0.05).sum())} of {len(U0)} points, up to {np.hypot(*SU.T).max():.1f} m")
    print(f"  {LOWER}: moved {int((np.hypot(*SL.T) > 0.05).sum())} of {len(L0)} points, up to {np.hypot(*SL.T).max():.1f} m")

    # generated houses: the shop row on Upper Mall's hill side moves with the road
    H = np.array(world["houses"], np.float64).reshape(-1, 9)
    near = (H[:, 0] > U0[:, 0].min() - 40) & (H[:, 0] < U0[:, 0].max() + 40) & (H[:, 1] > U0[:, 1].min() - 40) & (H[:, 1] < U0[:, 1].max() + 40)
    moved_h = 0
    for k in np.where(near)[0]:
        q = H[k, :2]
        d, p, i, _ = nearest_on(U0, q)
        if d > ROW_DEPTH:
            continue
        tan = U0[min(i + 1, len(U0) - 1)] - U0[i]
        tan /= max(np.hypot(*tan), 1e-9)
        n = np.array([tan[1], -tan[0]])
        dl, _, _, _ = nearest_on(L0, q)
        if dl < d:   # nearer Lower Mall: the lake side, not the row
            continue
        j = int(np.argmin(np.hypot(*(U0 - p).T)))
        s = SU[j]
        if np.hypot(*s) > 0.05:
            H[k, :2] += s
            moved_h += 1
    # drop generated houses and trees the new roads run over
    keepH = (dist_to(H[:, :2], U1) > 3.0 + 0.3 * np.minimum(H[:, 2], H[:, 3])) & (dist_to(H[:, :2], L1) > 2.6 + 0.3 * np.minimum(H[:, 2], H[:, 3]))
    keepH |= ~near
    T = np.array(world["trees"], np.float64).reshape(-1, 4)
    tn = (T[:, 0] > U0[:, 0].min() - 40) & (T[:, 0] < U0[:, 0].max() + 40) & (T[:, 1] > U0[:, 1].min() - 40) & (T[:, 1] < U0[:, 1].max() + 40)
    keepT = np.ones(len(T), bool)
    sub = np.where(tn)[0]
    keepT[sub] = (dist_to(T[sub, :2], U1) > 3.6) & (dist_to(T[sub, :2], L1) > 3.0)
    print(f"  houses: {moved_h} moved with Upper Mall, {int((~keepH).sum())} dropped (in a carriageway)")
    print(f"  trees: {int((~keepT).sum())} dropped (in a carriageway)")

    hit = []
    for b in world["osmBuildings"]:
        P = pairs(b["pts"])
        c = P.mean(0)
        if np.hypot(*(c - U1.mean(0))) > 1200:
            continue
        du, dl = dist_to(np.vstack([P, c]), U1).min(), dist_to(np.vstack([P, c]), L1).min()
        if du < 3.0 or dl < 2.4:
            hit.append((b.get("name") or "(unnamed)", round(float(c[0]), 1), round(float(c[1]), 1), round(float(min(du, dl)), 1)))
    print(f"  real footprints the new roads touch: {len(hit)}" + "".join(f"\n    {h}" for h in hit[:20]))

    H, keepH, T, keepT = street_edits(world, sv_dir, H, keepH, T, keepT, U1, L1)

    if dry:
        print("  --dry: nothing written")
        return
    os.makedirs(os.path.dirname(backup), exist_ok=True)
    if not os.path.exists(backup):
        shutil.copyfile(world_path, backup)
        print(f"  old world.json kept in {backup}")
    roads[UPPER]["pts"] = flat(U1)
    roads[LOWER]["pts"] = flat(L1)
    world["houses"] = [round(float(v), 2) for v in H[keepH].ravel()]
    world["trees"] = [round(float(v), 2) for v in T[keepT].ravel()]
    with open(world_path, "w", encoding="utf-8") as f:
        json.dump(world, f, separators=(",", ":"))
    print(f"  wrote {world_path}")


def point_in(poly, q):
    """Even-odd test: q inside the polygon (an n x 2 array)."""
    x, z = q
    inside = False
    for (ax, az), (bx, bz) in zip(poly, np.roll(poly, -1, axis=0)):
        if (az > z) != (bz > z) and x < ax + (z - az) * (bx - ax) / (bz - az):
            inside = not inside
    return inside


def load_track(sv_dir, route):
    folder = sorted(glob.glob(os.path.join(sv_dir, route + "*")))
    if not folder:
        return None
    rows = [r for r in csv.DictReader(open(os.path.join(folder[0], "route.csv"), encoding="utf-8-sig"))
            if not r["file"].startswith("FAILED")]
    return {int(r["index"]): to_local(float(r["lat"]), float(r["lng"])) for r in rows}


def fill_row(tr, sgn, H, keepH, new_houses, centres, radii, motor, rng):
    """Shop-houses shoulder to shoulder along one side of a stretch: walk along it and put a house in every gap
    wider than 4 m between the houses already there (and clear of real footprints and other roads)."""
    added = 0
    ROAD_EDGE, DEPTH = 4.6, 9.0
    for a, b in zip(tr[:-1], tr[1:]):
        ab = b - a
        L = float(np.hypot(*ab))
        t = ab / max(L, 1e-9)
        out = np.array([t[1], -t[0]]) * sgn          # from the road towards the row
        s = 0.0
        while s < L - 2.0:
            w = float(rng.uniform(5.0, 8.0))
            c = a + t * (s + w / 2) + out * (ROAD_EDGE + DEPTH / 2)
            occupied = keepH & (np.hypot(H[:, 0] - c[0], H[:, 1] - c[1]) < 0.5 * (np.maximum(H[:, 2], H[:, 3]) + w) * 0.9)
            mine = [h for h in new_houses if math.hypot(h[0] - c[0], h[1] - c[1]) < 0.5 * (max(h[2], h[3]) + w) * 0.9]
            real = np.hypot(*(centres - c).T) < radii + w * 0.45
            road = any(len(m) > 1 and np.min(np.hypot(*(m - c).T)) < 60 and dist_to(c[None], m)[0] < DEPTH / 2 + 3.6 for m in motor)
            if occupied.any() or mine or real.any() or road:
                s += 2.0
                continue
            face = -out
            yaw = math.degrees(math.atan2(face[0], face[1]))
            k = len(H) + len(new_houses)
            new_houses.append([round(float(c[0]), 2), round(float(c[1]), 2), round(w, 2), DEPTH, round(yaw, 2),
                               4 + int(rng.random() < 0.5), BAZAAR_WALLS[k % len(BAZAAR_WALLS)], int(rng.integers(0, 7)), 1])
            added += 1
            s += w + 0.05
    return added


def street_edits(world, sv_dir, H, keepH, T, keepT, U1, L1):
    """STREET_EDITS and HOUSE_EDITS: drop / lower generated houses beside stretches of a route, plant trees."""
    rng = np.random.default_rng(1911)
    blocks = [np.asarray(b["pts"], np.float64).reshape(-1, 2) for b in world["osmBuildings"]]
    centres = np.array([b.mean(0) for b in blocks])
    radii = np.array([np.max(np.hypot(*(b - b.mean(0)).T)) for b in blocks])
    motor = [pairs(r["pts"]) for r in world["roads"] if r["kind"] in ("trunk", "primary", "secondary", "tertiary", "residential",
                                                                       "unclassified", "service") and len(r["pts"]) >= 4
             and r["name"] not in (UPPER, LOWER)]   # the Mall roads are checked against their new lines (U1, L1)
    motor = [m if m is not None else None for m in motor]
    new_trees, new_houses = [], []
    dropped = lowered = raised = added = 0
    for e in STREET_EDITS:
        P = load_track(sv_dir, e["route"])
        if P is None:
            print(f"  street edit: route {e['route']} not found")
            continue
        idx = [i for i in sorted(P) if e["first"] <= i <= e["last"]]
        tr = np.array([P[i] for i in idx])
        sgn = 1.0 if e["side"] == "right" else -1.0

        def beside(q):
            """(metres to the side named in the edit, or -1 if on the other side / off the stretch)"""
            best = (1e9, -1.0)
            for a, b in zip(tr[:-1], tr[1:]):
                ab = b - a
                t = float(np.clip((q - a) @ ab / max(ab @ ab, 1e-9), 0.0, 1.0))
                p = a + t * ab
                d = float(np.hypot(*(q - p)))
                if d < best[0]:
                    right = np.array([ab[1], -ab[0]]) / max(np.hypot(*ab), 1e-9)
                    on_side = float((q - p) @ right) * sgn > 0
                    inside = 0.0 < t < 1.0 or d < 6.0
                    best = (d, d if on_side and inside else -1.0)
            return best[1]

        box = (H[:, 0] > tr[:, 0].min() - 45) & (H[:, 0] < tr[:, 0].max() + 45) & (H[:, 1] > tr[:, 1].min() - 45) & (H[:, 1] < tr[:, 1].max() + 45)
        for k in np.where(box & keepH)[0]:
            s = beside(H[k, :2])
            if s < 0 or not (e["near"] <= s <= e["far"]):
                continue
            if e.get("drop"):
                keepH[k] = False
                dropped += 1
            elif e.get("low"):
                H[k, 5], H[k, 6], H[k, 7] = 1, 0, e.get("roof", 2)
                lowered += 1
            elif e.get("tall"):
                H[k, 5] = max(H[k, 5], 4 + (k % 2))
                H[k, 6] = BAZAAR_WALLS[k % len(BAZAAR_WALLS)]
                raised += 1
        if e.get("tall"):   # real footprints in the row: the bazaar's height too
            for bi, b in enumerate(world["osmBuildings"]):
                if b.get("name"):
                    continue
                s = beside(centres[bi])
                if s >= 0 and e["near"] <= s <= e["far"] + 4.0 and (b.get("levels") or 0) < 4:
                    b["levels"] = 4 + (bi % 2)
                    raised += 1
        if e.get("fill"):
            added += fill_row(tr, sgn, H, keepH, new_houses, centres, radii, motor, rng)
        if e.get("trees"):
            step = e["trees"]
            why = {"road": 0, "mall": 0, "building": 0, "house": 0, "ok": 0}
            for a, b in zip(tr[:-1], tr[1:]):
                ab = b - a
                L = float(np.hypot(*ab))
                right = np.array([ab[1], -ab[0]]) / max(L, 1e-9) * sgn
                for s in np.arange(rng.uniform(0, step), L, step):
                    off = rng.uniform(e["near"] + 1.0, e["far"])
                    q = a + ab * (s / L) + right * off + rng.normal(0, 1.2, 2)
                    if min(dist_to(q[None], U1)[0], dist_to(q[None], L1)[0]) < 5.0:
                        why["mall"] += 1
                        continue
                    if any(dist_to(q[None], m)[0] < 4.5 for m in motor if len(m) > 1 and np.min(np.hypot(*(m - q).T)) < 60):
                        why["road"] += 1
                        continue
                    if any(point_in(blocks[j], q) or dist_to(q[None], np.vstack([blocks[j], blocks[j][:1]]))[0] < 1.5
                           for j in np.where(np.hypot(*(centres - q).T) < radii + 2.0)[0]):
                        why["building"] += 1
                        continue
                    hk = keepH & (np.hypot(H[:, 0] - q[0], H[:, 1] - q[1]) < 0.5 * np.maximum(H[:, 2], H[:, 3]) + 1.0)
                    if hk.any():
                        why["house"] += 1
                        continue
                    why["ok"] += 1
                    kind = 2.0 if rng.random() < 0.75 else 0.0   # banj oak mostly, a deodar here and there
                    new_trees.append([round(float(q[0]), 2), round(float(q[1]), 2), kind, round(float(rng.uniform(1.0, 1.35)), 2)])
            print(f"  trees beside {e['route'][-5:]} {e['first']}-{e['last']} {e['side']}: {why}")
    for (x, z), (lv, wall, roof) in HOUSE_EDITS:
        d = np.hypot(H[:, 0] - x, H[:, 1] - z)
        k = int(np.argmin(d))
        if d[k] < 1.5:
            H[k, 5], H[k, 6], H[k, 7] = lv, wall, roof
        else:
            print(f"  house edit: no house at ({x}, {z})")
    print(f"  street edits: {dropped} houses dropped, {lowered} lowered, {raised} raised to bazaar height, {added} added in the gaps, "
          f"{len(new_trees)} trees planted, {len(HOUSE_EDITS)} houses restyled")
    if new_houses:
        H = np.vstack([H, np.array(new_houses)])
        keepH = np.concatenate([keepH, np.ones(len(new_houses), bool)])
    if new_trees:
        T = np.vstack([T, np.array(new_trees)])
        keepT = np.concatenate([keepT, np.ones(len(new_trees), bool)])
    return H, keepH, T, keepT


if __name__ == "__main__":
    main()

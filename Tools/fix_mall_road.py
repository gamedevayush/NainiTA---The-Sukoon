"""
Mall Road clean-up after the Street View fit (user, 2026-11-19: "mallroad ki haalat kharab ho gayi hai, terrain ubad
khabad ho gayi hai near toll plaza, poori check karo, kisi ghar me na ghuse").

1. The Tallital junction: the fit moved Upper Mall so its end stopped 5.9 m short of where the Tallital link
   ("Mall Road", from the chowk) and Lower Mall meet; the ground between the three road ends was shaped by none of
   them (bumps). The ends of Upper and Lower Mall are pulled onto the link's end, the pull easing out over EASE m.
2. Generated houses standing in a Mall carriageway are pushed out sideways (clear of the road edge by CLEAR m), not
   removed (the house count stays).
3. Trees in a Mall carriageway are pushed out the same way.
Real footprints (osmBuildings) are only reported. A copy of world.json goes to Generated/world.before_mallfix.json.
  python fix_mall_road.py <world.json> [--dry]
Then Build World.
"""
import json, math, os, shutil, sys
import numpy as np

WORLD = sys.argv[1]
DRY = "--dry" in sys.argv
EASE, CLEAR, SNAP = 30.0, 0.8, 8.0
w = json.load(open(WORLD, encoding="utf-8"))
mall = [r for r in w["roads"] if "Mall" in (r.get("name") or "")]


def P(r):
    p = r["pts"]
    return np.array(p, float).reshape(-1, 2)


def put(r, a):
    r["pts"] = [round(float(v), 2) for v in a.reshape(-1)]


def ease_end(a, target, at_start):
    """Move one end of polyline a onto target; the shift fades out over EASE metres along the line."""
    if at_start:
        a = a[::-1].copy()
    d = target - a[-1]
    s = 0.0
    for i in range(len(a) - 1, -1, -1):
        if i < len(a) - 1:
            s += float(np.linalg.norm(a[i + 1] - a[i]))
        f = max(0.0, 1.0 - s / EASE)
        if f <= 0:
            break
        a[i] = a[i] + d * (0.5 - 0.5 * math.cos(math.pi * f))
    a[-1] = target
    return a[::-1] if at_start else a


# ---- 1. junctions: every Mall road end within SNAP m of another Mall road's end goes onto the main link's end
log = []
link = [r for r in mall if r["name"] == "Mall Road"]
for L in link:
    for end in (P(L)[0], P(L)[-1]):
        for r in mall:
            if r is L:
                continue
            a = P(r)
            for at_start in (True, False):
                e = a[0] if at_start else a[-1]
                gap = float(np.linalg.norm(e - end))
                if 0.05 < gap < SNAP:
                    a = ease_end(a, end.copy(), at_start)
                    log.append("joined %s %s to %s end (%.1f, %.1f): was %.1f m off" % (r["name"], "start" if at_start else "end", L["name"], end[0], end[1], gap))
            put(r, a)

# ---- 2/3. houses and trees out of the carriageways
segs = []
for r in mall:
    a = P(r)
    for i in range(len(a) - 1):
        segs.append((a[i], a[i + 1], r["width"] * 0.5))


def nearest(q):
    best = None
    for a, b, hw in segs:
        ab = b - a
        t = float(np.clip(np.dot(q - a, ab) / max(1e-9, np.dot(ab, ab)), 0, 1))
        c = a + ab * t
        d = float(np.linalg.norm(q - c))
        if best is None or d < best[0]:
            best = (d, c, hw, ab)
    return best


def push_out(q, reach):
    """reach: how far the thing extends from its centre toward the road. Returns the new centre or None."""
    d, c, hw, ab = nearest(q)
    need = hw + CLEAR + reach
    if d >= need:
        return None
    n = q - c
    if np.linalg.norm(n) < 1e-3:
        n = np.array([-ab[1], ab[0]])
    n = n / np.linalg.norm(n)
    return c + n * need


def near_roads(xy, margin):
    """Indices of points within margin of a Mall segment's box (numpy prefilter before the exact test)."""
    keep = np.zeros(len(xy), bool)
    lo = np.min([np.minimum(a, b) for a, b, _ in segs], axis=0) - margin
    hi = np.max([np.maximum(a, b) for a, b, _ in segs], axis=0) + margin
    box = np.all((xy >= lo) & (xy <= hi), axis=1)
    idx = np.nonzero(box)[0]
    sub = xy[idx]
    for a, b, hw in segs:
        m = hw + margin
        keep[idx[np.all((sub >= np.minimum(a, b) - m) & (sub <= np.maximum(a, b) + m), axis=1)]] = True
    return np.nonzero(keep)[0]


H = w["houses"]
moved_h = 0
hxy = np.array(H, float).reshape(-1, 9)[:, :2]
from shapely.geometry import Polygon, LineString
lines = [(LineString(P(r)), r["width"] * 0.5) for r in mall]


def rect(x, z, wd, dp, yaw):
    c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    return Polygon([(x + c * a * wd / 2 - s * b * dp / 2, z + s * a * wd / 2 + c * b * dp / 2) for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))])


for k in near_roads(hxy, 25.0):
    i = int(k) * 9
    x, z, wd, dp, yaw = H[i:i + 5]
    q = np.array([x, z])
    for _ in range(6):   # exact: the footprint must stay CLEAR m off every Mall road's edge
        poly = rect(q[0], q[1], wd, dp, yaw)
        worst = None
        for ln, hw in lines:
            short = hw + CLEAR - poly.distance(ln)
            if short > 0.01 and (worst is None or short > worst[0]):
                worst = (short, ln)
        if worst is None:
            break
        short, ln = worst
        on = ln.interpolate(ln.project(poly.centroid))
        n = np.array([poly.centroid.x - on.x, poly.centroid.y - on.y])
        if np.linalg.norm(n) < 1e-3:
            n = np.array([1.0, 0.0])
        q = q + n / np.linalg.norm(n) * (short + 0.05)
    if q[0] != x or q[1] != z:
        log.append("house %d (%.1f, %.1f) %.0fx%.0f m moved %.1f m to (%.1f, %.1f)" % (i // 9, x, z, wd, dp, math.hypot(q[0] - x, q[1] - z), q[0], q[1]))
        H[i], H[i + 1] = round(float(q[0]), 2), round(float(q[1]), 2)
        moved_h += 1

T = w.get("trees", [])
moved_t = 0
if T:
    stride = 4   # x, z, kind, scale
    txy = np.array(T, float).reshape(-1, stride)[:, :2]
    for k in near_roads(txy, 2.0):
        i = int(k) * stride
        q = np.array([T[i], T[i + 1]])
        nq = push_out(q, 0.6)
        if nq is not None:
            T[i], T[i + 1] = round(float(nq[0]), 2), round(float(nq[1]), 2)
            moved_t += 1

print("\n".join(log))
print("junction fixes %d, houses moved %d, trees moved %d" % (sum(1 for l in log if l.startswith("joined")), moved_h, moved_t))
if not DRY:
    bak = os.path.join(os.path.dirname(WORLD), "..", "Generated", "world.before_mallfix.json")
    os.makedirs(os.path.dirname(bak), exist_ok=True)
    if not os.path.exists(bak):
        shutil.copy(WORLD, bak)
    json.dump(w, open(WORLD, "w", encoding="utf-8"), separators=(",", ":"))
    print("written", WORLD)

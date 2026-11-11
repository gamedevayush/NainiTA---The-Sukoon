"""Turn raw DEM + OSM into game-ready data for the Unity world builder.

Usage:  python -I process_data.py <raw_dir> <out_dir> [preview.png]

Outputs in <out_dir>:
  heightmap.bytes  uint16 little-endian, N x N, row 0 = south edge, col 0 = west edge
  world.json       meta, lake, roads, OSM buildings, generated houses, trees, POIs, areas

Everything uses local metres: x = east, z = north, (0, 0) = map centre.
"""
import json
import math
import pathlib
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import area  # noqa: E402

N = area.HEIGHTMAP_RES
CELL = area.SIZE_M / (N - 1)
HALF = area.HALF
X0, Z0 = area.X0, area.Z0          # the map's west and south edges (local metres)
MAP_CX, MAP_CZ = area.MAP_CX, area.MAP_CZ
SEED = 4242
EDGE_MARGIN = 25.0  # metres kept free at the map border

ROAD_WIDTH = {
    "trunk": 8.0, "trunk_link": 6.5, "primary": 7.5, "secondary": 7.0, "tertiary": 6.0,
    "unclassified": 5.0, "residential": 4.5, "living_street": 4.0, "service": 3.5,
    "pedestrian": 5.5, "track": 3.0, "path": 1.8, "footway": 2.0, "steps": 2.2, "bridleway": 2.5,
}
MINOR_ROADS = {"path", "footway", "steps", "bridleway", "track"}
SPLAT_RES = 4096   # terrain texture weights: Unity's maximum alphamap; 1.46 m texels on the 6 km map, so road edges can blend


# ----------------------------------------------------------------------------- helpers
def to_grid(x, z):
    return (np.asarray(x) - X0) / CELL, (np.asarray(z) - Z0) / CELL


def bilinear(img, x, y):
    """Sample img[row, col] at fractional (col=x, row=y)."""
    x0 = np.clip(np.floor(x).astype(np.int64), 0, img.shape[1] - 2)
    y0 = np.clip(np.floor(y).astype(np.int64), 0, img.shape[0] - 2)
    fx = np.clip(x - x0, 0.0, 1.0)
    fy = np.clip(y - y0, 0.0, 1.0)
    a, b = img[y0, x0], img[y0, x0 + 1]
    c, d = img[y0 + 1, x0], img[y0 + 1, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def sample_h(h, x, z):
    gx, gz = to_grid(x, z)
    return bilinear(h, gx, gz)


def box_blur_axis(a, r, axis):
    pad = [(0, 0), (0, 0)]
    pad[axis] = (r + 1, r)
    c = np.cumsum(np.pad(a, pad, mode="edge"), axis=axis)
    k = 2 * r + 1
    if axis == 0:
        return (c[k:, :] - c[:-k, :]) / k
    return (c[:, k:] - c[:, :-k]) / k


def gauss(a, r, passes=3):
    for _ in range(passes):
        a = box_blur_axis(box_blur_axis(a, r, 0), r, 1)
    return a


def value_noise(shape, cell_px, rng):
    gh, gw = shape[0] // cell_px + 3, shape[1] // cell_px + 3
    coarse = rng.random((gh, gw)).astype(np.float32) * 2 - 1
    img = Image.fromarray(coarse).resize((gw * cell_px, gh * cell_px), Image.BICUBIC)
    return np.asarray(img)[: shape[0], : shape[1]].astype(np.float64)


def poly_mask(polys_xz, size=N, cell=CELL, fill=255):
    """Rasterise polygons (list of (k,2) metre arrays) to a PIL 'L' mask on the grid."""
    img = Image.new("L", (size, size), 0)
    dr = ImageDraw.Draw(img)
    for p in polys_xz:
        if len(p) < 3:
            continue
        pts = [((x - X0) / cell, (z - Z0) / cell) for x, z in p]
        dr.polygon(pts, fill=fill)
    return img


def erosion_distance(mask_img, steps, filt=3):
    """Approx distance (in grid steps) from outside into the mask, capped at `steps`."""
    dist = np.zeros((mask_img.size[1], mask_img.size[0]), np.float64)
    cur = mask_img
    for _ in range(steps):
        arr = np.asarray(cur) > 0
        if not arr.any():
            break
        dist += arr
        cur = cur.filter(ImageFilter.MinFilter(filt))
    return dist * (filt // 2)


def dilation_distance(mask_img, steps):
    """Approx distance (in grid steps) from the mask outwards; 0 inside, steps+1 beyond reach."""
    arr = np.asarray(mask_img) > 0
    dist = np.where(arr, 0.0, float(steps + 1))
    cur = mask_img
    for k in range(1, steps + 1):
        cur = cur.filter(ImageFilter.MaxFilter(3))
        a = np.asarray(cur) > 0
        dist[a & (dist > k)] = k
    return dist


def road_edge_distance(roads, kinds, reach_m):
    """Metres from each grid cell to the nearest road surface of the given kinds (0 on it), capped at reach_m + CELL."""
    img = Image.new("L", (N, N), 0)
    dr = ImageDraw.Draw(img)
    for r in roads:
        if r["kind"] not in kinds or len(r["pts"]) < 2:
            continue
        pts = [((x - X0) / CELL, (z - Z0) / CELL) for x, z in r["pts"]]
        wpx = max(1, int(round(r["width"] / CELL)))
        dr.line(pts, fill=255, width=wpx, joint="curve")
        rr = wpx / 2.0
        for x, y in (pts[0], pts[-1]):   # round ends
            dr.ellipse([x - rr, y - rr, x + rr, y + rr], fill=255)
    steps = int(math.ceil(reach_m / CELL))
    return dilation_distance(img, steps) * CELL


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def resample_polyline(pts, step):
    pts = np.asarray(pts, np.float64)
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)])
    if s[-1] < 1e-6:
        return pts[:1]
    n = max(2, int(math.ceil(s[-1] / step)) + 1)
    t = np.linspace(0.0, s[-1], n)
    return np.stack([np.interp(t, s, pts[:, 0]), np.interp(t, s, pts[:, 1])], axis=1)


def moving_avg(v, win):
    if win <= 1 or len(v) < 3:
        return v
    r = min(win // 2, (len(v) - 1) // 2)
    if r < 1:
        return v
    p = np.pad(v, (r, r), mode="edge")
    c = np.cumsum(np.concatenate([[0.0], p]))
    return (c[2 * r + 1:] - c[: -2 * r - 1]) / (2 * r + 1)


def polygon_area(p):
    x, z = p[:, 0], p[:, 1]
    return 0.5 * (np.dot(x, np.roll(z, -1)) - np.dot(z, np.roll(x, -1)))


def flat(arr, nd=2):
    return [round(float(v), nd) for v in np.asarray(arr).ravel()]


# ----------------------------------------------------------------------------- DEM
def load_dem(raw):
    files = sorted((raw / "dem").glob("15_*_*.png"))
    coords = [tuple(map(int, f.stem.split("_")[1:])) for f in files]
    tx0, ty0 = min(c[0] for c in coords), min(c[1] for c in coords)
    w = (max(c[0] for c in coords) - tx0 + 1) * 256
    hgt = (max(c[1] for c in coords) - ty0 + 1) * 256
    mosaic = np.zeros((hgt, w), np.float64)
    for f, (tx, ty) in zip(files, coords):
        a = np.asarray(Image.open(f).convert("RGB"), dtype=np.float64)
        e = a[..., 0] * 256 + a[..., 1] + a[..., 2] / 256 - 32768
        mosaic[(ty - ty0) * 256:(ty - ty0 + 1) * 256, (tx - tx0) * 256:(tx - tx0 + 1) * 256] = e
    # Fill DEM voids / spikes (terrarium encodes them as huge negative values).
    # Nothing in this 4 km window is below ~1450 m, so lower values are void/spike artefacts.
    bad = (mosaic < 1200) | (mosaic > 9000)
    # Spikes: pixels far from their 5x5 neighbourhood median.
    pad = np.pad(np.where(bad, np.nan, mosaic), 2, mode="edge")
    stack = np.stack([pad[dy:dy + mosaic.shape[0], dx:dx + mosaic.shape[1]] for dy in range(5) for dx in range(5)])
    med = np.nanmedian(stack, axis=0)
    bad |= np.abs(mosaic - np.nan_to_num(med, nan=mosaic.mean())) > 45.0
    del stack
    print(f"  DEM void/spike pixels: {int(bad.sum())}")
    w = (~bad).astype(np.float64)
    vals = np.where(bad, 0.0, mosaic)
    for _ in range(200):
        if not bad.any():
            break
        s = box_blur_axis(box_blur_axis(vals, 1, 0), 1, 1)
        c = box_blur_axis(box_blur_axis(w, 1, 0), 1, 1)
        fill = bad & (c > 1e-6)
        mosaic[fill] = s[fill] / c[fill]
        vals[fill] = mosaic[fill]
        w[fill] = 1.0
        bad &= ~fill
    n = 2 ** 15
    idx = np.arange(N)
    lat = area.SOUTH + idx / (N - 1) * (area.NORTH - area.SOUTH)
    lon = area.WEST + idx / (N - 1) * (area.EAST - area.WEST)
    px = ((lon + 180.0) / 360.0 * n - tx0) * 256 - 0.5
    py = ((1 - np.arcsinh(np.tan(np.radians(lat))) / np.pi) / 2 * n - ty0) * 256 - 0.5
    PX, PY = np.meshgrid(px, py)
    return bilinear(mosaic, PX, PY)


# ----------------------------------------------------------------------------- OSM
def geom_xz(geom):
    return np.array([area.to_local(g["lat"], g["lon"]) for g in geom], np.float64)


def assemble_rings(member_geoms):
    """Join open way segments of a multipolygon into closed rings."""
    segs = [list(map(tuple, g)) for g in member_geoms if len(g) >= 2]
    rings = []
    while segs:
        ring = segs.pop(0)
        changed = True
        while changed and ring[0] != ring[-1]:
            changed = False
            for i, s in enumerate(segs):
                if s[0] == ring[-1]:
                    ring += s[1:]
                elif s[-1] == ring[-1]:
                    ring += s[::-1][1:]
                elif s[-1] == ring[0]:
                    ring = s[:-1] + ring
                elif s[0] == ring[0]:
                    ring = s[::-1][:-1] + ring
                else:
                    continue
                segs.pop(i)
                changed = True
                break
        rings.append(np.array(ring))
    return rings


def element_polygons(e):
    if e["type"] == "way" and "geometry" in e:
        p = geom_xz(e["geometry"])
        return [p[:-1] if len(p) > 3 and np.allclose(p[0], p[-1]) else p]
    if e["type"] == "relation":
        outers = [geom_xz(m["geometry"]) for m in e.get("members", [])
                  if m.get("role") == "outer" and "geometry" in m]
        out = []
        for r in assemble_rings(outers):
            out.append(r[:-1] if len(r) > 3 and np.allclose(r[0], r[-1]) else r)
        return out
    return []


def in_map(p, lim):
    """Points (n x 2, or one x/z pair) within lim of the map square's centre on both axes."""
    p = np.asarray(p, np.float64)
    return (np.abs(p[..., 0] - MAP_CX) <= lim) & (np.abs(p[..., 1] - MAP_CZ) <= lim)


def clip_polyline(p, lim):
    """Split a polyline into parts that stay inside the map square shrunk to lim from its centre."""
    inside = in_map(p, lim)
    parts, cur = [], []
    for pt, ok in zip(p, inside):
        if ok:
            cur.append(pt)
        elif cur:
            parts.append(np.array(cur))
            cur = []
    if cur:
        parts.append(np.array(cur))
    return [q for q in parts if len(q) >= 2]


MANUAL_POIS = [
    ("Tallital Roadways Bus Station", "bus_station", 29.379946, 79.463361),   # the Bank of Baroda building, faces the junction
    ("Bishop Shaw School", "school", 29.379901, 79.463032),
    ("Tallital Traffic Post", "traffic_post", 29.380171, 79.463502),          # booth on the junction island
    # Mallital, from the user's Street View and photo spheres (2026-11-11)
    ("Tibetan Market", "market", 29.389273, 79.454010),   # stone plaza outside Naina Devi's gate (photo spheres)
    ("Naina Devi Gate", "gate", 29.389210, 79.454206),   # lavender pillars, golden arch, opens NW onto the plaza
    ("Mallital Police Booth", "police", 29.391375, 79.455845),   # Street View at the Mallital end of Mall Road, Aug 2022
    ("Bandstand", "bandstand", 29.390626, 79.455351),   # lakeside by the Flats (approximate)
    ("Mallital Boat Point", "boat_point", 29.390834, 79.455526),   # ghat with moored boats (photo sphere)
    ("Children's Park (Capitol)", "park", 29.390481, 79.455243),   # park in front of Capitol Cinema
    # viewpoints, checked on Google Maps and against the DEM (2026-11-11)
    ("Tiffin Top", "viewpoint", 29.383629, 79.450561),          # Dorothy's Seat on Ayarpatta (OSM's point is 40 m down the slope)
    ("Naina Peak summit", "viewpoint", 29.404085, 79.440937),   # the lookout on the top, 2,615 m (DEM summit)
    ("Himalaya Darshan", "viewpoint", 29.402843, 79.451440),    # on the Kilbury road, looks NNE at the snow peaks
    ("Tower 360", "attraction", 29.395852, 79.460669),          # Snow View's 360 tower ride (Google Maps)
    ("Ropeway Mallital station", "ropeway", 29.392629, 79.455104),   # the cable car's lower station (Google Maps)
    ("Zoo entrance", "gate", 29.380815, 79.467706),             # where the zoo road reaches the zoo (Google Maps "Nainital Zoo")
    # the Khurpatal road (Google Maps, 2026-11-11): Barapathar's pony stand, Lover's Point, Sariyatal, the falls, Khurpatal
    ("Barapathar", "pony_stand", 29.389363, 79.442088),          # ponies up to Tiffin Top and Land's End leave from here
    ("Lovers Point", "viewpoint", 29.387094, 79.440321),
    ("Khurpatal Lake View Point", "viewpoint", 29.380123, 79.435894),
    ("Sariyatal", "lake_point", 29.379448, 79.430917),
    ("Sariyatal Falls", "waterfall", 29.377938, 79.430688),      # "Woodland water fall"
    ("Khurpatal View Point", "viewpoint", 29.374623, 79.429318),
    ("Khurpatal", "lake_point", 29.372686, 79.428287),
]


def parse_osm(raw):
    els = json.loads((raw / "osm.json").read_text(encoding="utf-8"))["elements"]
    out = {"lake": None, "ponds": [], "roads": [], "buildings": [], "pois": [], "areas": []}
    lim = HALF - EDGE_MARGIN
    seen_poi = set()
    for e in els:
        t = e.get("tags", {})
        name = t.get("name:en") or t.get("name") or ""
        if t.get("natural") == "water":
            for p in element_polygons(e):
                if len(p) < 3:
                    continue
                if name == "Naini Lake" or (out["lake"] is None and abs(polygon_area(p)) > 1e5):
                    out["lake"] = p
                else:
                    out["ponds"].append({"name": name, "pts": p})
        if "highway" in t and e["type"] == "way" and "geometry" in e:
            kind = t["highway"]
            if kind not in ROAD_WIDTH or t.get("area") == "yes":
                continue
            if name == "Zoo Road Nainital":
                kind = "tertiary"   # OSM calls it a track; it is a narrow paved road with a zoo shuttle on it (2025 videos)
            for part in clip_polyline(geom_xz(e["geometry"]), lim):
                out["roads"].append({"name": name, "kind": kind, "width": 4.5 if name == "Zoo Road Nainital" else ROAD_WIDTH[kind], "pts": part})
        if "building" in t and e["type"] in ("way", "relation"):
            for p in element_polygons(e):
                if len(p) >= 3 and np.all(in_map(p, lim)):
                    lv = t.get("building:levels", "")
                    levels = int(float(lv)) if lv.replace(".", "", 1).isdigit() else 0
                    kind = t.get("amenity") or t.get("tourism") or t.get("building")
                    out["buildings"].append({"name": name, "kind": kind, "levels": levels, "pts": p})
        area_kind = None
        if t.get("natural") in ("wood", "scrub", "grassland"):
            area_kind = t["natural"]
        elif t.get("landuse") in ("forest", "grass", "meadow", "residential", "commercial", "cemetery"):
            area_kind = t["landuse"]
        elif t.get("leisure") in ("park", "pitch", "stadium", "garden", "golf_course", "playground"):
            area_kind = t["leisure"]
        elif t.get("tourism") == "zoo":
            area_kind = "zoo"
        if area_kind and e["type"] in ("way", "relation"):
            for p in element_polygons(e):
                if len(p) >= 3:
                    out["areas"].append({"kind": area_kind, "name": name, "pts": p})
        # Points of interest (nodes, or centroid of named ways)
        poi_kind = (t.get("tourism") or t.get("historic") or
                    (t.get("amenity") if t.get("amenity") in ("place_of_worship", "bus_station", "school",
                                                              "college", "university", "hospital",
                                                              "restaurant", "cafe", "fast_food") else None) or
                    (t.get("leisure") if t.get("leisure") in ("stadium", "park") else None) or
                    ("shop" if "shop" in t else None) or
                    ("peak" if t.get("natural") == "peak" else None))
        if poi_kind and name:
            if e["type"] == "node":
                x, z = area.to_local(e["lat"], e["lon"])
            elif "geometry" in e:
                g = geom_xz(e["geometry"])
                x, z = g[:, 0].mean(), g[:, 1].mean()
            elif "bounds" in e:
                b = e["bounds"]
                x, z = area.to_local((b["minlat"] + b["maxlat"]) / 2, (b["minlon"] + b["maxlon"]) / 2)
            else:
                continue
            key = (name, round(x), round(z))
            if in_map((x, z), lim) and key not in seen_poi:
                seen_poi.add(key)
                sub = t.get("religion", "") if poi_kind == "place_of_worship" else ""
                out["pois"].append({"name": name, "kind": poi_kind, "sub": sub, "x": x, "z": z})
    if out["lake"] is None:
        raise RuntimeError("Naini Lake polygon not found in OSM data")
    # places OSM lacks, located from the user's photos and a Street View check (2026-11-11):
    # the Tallital roadways bus station sits ~50 m SW of the lake-corner junction, the spired stone school behind it
    for name, kind, lat, lon in MANUAL_POIS:
        x, z = area.to_local(lat, lon)
        out["pois"].append({"name": name, "kind": kind, "sub": "", "x": x, "z": z})
    return out


# ----------------------------------------------------------------------------- terrain shaping
# Mall Road runs on a narrow shelf between the lake and the hill. The 30 m DEM cannot resolve that
# shelf (samples flip between hillside and shore: 1.6-19 m above the lake, grades up to 29 %), so its
# profile is set by design: metres above the lake, a long smoothing window, and a grade limit.
MALL_RANGE = {"Lower Mall Road": (2.0, 6.0), "Upper Mall Road": (4.0, 13.0), "Mall Road": (1.6, 13.0), "Thandi Sadak": (3.0, 14.0)}
MALL_SMOOTH = 201     # samples (~1 m each)
MALL_MAX_GRADE = 0.04


def limit_grade(ph, max_step):
    """Limit the height change per sample, keeping both end heights where possible."""
    f, end = ph.copy(), ph[-1]
    for i in range(1, len(f)):
        f[i] = np.clip(f[i], f[i - 1] - max_step, f[i - 1] + max_step)
    f[-1] = end
    for i in range(len(f) - 2, -1, -1):
        f[i] = np.clip(f[i], f[i + 1] - max_step, f[i + 1] + max_step)
    return f


def level_mall_road(roads, lake_level):
    fam = [r for r in roads if r["name"] in MALL_RANGE]
    for r in fam:
        lo, hi = MALL_RANGE[r["name"]]
        r["profile"] = np.clip(moving_avg(r["profile"], MALL_SMOOTH), lake_level + lo, lake_level + hi)
    # segments that meet must meet at one height
    ends = [(r, i) for r in fam for i in (0, len(r["profile"]) - 1)]
    used = set()
    for a in range(len(ends)):
        if a in used:
            continue
        pa = ends[a][0]["line"][ends[a][1]]
        group = [b for b in range(a, len(ends)) if b not in used and
                 np.hypot(*(ends[b][0]["line"][ends[b][1]] - pa)) < 25.0]
        used.update(group)
        lvl = float(np.mean([ends[g][0]["profile"][ends[g][1]] for g in group]))
        for g in group:
            ends[g][0]["profile"][ends[g][1]] = lvl
    for r in fam:
        step = np.linalg.norm(r["line"][1] - r["line"][0])
        r["profile"] = limit_grade(r["profile"], MALL_MAX_GRADE * step)


# The real Mall Road is two roads side by side: Upper Mall runs a step above Lower Mall, with a whitewashed
# wall between them, and Upper Mall has an unbroken row of shop-houses against the hill.
MALL_UPPER, MALL_LOWER = "Upper Mall Road", "Lower Mall Road"
MALL_WIDTH = 6.0      # each carriageway
MALL_GAP = 2.2        # between them: lamp strip and retaining wall
LOWER_HALF = 2.4      # Lower Mall's centre line to its lake parapet: narrower than Upper (Street View 2022)
MALL_STEP_SOUTH = 1.6  # Upper above Lower at the Tallital end: Upper Mall's railing looks down on Lower Mall (Street View 2022)
MALL_STEP_NORTH = 2.2  # from the middle on: a mossy stone wall ~2.2 m high under Upper Mall's railing (Street View 2022)
MALL_STEP_FROM, MALL_STEP_TO = 0.35, 0.5   # fraction of Lower Mall, from its south end, over which the wall grows
SHOP_SETBACK = 2.6    # Upper's edge to the shop fronts
# Stretches of Upper Mall's hill side that are one tall retaining wall, not shops (Street View 2022):
# (t from, t to, wall height m, face's distance from the centre line m); WorldBuilder.Mall.cs: UpperWalls
UPPER_WALLS = [(0.40, 0.445, 3.0, 5.0), (0.475, 0.53, 4.5, 3.6)]


def mall_pair(roads):
    up = next((r for r in roads if r["name"] == MALL_UPPER), None)
    lo = next((r for r in roads if r["name"] == MALL_LOWER), None)
    return (up, lo) if up is not None and lo is not None else (None, None)


def nearest_on(a, b):
    """For each point of a, the index of the closest point of b."""
    return np.array([int(np.argmin(np.hypot(*(b - q).T))) for q in a])


def spread_mall(roads):
    """OSM draws the two Mall roads about 8 m apart: make room for both carriageways and the wall between them."""
    up, lo = mall_pair(roads)
    if up is None:
        return
    up["width"] = lo["width"] = MALL_WIDTH
    L = resample_polyline(lo["pts"], 2.0)
    U = resample_polyline(up["pts"], 2.0)
    s = np.arange(len(U)) * 2.0
    ends = smoothstep(0.0, 40.0, s) * smoothstep(0.0, 40.0, s[-1] - s)   # they merge at both ends
    want = MALL_WIDTH + MALL_GAP
    for _ in range(4):
        v = U - L[nearest_on(U, L)]
        d = np.maximum(np.hypot(v[:, 0], v[:, 1]), 1e-6)
        U = U + v / d[:, None] * (np.maximum(want - d, 0.0) * ends)[:, None]
        U = np.column_stack([moving_avg(U[:, 0], 9), moving_avg(U[:, 1], 9)])
    up["pts"] = U


def mall_t(line):
    """0 at the south end of a Mall road, 1 at the north end."""
    t = np.arange(len(line)) / (len(line) - 1.0)
    return t if line[0, 1] < line[-1, 1] else 1.0 - t


def step_upper_mall(roads):
    """Upper Mall follows Lower Mall's levels, a step higher, meeting it again at both ends."""
    up, lo = mall_pair(roads)
    if up is None:
        return
    j = nearest_on(up["line"], lo["line"])
    s = np.arange(len(up["line"])) * np.linalg.norm(up["line"][1] - up["line"][0])
    taper = smoothstep(0.0, 60.0, s) * smoothstep(0.0, 60.0, s[-1] - s)
    step = MALL_STEP_SOUTH + (MALL_STEP_NORTH - MALL_STEP_SOUTH) * smoothstep(MALL_STEP_FROM, MALL_STEP_TO, mall_t(lo["line"])[j])
    up["profile"] = moving_avg(lo["profile"][j] + step * taper, 31)


def mall_frame(line, other):
    """Unit tangents and normals of a line; the normals point away from the other road."""
    t = np.gradient(line, axis=0)
    t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-9)[:, None]
    n = np.column_stack([-t[:, 1], t[:, 0]])
    if ((line - other[nearest_on(line, other)]) * n).sum() < 0:
        n = -n
    return t, n


def upper_walls(U):
    """Per Upper Mall sample: the tall wall's height (0 where there is none) and its face's distance from the centre line."""
    t = mall_t(U)
    wh, face = np.zeros(len(U)), np.zeros(len(U))
    for a, b, height, f in UPPER_WALLS:
        m = (t >= a) & (t <= b)
        wh[m], face[m] = height, f
    return wh, face


def mall_row(osm):
    """Shop-houses shoulder to shoulder on the hill side of Upper Mall, in blocks broken by lanes, walls and trees.
    Returns (house rows, shelf depth per Upper Mall sample)."""
    up, lo = mall_pair(osm["roads"])
    if up is None:
        return [], None
    rng_py = random.Random(SEED + 21)
    U = up["line"]
    step = float(np.linalg.norm(U[1] - U[0]))
    _, n = mall_frame(U, lo["line"])
    others = np.vstack([resample_polyline(r["pts"], 3.0) for r in osm["roads"]
                        if r["name"] not in (MALL_UPPER, MALL_LOWER) and len(r["pts"]) > 1])
    fixed = [np.asarray(b["pts"], np.float64) for b in osm["buildings"]]
    fixed = np.vstack(fixed) if fixed else np.zeros((1, 2)) + 1e7
    half, total = MALL_WIDTH / 2.0, step * (len(U) - 1)
    shelf = np.full(len(U), 1.0)
    wall_h, wall_face = upper_walls(U)
    row = []
    s = 12.0
    while s < total - 12.0:
        block_end = min(s + rng_py.uniform(40.0, 130.0), total - 12.0)
        while s < block_end:
            w, d = rng_py.uniform(4.2, 8.0), rng_py.uniform(8.0, 10.0)
            hotel = rng_py.random() < 0.12
            if hotel:
                w, d = rng_py.uniform(12.0, 20.0), rng_py.uniform(10.0, 12.0)
            i = min(int((s + w / 2.0) / step), len(U) - 1)
            if wall_h[int(max((s - 2.0) / step, 0)):int((s + w + 2.0) / step) + 1].any():   # a tall wall here, not shops
                s += 3.0
                continue
            c = U[i] + n[i] * (half + SHOP_SETBACK + d / 2.0)
            if min(np.min(np.hypot(*(others - c).T)), np.min(np.hypot(*(fixed - c).T))) < 7.0:
                s += 3.0
                continue
            face = -n[i]
            wall = rng_py.choices([0, 1, 2, 3, 4, 6], weights=[30, 20, 25, 8, 7, 10])[0]
            roof = rng_py.choices([0, 1, 2, 4, 5, 6], weights=[14, 12, 16, 22, 14, 22])[0]
            levels = rng_py.choices([2, 3, 4], weights=[3, 5, 3])[0]
            if hotel:
                wall, levels = rng_py.choice([0, 1, 2, 2]), rng_py.choice([4, 5])
            row.append([float(c[0]), float(c[1]), w, d, math.degrees(math.atan2(face[0], face[1])),
                        levels, wall, roof, 0 if rng_py.random() < 0.65 else 1])
            a, b = int(max((s - 2.0) / step, 0)), int((s + w + 2.0) / step) + 1
            shelf[a:b] = SHOP_SETBACK + d + 1.5
            s += w + 0.05
        s += rng_py.uniform(7.0, 24.0)
    walled = wall_h > 0
    shelf[walled] = wall_face[walled] - half + 2.0   # level ground runs 2 m in behind the wall's face
    return row, moving_avg(shelf, 5)


def nearest_field(pts, radius):
    """Grid fields: distance to the nearest sample and that sample's index (-1 outside the radius)."""
    best, idx = np.full((N, N), np.inf), np.full((N, N), -1, np.int64)
    rc = int(math.ceil(radius / CELL)) + 1
    gx, gz = to_grid(pts[:, 0], pts[:, 1])
    for i in range(len(pts)):
        c0, r0 = int(round(float(gx[i]))), int(round(float(gz[i])))
        a0, a1, b0, b1 = max(r0 - rc, 0), min(r0 + rc + 1, N), max(c0 - rc, 0), min(c0 + rc + 1, N)
        rr, cc = np.ogrid[a0:a1, b0:b1]
        d = np.hypot(cc * CELL + X0 - pts[i, 0], rr * CELL + Z0 - pts[i, 1])
        closer = d < best[a0:a1, b0:b1]
        best[a0:a1, b0:b1][closer] = d[closer]
        idx[a0:a1, b0:b1][closer] = i
    return best, idx


def carve_mall(h, road_blend, roads, lake, shelf):
    """Exact levels for the two carriageways, the step between them and the flat shelf the shops stand on."""
    up, lo = mall_pair(roads)
    U, L, Uh, Lh = up["line"], lo["line"], up["profile"], lo["profile"]
    tu, nu = mall_frame(U, L)
    tl, nl = mall_frame(L, U)
    half = MALL_WIDTH / 2.0
    X, Z = np.meshgrid(np.arange(N) * CELL + X0, np.arange(N) * CELL + Z0)
    out, blend = h.copy(), road_blend.copy()

    def frame(P, T, Nr, idx):
        vx, vz = X - P[idx, 0], Z - P[idx, 1]
        return vx * Nr[idx, 0] + vz * Nr[idx, 1], vx * T[idx, 0] + vz * T[idx, 1]

    # Lower Mall's ground: the road out to its lake-side parapet, from end to end; past the parapet the bank
    # may only fall away towards the water, never rise over the road
    _, iu = nearest_field(U, 45.0)
    mu = iu >= 0
    su, au = frame(U, tu, nu, np.where(mu, iu, 0))
    _, il = nearest_field(L, 30.0)
    m = il >= 0
    ilc = np.where(m, il, 0)
    sl, al = frame(L, tl, nl, ilc)
    # towards Upper Mall the low ground runs on under the cap of the wall between them, wherever the two roads stand
    # apart, so the wall's foot meets Lower Mall with no grass lip
    lim = np.where(mu & (su < -(half - 0.5)), -14.0, -(half + MALL_GAP + 1.5))
    low = m & (np.abs(al) <= 2.5) & (sl <= LOWER_HALF + 1.0) & (sl >= lim)
    out[low] = Lh[il[low]] - 0.06
    blend[low] = 1.0
    drop = m & (np.abs(al) <= 2.5) & (sl > LOWER_HALF + 1.0) & (sl < LOWER_HALF + 16.0) & ~lake
    cap = Lh[il[drop]] - 0.06 - (sl[drop] - LOWER_HALF - 1.0) * 0.45
    out[drop] = np.minimum(out[drop], np.maximum(cap, Lh[il[drop]] - 3.5))

    m = mu
    depth = shelf[np.where(m, iu, 0)]
    along = m & (np.abs(au) <= 2.5)
    flat = along & (su >= -(half - 1.0)) & (su <= half + depth)
    out[flat] = Uh[iu[flat]] - 0.06
    blend[flat] = 1.0
    bank = along & (su > half + depth) & (su < half + depth + 12.0)
    t = smoothstep(0.0, 12.0, su[bank] - half - depth[bank])
    out[bank] = (Uh[iu[bank]] - 0.06) * (1.0 - t) + h[bank] * t
    # behind the tall walls the hillside stands at the wall's top, then eases back into the slope
    wall_h, wall_face = upper_walls(U)
    # ease the raised ground in and out over 12 m at each end of a wall, so it never ends in a cliff
    ends = np.zeros(len(U))
    on = wall_h > 0
    step_u = float(np.linalg.norm(U[1] - U[0]))
    k = max(1, int(round(12.0 / step_u)))
    for i in np.where(on)[0]:
        lo, hi = max(0, i - k), min(len(U), i + k + 1)
        ends[i] = min(1.0, min(np.sum(on[lo:i + 1]), np.sum(on[i:hi])) / k)
    wh_eased = wall_h * smoothstep(0.0, 1.0, ends)
    iuc = np.where(m, iu, 0)
    rel = su - wall_face[iuc] - 3.0
    tall = along & (wh_eased[iuc] > 0) & (rel >= 0.0) & (rel < 20.0)
    tb = smoothstep(8.0, 20.0, rel[tall])
    out[tall] = np.maximum(out[tall], (Uh[iu[tall]] + wh_eased[iu[tall]]) * (1.0 - tb) + h[tall] * tb)
    out[lake] = h[lake]
    return out, blend


MOTOR_ROADS = {"trunk", "trunk_link", "primary", "primary_link", "secondary", "secondary_link", "tertiary", "tertiary_link",
               "unclassified", "residential", "living_street", "service"}


def off_the_roads(houses, buildings, roads, reach=0.8):
    """Drop houses and unnamed OSM buildings that stand more than `reach` metres into a motor road's carriageway."""
    A, B, Hw = [], [], []
    for r in roads:
        if r["kind"] not in MOTOR_ROADS or len(r["pts"]) < 2:
            continue
        pts = np.asarray(r.get("line", r["pts"]), np.float64)
        half = (2.0 * LOWER_HALF if r["name"] == MALL_LOWER else r["width"]) / 2.0
        A.append(pts[:-1]); B.append(pts[1:]); Hw.append(np.full(len(pts) - 1, half))
    A, B, Hw = np.vstack(A), np.vstack(B), np.concatenate(Hw)
    AB = B - A
    L2 = np.maximum((AB * AB).sum(1), 1e-9)

    def into(P):
        """How far the deepest of points P reaches into any carriageway (m; <= 0: clear)."""
        lo, hi = P.min(0) - 20.0, P.max(0) + 20.0
        near = np.all((np.minimum(A, B) < hi) & (np.maximum(A, B) > lo), axis=1)
        if not near.any():
            return -1.0
        a, ab, l2, hw = A[near], AB[near], L2[near], Hw[near]
        t = np.clip(((P[:, None, :] - a[None]) * ab[None]).sum(2) / l2[None], 0.0, 1.0)
        d = np.hypot(*(P[:, None, :] - (a[None] + ab[None] * t[..., None])).transpose(2, 0, 1))
        return float(np.max(hw[None] - d))

    def footprint(x, z, w, d, yaw):
        a = math.radians(yaw)
        rv = np.array([math.cos(a), -math.sin(a)]) * w / 2.0
        fv = np.array([math.sin(a), math.cos(a)]) * d / 2.0
        c = np.array([x, z])
        C = [c + rv + fv, c - rv + fv, c - rv - fv, c + rv - fv]
        edge = [C[i] * (1 - t) + C[(i + 1) % 4] * t for i in range(4) for t in np.linspace(0.0, 1.0, 5)]
        return np.array(edge + [c] + [c + (q - c) * 0.5 for q in C])

    kept = [h for h in houses if into(footprint(*h[:5])) <= reach]
    rest = []
    for b in buildings:
        P = np.asarray(b["pts"], np.float64)
        c = P.mean(0)
        if b.get("name") or into(np.vstack([P, c, (P + c) / 2.0])) <= reach:
            rest.append(b)
    print(f"  off the roads: {len(houses) - len(kept)} houses, {len(buildings) - len(rest)} OSM buildings stood in a carriageway")
    return kept, rest


def merge_mall_row(houses, buildings, row):
    """Add the shop row and drop generated houses that stand where it goes."""
    R = np.array([[r[0], r[1], max(r[2], r[3])] for r in row])

    def clash(x, z, size):
        return bool(np.any(np.hypot(R[:, 0] - x, R[:, 1] - z) < 0.5 * (R[:, 2] + size) * 0.95))

    kept = [b for b in houses if not clash(b[0], b[1], max(b[2], b[3]))]
    rest = []
    for b in buildings:
        c = np.mean(np.asarray(b["pts"], np.float64), axis=0)
        if b["kind"] != "house" or not clash(c[0], c[1], 6.0):
            rest.append(b)
    return kept + row, rest


STREET_ROW_RADIUS = 115.0   # Tallital: shops shoulder to shoulder along every road within this distance of the chowk


def tallital_centre(osm):
    """The lake corner nearest the Tallital roadways station (the chowk)."""
    stn = [p for p in osm["pois"] if p["name"] == "Tallital Roadways Bus Station"]
    if not stn:
        return None
    lake = np.asarray(osm["lake"], np.float64)
    q = np.array([stn[0]["x"], stn[0]["z"]])
    return lake[int(np.argmin(np.hypot(*(lake - q).T)))]


def street_rows(osm, d_out, centre, avoid, radius=STREET_ROW_RADIUS, seed=31, placed=None, keep_out=()):
    """Shop-houses at the road edge on both sides of every road near the chowk, as in the Street View of Tallital:
    no gaps of open ground between them, fronts on the road."""
    rng_py = random.Random(SEED + seed)
    roads = [r for r in osm["roads"] if r["kind"] not in MINOR_ROADS and r["name"] not in (MALL_UPPER, MALL_LOWER) and len(r["pts"]) > 1]
    lines = [resample_polyline(r["pts"], 1.0) for r in roads]
    near_roads = [r for r in osm["roads"] if r["kind"] not in MINOR_ROADS and len(r["pts"]) > 1]   # incl. the two Mall roads
    every = np.vstack([resample_polyline(r["pts"], 1.0) for r in near_roads])
    every_half = np.concatenate([np.full(len(resample_polyline(r["pts"], 1.0)), r["width"] / 2.0) for r in near_roads])

    def lake_d(p):
        gx, gz = to_grid(p[0], p[1])
        return float(d_out[int(np.clip(round(float(gz)), 0, N - 1)), int(np.clip(round(float(gx)), 0, N - 1))])

    row = []
    placed = [] if placed is None else placed
    for r, L in zip(roads, lines):
        if len(L) < 6 or np.min(np.hypot(*(L - centre).T)) > radius:
            continue
        half = r["width"] / 2.0
        t = np.gradient(L, axis=0)
        t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-9)[:, None]
        n = np.column_stack([-t[:, 1], t[:, 0]])
        total = len(L) - 1
        for side in (1, -1):
            s = 3.0
            while s < total - 3:
                w, d = rng_py.uniform(4.0, 7.5), rng_py.uniform(7.0, 9.5)
                i = int(min(s + w / 2, total))
                c = L[i] + side * n[i] * (half + 1.6 + d / 2)
                ok = (np.hypot(*(c - centre)) < radius and lake_d(c) > 12.0
                      and all(np.hypot(*(c - a)) >= ar + max(w, d) / 2 for a, ar in avoid)
                      and not any(point_in_poly(c, poly) for poly in keep_out))
                if ok:
                    ok = (np.min(np.hypot(*(every - c).T) - every_half) > max(w, d) / 2 + 1.0
                          and all(np.hypot(*(c - q)) >= (qr + max(w, d) / 2) * 0.92 for q, qr in placed))
                if not ok:
                    s += 2.0
                    continue
                face = -side * n[i]
                row.append([float(c[0]), float(c[1]), w, d, math.degrees(math.atan2(face[0], face[1])),
                            (rng_py.choices([3, 4, 5], weights=[3, 5, 3])[0] if np.hypot(*(c - centre)) < 60.0   # taller round the chowk
                             else rng_py.choices([1, 2, 3, 4], weights=[2, 4, 4, 2])[0]),
                            rng_py.choices([0, 1, 2, 3, 4, 6, 7], weights=[20, 18, 18, 8, 10, 10, 6])[0],
                            rng_py.choices([0, 1, 2, 4, 5, 6], weights=[14, 10, 18, 22, 12, 24])[0],
                            1 if rng_py.random() < 0.55 else 0])
                placed.append((c, max(w, d) / 2))
                s += w + 0.1
    return row


def point_in_poly(p, poly):
    poly = np.asarray(poly, np.float64)
    x, z = p
    inside = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, zi = poly[i]
        xj, zj = poly[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi + 1e-12) + xi:
            inside = not inside
        j = i
    return inside


def merge_street_row(houses, buildings, row):
    """Add the row; drop generated houses and unnamed footprints standing where it goes."""
    R = np.array([[r[0], r[1], max(r[2], r[3])] for r in row])

    def clash(x, z, size):
        return bool(np.any(np.hypot(R[:, 0] - x, R[:, 1] - z) < 0.5 * (R[:, 2] + size) * 0.95))

    kept = [b for b in houses if not clash(b[0], b[1], max(b[2], b[3]))]
    rest = []
    for b in buildings:
        c = np.mean(np.asarray(b["pts"], np.float64), axis=0)
        if b.get("name") or not clash(c[0], c[1], 6.0):
            rest.append(b)
    return kept + row, rest


def hill_forest(trees, h, lake, osm, houses, centres, radius=450.0, spacing=7.0, min_slope=25.0):
    """Steep slopes round the town are thick oak and deodar forest in every photo (behind Naina Devi, above Tallital);
    plant them wherever nothing else stands."""
    rng_py = random.Random(SEED + 77)
    gz_, gx_ = np.gradient(h, CELL)
    slope = np.degrees(np.arctan(np.hypot(gx_, gz_)))
    road_pts = [(resample_polyline(r["pts"], 2.0), r["width"] / 2.0 + 2.0) for r in osm["roads"] if len(r["pts"]) > 1]
    rp = np.vstack([p for p, _ in road_pts])
    rr = np.concatenate([np.full(len(p), w) for p, w in road_pts])
    H = np.asarray(houses, np.float64) if len(houses) else np.zeros((0, 9))
    keep_pois = [np.array([p["x"], p["z"]]) for p in osm["pois"] if p["kind"] in ("place_of_worship", "market", "gate", "bus_station", "school", "attraction")]
    old = np.array([[t[0], t[1]] for t in trees]) if trees else np.zeros((0, 2))
    added = []
    for c in centres:
        xs = np.arange(c[0] - radius, c[0] + radius, spacing)
        zs = np.arange(c[1] - radius, c[1] + radius, spacing)
        for x in xs:
            for z in zs:
                q = np.array([x + rng_py.uniform(-2.5, 2.5), z + rng_py.uniform(-2.5, 2.5)])
                if np.hypot(*(q - c)) > radius or not in_map(q, HALF - EDGE_MARGIN):
                    continue
                gx, gz = to_grid(q[0], q[1])
                ci, ri = int(round(float(gx))), int(round(float(gz)))
                if lake[ri, ci] or slope[ri, ci] < min_slope or rng_py.random() > 0.85:
                    continue
                if np.min(np.hypot(*(rp - q).T) - rr) < 0:
                    continue
                if len(H) and np.any(np.hypot(H[:, 0] - q[0], H[:, 1] - q[1]) < np.maximum(H[:, 2], H[:, 3]) * 0.6 + 2.0):
                    continue
                if any(np.hypot(*(q - k)) < 22.0 for k in keep_pois):
                    continue
                if len(old) and np.min(np.hypot(*(old - q).T)) < 4.0:
                    continue
                roll = rng_py.random()
                kind = 2 if roll < 0.55 else (0 if roll < 0.85 else 3)   # banj oak, deodar, cypress
                added.append([float(q[0]), float(q[1]), kind, rng_py.uniform(0.85, 1.35)])
    return added


# Lookouts and the compass bearing they face (None: the lake). The ground falls away in front of each, so trees there
# would only block the view: keep a wedge clear (LandmarkBuilder.cs: the same bearings).
VIEW_CONES = {"Snow View": 25.0, "Naina Peak summit": None, "Tiffin Top": None, "Land's End": 244.0, "Himalaya Darshan": 25.0,
              "Lovers Point": 225.0, "Khurpatal Lake View Point": 222.0, "Khurpatal View Point": 205.0}


LOOKOUT_FRONT = {"Snow View": 12.0}   # centre of the site to its railing (LandmarkBuilder.cs); others ~4 m


def shift_lookouts(pois, h, lake_pts):
    """Move each lookout along its view to where the ground starts to drop (>= 6 m over the next 20 m)."""
    lc = np.mean(np.asarray(lake_pts, np.float64), axis=0)
    for name, bearing in VIEW_CONES.items():
        q = next((x for x in pois if x["name"] == name), None)
        if q is None:
            continue
        pt = np.array([q["x"], q["z"]])
        f = lc - pt if bearing is None else np.array([math.sin(math.radians(bearing)), math.cos(math.radians(bearing))])
        f = f / max(np.hypot(*f), 1e-6)
        edge = None
        for d in np.arange(0.0, 90.0, 2.0):
            a, b = pt + f * d, pt + f * (d + 20.0)
            if sample_h(h, np.array([a[0]]), np.array([a[1]]))[0] - sample_h(h, np.array([b[0]]), np.array([b[1]]))[0] > 6.0:
                edge = d
                break
        if edge is None:
            print(f"  lookout {name}: no edge within 90 m, left where it is")
            continue
        move = max(0.0, edge - LOOKOUT_FRONT.get(name, 4.0))
        q["x"], q["z"] = float(pt[0] + f[0] * move), float(pt[1] + f[1] * move)
        print(f"  lookout {name}: moved {move:.0f} m to the brink")


def clear_view_cones(trees, pois, lake_pts):
    lc = np.mean(np.asarray(lake_pts, np.float64), axis=0)
    T = np.asarray(trees, np.float64)
    keep = np.ones(len(T), bool)
    for name, bearing in VIEW_CONES.items():
        p = next((np.array([q["x"], q["z"]]) for q in pois if q["name"] == name), None)
        if p is None:
            continue
        f = lc - p if bearing is None else np.array([math.sin(math.radians(bearing)), math.cos(math.radians(bearing))])
        f = f / max(np.hypot(*f), 1e-6)
        v = T[:, :2] - p
        d = np.hypot(v[:, 0], v[:, 1])
        cosang = (v @ f) / np.maximum(d, 1e-6)
        wedge = (d < 80.0) & (cosang > math.cos(math.radians(38.0)))
        keep &= ~(wedge | (d < 12.0))
        print(f"  view from {name}: cleared {int((wedge | (d < 12.0)).sum())} trees")
    return [t for t, k in zip(trees, keep) if k]


def mall_lake_trees(roads, d_out):
    """Big old broadleaf trees on the bank below Lower Mall's lake railing: a near-unbroken line north of the middle,
    scattered further south (Street View 2022)."""
    up, lo = mall_pair(roads)
    if up is None:
        return []
    rng_py = random.Random(SEED + 22)
    L = lo["line"]
    _, n = mall_frame(L, up["line"])
    t = mall_t(L)
    step, out = float(np.linalg.norm(L[1] - L[0])), []
    half = MALL_WIDTH / 2.0

    def room_at(q):
        g = to_grid(q[0], q[1])
        return float(d_out[int(round(float(g[1]))), int(round(float(g[0])))])

    s = 6.0
    while s < step * (len(L) - 1):
        i = int(s / step)
        north = t[i] >= 0.45
        room = room_at(L[i]) - LOWER_HALF - 1.0
        if north:   # a near-unbroken line right behind the railing, at the water's edge, leaning over road and lake
            off = float(np.clip(room_at(L[i]) - 0.8, LOWER_HALF + 0.95, LOWER_HALF + 3.5)) + rng_py.uniform(-0.1, 0.4)
            q = L[i] + n[i] * off
            out.append([float(q[0]), float(q[1]), 2, rng_py.uniform(1.3, 1.75)])
        elif room > 2.0:
            q = L[i] + n[i] * (LOWER_HALF + 1.4 + rng_py.uniform(0.3, min(room - 1.0, 7.0)))
            out.append([float(q[0]), float(q[1]), 2, rng_py.uniform(1.2, 1.6)])
        s += rng_py.uniform(7.0, 12.0) if north else rng_py.uniform(11.0, 24.0)
    return out


ROAD_RANK = {"trunk": 0, "trunk_link": 0, "primary": 1, "secondary": 2, "tertiary": 3,
             "unclassified": 4, "residential": 4, "living_street": 5, "service": 5, "pedestrian": 6}
ROAD_GRADE = {"trunk": 0.09, "trunk_link": 0.10, "primary": 0.10, "secondary": 0.12, "tertiary": 0.14,
              "unclassified": 0.16, "residential": 0.18, "living_street": 0.18, "service": 0.2, "pedestrian": 0.2}
SNAP_M = 10.0   # a road end this close to an earlier road takes that road's height


def even_roads(roads):
    """Bigger roads first. Local dips and bumps go; the climb over a ridge stays (the grade limit follows a long
    average of the road). Road ends take the height of the road they join, when the two are near in height."""
    todo = [r for r in roads if r["kind"] not in MINOR_ROADS and r["name"] not in MALL_RANGE and len(r["line"]) > 8]
    todo.sort(key=lambda r: (ROAD_RANK.get(r["kind"], 6), -len(r["line"])))
    for r in todo:   # first pass: smoothed shape of every road, for the ends to snap to
        r["profile_raw"] = r["profile"]
        r["profile"] = moving_avg(r["profile"], 61 if ROAD_RANK.get(r["kind"], 6) <= 3 else 41)
    others = [r for r in roads if r["kind"] not in MINOR_ROADS and len(r["line"]) > 1]
    for r in todo:
        line, ph = r["line"], r["profile"].copy()
        step = float(np.linalg.norm(line[1] - line[0]))
        for end in (0, len(ph) - 1):
            best, best_d = None, SNAP_M
            for o in others:
                if o is r:
                    continue
                d = np.hypot(o["line"][:, 0] - line[end, 0], o["line"][:, 1] - line[end, 1])
                j = int(np.argmin(d))
                if d[j] < best_d and abs(float(o["profile"][j]) - ph[end]) < 8.0:
                    best, best_d = float(o["profile"][j]), float(d[j])
            if best is not None:
                ph[end] = best
        shape = moving_avg(r["profile_raw"], 151)
        need = float(np.max(np.abs(np.diff(shape)))) / step if len(shape) > 1 else 0.0
        span = max(step * (len(ph) - 1), 1.0)
        grade = max(ROAD_GRADE.get(r["kind"], 0.18), need * 1.15, abs(ph[-1] - ph[0]) / span * 1.25)
        r["profile"] = limit_grade(ph, grade * step)
    if __import__("os").environ.get("ROAD_DEBUG"):
        import pickle
        pickle.dump([(r["name"], r["kind"], r["line"], r.get("profile_raw"), r["profile"]) for r in roads if r["kind"] not in MINOR_ROADS],
                    open(__import__("os").environ["ROAD_DEBUG"], "wb"))


def stamp_roads(h, roads, lake_level, lake_near):
    """Flatten terrain corridors along roads. Returns (new_h, road_blend)."""
    acc_w = np.zeros(N * N)
    acc_wh = np.zeros(N * N)
    acc_log = np.zeros(N * N)
    spread_mall(roads)
    for r in roads:
        minor = r["kind"] in MINOR_ROADS
        pts = resample_polyline(r["pts"], 1.0)
        ph = sample_h(h, pts[:, 0], pts[:, 1])
        ph = moving_avg(ph, 7 if minor else 21)
        # keep roads near the lake above water
        near = bilinear(lake_near, *to_grid(pts[:, 0], pts[:, 1]))
        r["line"] = pts
        r["profile"] = np.where(near > 0.5, np.maximum(ph, lake_level + 1.6), ph)
    level_mall_road(roads, lake_level)
    step_upper_mall(roads)
    even_roads(roads)
    for r in roads:
        minor = r["kind"] in MINOR_ROADS
        half = r["width"] / 2.0 + (0.3 if minor else 0.8)
        pts, ph = r["line"], r["profile"]
        if r["name"] in MALL_RANGE:
            fall = np.full(len(pts), 14.0)   # wider bank where Mall Road is cut in
        else:
            # the bank grows with the cut or fill, so it lies at ~30 deg instead of standing as a wall (user, 2026-11-11:
            # "kuch jagah terrain bahut gadbad hai")
            base, cap = (2.5, 10.0) if minor else (6.0, 26.0)
            diff = np.abs(ph - sample_h(h, pts[:, 0], pts[:, 1]))
            fall = np.clip(moving_avg(diff, 15) * 1.7 + base * 0.5, base, cap)
        R = half + float(fall.max())
        rc = int(math.ceil(R / CELL)) + 1
        dx, dz = np.meshgrid(np.arange(-rc, rc + 1), np.arange(-rc, rc + 1))
        dx, dz = dx.ravel(), dz.ravel()
        for s in range(0, len(pts), 3000):
            P, H, FL = pts[s:s + 3000], ph[s:s + 3000], fall[s:s + 3000]
            gx, gz = to_grid(P[:, 0], P[:, 1])
            cx = np.rint(gx).astype(np.int64)[:, None] + dx[None, :]
            cz = np.rint(gz).astype(np.int64)[:, None] + dz[None, :]
            wx = cx * CELL + X0 - P[:, 0:1]
            wz = cz * CELL + Z0 - P[:, 1:2]
            d = np.sqrt(wx * wx + wz * wz)
            w = 1.0 - smoothstep(half, half + FL[:, None], d)
            ok = (cx >= 0) & (cx < N) & (cz >= 0) & (cz < N) & (w > 0.001)
            idx = (cz * N + cx)[ok]
            ww = w[ok]
            hh = np.broadcast_to(H[:, None], w.shape)[ok]
            acc_w += np.bincount(idx, ww, N * N)
            acc_wh += np.bincount(idx, ww * hh, N * N)
            acc_log += np.bincount(idx, np.log1p(-np.minimum(ww, 0.999)), N * N)
    acc_w = acc_w.reshape(N, N)
    target = np.where(acc_w > 0, acc_wh.reshape(N, N) / np.maximum(acc_w, 1e-9), h)
    blend = 1.0 - np.exp(acc_log.reshape(N, N))
    return h * (1 - blend) + target * blend, blend


def build_terrain(raw, osm, rng):
    print("DEM sampling ...")
    h = load_dem(raw)
    print(f"  raw elevation {h.min():.1f} .. {h.max():.1f} m")
    h = gauss(h, 3)  # ~6 m sigma: removes SRTM stair artefacts

    lake_img = poly_mask([osm["lake"]])
    lake = np.asarray(lake_img) > 0
    lake_level = float(np.median(h[lake]))
    print(f"  lake level (DEM median) {lake_level:.1f} m, lake cells {lake.sum()}")

    # Distance inside the lake (for bed depth) and outside it (for banks).
    d_in = erosion_distance(lake_img, 120) * CELL
    reach = int(math.ceil(64.0 / CELL))   # metres, not cells: the cell size changes with the map (31 cells was 60 m at 1.95 m)
    d_out_cells = np.minimum(dilation_distance(lake_img, reach), reach + 1)
    d_out = np.where(lake, 0.0, d_out_cells * CELL)
    near_lake = (~lake) & (d_out <= 45.0)

    # Banks: an embankment (stone wall) right at the shore, terrain never below water nearby.
    bank_req = lake_level + 1.5 + np.maximum(0.0, d_out - 10.0) * 0.02
    bank_w = 1.0 - smoothstep(10.0, 45.0, d_out)
    h = np.where(near_lake, np.maximum(h, h * (1 - bank_w) + np.maximum(h, bank_req) * bank_w), h)
    h = np.where(near_lake, np.maximum(h, lake_level + 0.7), h)

    # Lake bed: shallow at edge, ~20 m in the middle (real max depth ~27 m).
    bed = lake_level - np.minimum(20.0, 1.2 + d_in * 0.22)
    h = np.where(lake, bed, h)

    lake_near_f = np.where(d_out < 60.0, 1.0, 0.0) * (~lake)

    # The small lakes (Sariyatal, Khurpatal ...): level from the ground under them, a bed below it, a rim above it.
    for pond in osm["ponds"]:
        pts = np.asarray(pond["pts"], np.float64)
        if len(pts) < 3 or abs(polygon_area(pts)) < 2500.0 or "sukha" in pond["name"].lower():
            continue   # Sukha Tal is mostly a dry bed
        pimg = poly_mask([pts])
        pm = np.asarray(pimg) > 0
        if pm.sum() < 20:
            continue
        lvl = float(np.median(h[pm]))
        pin = erosion_distance(pimg, 40) * CELL
        pout = np.minimum(dilation_distance(pimg, 6), 7) * CELL
        rim = (~pm) & (pout <= 8.0)
        h = np.where(rim, np.maximum(h, lvl + 0.35 + pout * 0.05), h)
        h = np.where(pm, lvl - np.minimum(6.0, 0.6 + pin * 0.18), h)
        pond["level"] = round(lvl, 3)
        print(f"  pond {pond['name'] or '(unnamed)'}: level {lvl:.1f} m, {int(pm.sum())} cells")

    # The Flats (stadium) and other sports grounds are flat.
    for a in osm["areas"]:
        if a["kind"] in ("stadium", "pitch"):
            m = np.asarray(poly_mask([a["pts"]])) > 0
            if m.any():
                lvl = max(float(np.median(h[m])), lake_level + 1.4)
                soft = gauss(m.astype(np.float64), 2, 2)
                h = h * (1 - soft) + lvl * soft

    print("Flattening roads ...")
    h, road_blend = stamp_roads(h, osm["roads"], lake_level, lake_near_f)
    osm["mall_row"], shelf = mall_row(osm)
    if osm["mall_row"]:
        h, road_blend = carve_mall(h, road_blend, osm["roads"], lake, shelf)

    # Small natural detail away from roads/lake/town.
    detail = (value_noise(h.shape, 18, rng) * 1.1 + value_noise(h.shape, 6, rng) * 0.35)
    keep = (1 - road_blend) * smoothstep(6.0, 40.0, d_out) * (~lake)
    h = h + detail * keep
    h, osm["falls_clear"] = carve_falls(h, osm["pois"], road_blend, osm["roads"]) if FALLS_ENABLED else (h, None)
    return h, lake, lake_level, d_out, road_blend


# The Woodland falls below Sariyatal (2025-26 walking videos): two streams over a ~8 m mossy rock face into a shallow
# rocky pool, then white water over grey slabs down a bamboo gully. The 30 m DEM shows only a 1-in-5 slope there.
FALLS_ENABLED = False   # parked (user, 2026-11-11): the waterfall is not at Sariyatal - re-check its real place first
FALLS_DROP = 8.0      # metres, the rock face
FALLS_FACE = (3.0, 6.0)   # along the stream from the lip: where the face starts and ends
FALLS_POOL = 12.0     # pool centre, metres from the lip


def carve_falls(h, pois, road_blend, roads):
    """Cut the falls into the ground: the lip, the rock face (curved round the pool like an amphitheatre), the plunge
    pool and the gully below it. Only ever lowers the ground, and never where a road is. As in the videos the falls sit
    just below the road, reached by steps: the lip is the steepest spot near the POI whose whole cut stays off the roads
    and is within a short stair of one. Moves the "Sariyatal Falls" POI to the lip and adds "Sariyatal Falls pool", so the
    builder knows which way the water goes."""
    p = next((q for q in pois if q["name"] == "Sariyatal Falls"), None)
    if p is None:
        return h, None
    c = np.array([p["x"], p["z"]], np.float64)

    def H(pt):
        return float(sample_h(h, np.array([pt[0]]), np.array([pt[1]]))[0])

    src = next((q for q in pois if q["name"] == "Sariyatal"), None)   # the water comes out of Sariyatal, so it runs away from it
    away = c - np.array([src["x"], src["z"]]) if src else np.array([0.0, -1.0])
    away = away / max(float(np.hypot(*away)), 1e-6)
    gz_n, gx_n = road_blend.shape

    def rb(q):
        gx, gz = to_grid(q[0], q[1])
        return road_blend[int(np.clip(round(float(gz)), 0, gz_n - 1)), int(np.clip(round(float(gx)), 0, gx_n - 1))]

    segs = []
    for r in roads:
        pts = np.asarray(r["pts"], np.float64)
        if r["kind"] in MINOR_ROADS or len(pts) < 2 or np.min(np.hypot(pts[:, 0] - c[0], pts[:, 1] - c[1])) > 200.0:
            continue
        segs += [(pts[k], pts[k + 1], r.get("width", 6.0)) for k in range(len(pts) - 1)]

    def road_edge_dist(q):
        best = 1e9
        for a, b, wd in segs:
            ab = b - a
            tt = np.clip(np.dot(q - a, ab) / max(np.dot(ab, ab), 1e-9), 0.0, 1.0)
            best = min(best, float(np.hypot(*(a + ab * tt - q))) - wd * 0.5)
        return best

    best_score, lip, d = -1e9, c.copy(), np.array([0.0, -1.0])
    for ox in np.arange(-50.0, 50.1, 2.5):
        for oz in np.arange(-50.0, 50.1, 2.5):
            q = c + np.array([ox, oz])
            off = float(np.hypot(ox, oz))
            if off > 50.0:
                continue
            e = road_edge_dist(q)
            if not (3.0 <= e <= 16.0):   # a short flight of steps from the road
                continue
            hq = H(q)
            for k in range(36):
                v = np.array([math.cos(k * math.pi / 18.0), math.sin(k * math.pi / 18.0)])
                drop = hq - H(q + v * 28.0)
                if drop < 3.0 or np.dot(v, away) < 0.25:
                    continue
                n = np.array([-v[1], v[0]])
                if any(rb(q + v * t + n * sd) > 0.02 for t in (2.0, 8.0, 14.0, 20.0, 28.0) for sd in (-10.0, -5.0, 0.0, 5.0, 10.0)):
                    continue
                score = drop - 0.06 * off
                if score > best_score:
                    best_score, lip, d = score, q, v
    if best_score < -1e8:
        print("  Sariyatal Falls: no clear spot near the POI, not carved")
        return h, None
    h_lip = H(lip)
    # local window, in grid cells
    R = 80.0
    gx0, gz0 = to_grid(lip[0] - R, lip[1] - R)
    gx1, gz1 = to_grid(lip[0] + R, lip[1] + R)
    i0, i1 = int(max(0, gz0)), int(min(h.shape[0], gz1 + 1))
    j0, j1 = int(max(0, gx0)), int(min(h.shape[1], gx1 + 1))
    zz, xx = np.mgrid[i0:i1, j0:j1]
    wx, wz = X0 + xx * CELL, Z0 + zz * CELL
    rx, rz = wx - lip[0], wz - lip[1]
    t = rx * d[0] + rz * d[1]                 # along the stream
    sd = rx * -d[1] + rz * d[0]               # across it
    te = t - 0.05 * sd * sd                   # the face curves round the pool
    orig = h[i0:i1, j0:j1].copy()
    a0, a1 = FALLS_FACE
    floor = h_lip - FALLS_DROP - 0.8 - 0.18 * np.maximum(0.0, t - (FALLS_POOL + 5.0))   # pool bed, then the gully
    face = h_lip - FALLS_DROP * smoothstep(a0, a1, te) - 0.8 * smoothstep(a1, a1 + 1.0, te)
    target = np.where(te < a1 + 1.0, face, floor)
    pool_r = np.hypot((t - FALLS_POOL) / 6.5, sd / 5.5)
    target = np.where(pool_r < 1.0, np.minimum(target, h_lip - FALLS_DROP - 0.8 - 0.9 * (1.0 - pool_r * pool_r)), target)
    w_side = 1.0 - smoothstep(6.0, 14.0, np.abs(sd) - np.maximum(0.0, t - FALLS_POOL) * 0.06)
    w_along = smoothstep(a0 - 1.0, a0, te) * (1.0 - smoothstep(28.0, 55.0, t))
    w = w_side * w_along * (1.0 - np.clip(road_blend[i0:i1, j0:j1] * 4.0, 0.0, 1.0))
    h[i0:i1, j0:j1] = np.minimum(orig, orig + (target - orig) * w)
    pool = lip + d * FALLS_POOL
    p["x"], p["z"] = float(lip[0]), float(lip[1])
    pois.append({"name": "Sariyatal Falls pool", "kind": "waterfall_pool", "sub": "", "x": float(pool[0]), "z": float(pool[1])})
    print(f"  Sariyatal Falls: lip {np.hypot(*(lip - c)):.0f} m from the POI, {road_edge_dist(lip):.0f} m below the road, face {FALLS_DROP:.0f} m, "
          f"pool {H(pool):.1f} m (lip {h_lip:.1f} m), bearing {math.degrees(math.atan2(d[0], d[1])) % 360:.0f} deg")
    # nothing grows in the pool or on the face; bamboo and trees can line the gully
    return h, [(float(lip[0] + d[0] * k), float(lip[1] + d[1] * k), 9.0) for k in (2.0, 7.0, 12.0, 17.0)]


# ----------------------------------------------------------------------------- placement
def oriented_rect_cells(cx, cz, w, d, yaw, gap, occ_cell):
    """Occupancy-grid cell indices covered by a rotated rectangle (+gap)."""
    hw, hd = w / 2 + gap, d / 2 + gap
    rad = math.hypot(hw, hd)
    c0 = int((cx - rad - X0) / occ_cell); c1 = int((cx + rad - X0) / occ_cell) + 1
    r0 = int((cz - rad - Z0) / occ_cell); r1 = int((cz + rad - Z0) / occ_cell) + 1
    cols, rows = np.meshgrid(np.arange(c0, c1 + 1), np.arange(r0, r1 + 1))
    px = (cols + 0.5) * occ_cell + X0 - cx
    pz = (rows + 0.5) * occ_cell + Z0 - cz
    cs, sn = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    lx = px * cs - pz * sn
    lz = px * sn + pz * cs
    m = (np.abs(lx) <= hw) & (np.abs(lz) <= hd)
    return rows[m], cols[m]


def place_houses(h, lake, lake_level, d_out, road_blend, osm, rng, occ, occ_cell, generate=True):
    gz_, gx_ = np.gradient(h, CELL)
    slope = np.degrees(np.arctan(np.hypot(gx_, gz_)))

    # Lake proximity on a coarse grid (distance up to ~2.5 km).
    small = 257
    sc = area.SIZE_M / (small - 1)
    lake_small = poly_mask([osm["lake"]], small, sc)
    d_lake_small = dilation_distance(lake_small, 180) * sc
    d_lake = np.asarray(Image.fromarray(d_lake_small.astype(np.float32)).resize((N, N), Image.BILINEAR))

    road_core = np.where(road_blend > 0.6, 1.0, 0.0)
    road_near = np.clip(gauss(road_core, 12, 2) * 6.0, 0.0, 1.0)  # ~ within 40-60 m of a road

    wood = np.asarray(poly_mask([a["pts"] for a in osm["areas"] if a["kind"] in ("wood", "forest")])) > 0
    rel = h - lake_level
    elev_f = smoothstep(-5.0, 4.0, rel) * (1.0 - smoothstep(170.0, 320.0, rel))
    urban = elev_f * np.clip(1.0 - d_lake / 1250.0, 0.0, 1.0) ** 1.3 * (0.2 + 0.8 * road_near)
    urban *= np.where(wood, 0.25, 1.0)
    urban *= 1.0 - smoothstep(34.0, 44.0, slope)

    houses = []
    if not generate:   # real footprints are used instead (see real_buildings)
        return houses, slope, urban, wood
    spacing = 8.0
    xs = np.arange(X0 + EDGE_MARGIN, X0 + 2 * HALF - EDGE_MARGIN, spacing)
    zs = np.arange(Z0 + EDGE_MARGIN, Z0 + 2 * HALF - EDGE_MARGIN, spacing)
    CX, CZ = np.meshgrid(xs, zs)
    CX = (CX + rng.uniform(-3, 3, CX.shape)).ravel()
    CZ = (CZ + rng.uniform(-3, 3, CZ.shape)).ravel()
    U = bilinear(urban, *to_grid(CX, CZ))
    sel = (U > 0.02) & (rng.random(U.shape) < np.minimum(0.95, U * 1.15))
    order = rng.permutation(np.flatnonzero(sel))
    rng_py = random.Random(SEED)
    wall_w = [16, 14, 11, 9, 8, 8, 7, 7, 6, 5, 5, 4]  # palette weights (see WorldBuilder colours)
    roof_w = [22, 16, 14, 10, 8, 6, 5]
    for k in order:
        x, z, u = float(CX[k]), float(CZ[k]), float(U[k])
        gx, gz = to_grid(x, z)
        ci, ri = int(round(float(gx))), int(round(float(gz)))
        if lake[ri, ci] or d_out[ri, ci] < 7.0:
            continue
        w = rng_py.uniform(6.5, 13.0) if u > 0.4 else rng_py.uniform(6.0, 10.0)
        d = rng_py.uniform(6.0, 11.0)
        # Face downhill: local +z (front) points down the slope.
        g = np.array([gx_[ri, ci], gz_[ri, ci]])
        yaw = math.degrees(math.atan2(-g[0], -g[1])) if np.hypot(*g) > 0.05 else rng_py.uniform(0, 360)
        yaw += rng_py.uniform(-8, 8)
        rr, cc = oriented_rect_cells(x, z, w, d, yaw, 1.2, occ_cell)
        if len(rr) == 0 or rr.min() < 0 or cc.min() < 0 or rr.max() >= occ.shape[0] or cc.max() >= occ.shape[1]:
            continue
        if occ[rr, cc].any():
            continue
        rr2, cc2 = oriented_rect_cells(x, z, w, d, yaw, 0.0, occ_cell)
        occ[rr2, cc2] = True
        levels = rng_py.choices([1, 2, 3, 4, 5], weights=[1, 5, 6, 3, 1] if u > 0.45 else [3, 6, 3, 1, 0])[0]
        wall = rng_py.choices(range(len(wall_w)), weights=wall_w)[0]
        roof = rng_py.choices(range(len(roof_w)), weights=roof_w)[0]
        roof_type = 0 if rng_py.random() < 0.72 else 1  # 0 = sloped tin, 1 = flat terrace
        houses.append([x, z, w, d, yaw, levels, wall, roof, roof_type])
    return houses, slope, urban, wood


# ----------------------------------------------------------------------------- real buildings (Overture)
import re

WALL_WEIGHTS = [20, 14, 12, 12, 10, 6, 8, 5, 3, 3, 4, 3]   # white, cream, yellow, pink, sky, green, peach, stone, lavender, brick, mustard, teal
ROOF_WEIGHTS = [26, 12, 18, 8, 14, 6, 16]              # red tin, maroon, green, dark green, blue grey, rust, grey


def wkt_exterior_rings(wkt):
    """Exterior rings of a POLYGON / MULTIPOLYGON WKT as lists of (lon, lat)."""
    rings = []
    for m in re.finditer(r"\(\(([^()]+)\)", wkt or ""):
        pts = [tuple(map(float, q.split()[:2])) for q in m.group(1).split(",") if q.strip()]
        if len(pts) >= 4:
            rings.append(pts)
    return rings


FLOOR_M = 3.0   # one storey, for turning a measured height into floors


def load_overture_buildings(raw):
    p = raw / "overture" / "buildings.json"
    if not p.exists():
        return None
    lim = HALF - EDGE_MARGIN
    out = []
    hp = raw / "open_buildings" / "building_heights.json"   # Tools/fetch_open_buildings.py
    measured = json.loads(hp.read_text(encoding="utf-8")) if hp.exists() else {}
    for r in json.loads(p.read_text(encoding="utf-8")):
        if r.get("source") == "OpenStreetMap" or r.get("is_underground"):
            continue   # OSM buildings come from osm.json (with names and kinds)
        for ring in wkt_exterior_rings(r.get("wkt")):
            pts = np.array([area.to_local(lat, lon) for lon, lat in ring], np.float64)
            if np.allclose(pts[0], pts[-1]):
                pts = pts[:-1]
            if len(pts) < 3 or not np.all(in_map(pts, lim)):
                continue
            if abs(polygon_area(pts)) < 12.0:
                continue   # sheds, noise
            out.append({"pts": pts, "floors": r.get("num_floors"), "name": r.get("name") or "",
                        "height": (measured.get(r.get("id")) or {}).get("h")})
    return out


def min_area_rect(pts):
    """Smallest enclosing rectangle: centre, width (local x), depth (local z), Unity yaw (deg)."""
    P = sorted(set(map(tuple, np.round(pts, 3))))
    if len(P) < 3:
        return None

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for q in P:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], q) <= 0:
            lower.pop()
        lower.append(q)
    for q in reversed(P):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], q) <= 0:
            upper.pop()
        upper.append(q)
    hull = np.array(lower[:-1] + upper[:-1])
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
            best = (w * d, w, d, ux, c)
    if best is None:
        return None
    _, w, d, ux, c = best
    # Unity: Euler(0, yaw, 0) * right = (cos yaw, -sin yaw) in XZ
    return c, w, d, math.degrees(math.atan2(-ux[1], ux[0]))


def real_buildings(ob, h, lake, road_blend, d_out, lake_level, occ, occ_cell):
    """Overture footprints -> rectangular houses (house kit: windows, balconies, tin or flat roofs)
    and irregular footprints (extruded polygons). Fronts face downhill, as in the hill town."""
    gz_, gx_ = np.gradient(h, CELL)
    rng_py = random.Random(SEED + 7)
    houses, footprints = [], []
    for k, b in enumerate(ob):
        pts = b["pts"]
        cx, cz = pts[:, 0].mean(), pts[:, 1].mean()
        gx, gz = to_grid(cx, cz)
        ci, ri = int(round(float(gx))), int(round(float(gz)))
        if not (0 <= ci < N and 0 <= ri < N) or lake[ri, ci] or road_blend[ri, ci] > 0.7:
            continue
        a = abs(polygon_area(pts))
        # floors: Overture's count if any, else the satellite height (Open Buildings 2.5D),
        # else a guess from footprint size that follows the measured spread (mostly 1-3 floors)
        near = d_out[ri, ci] < 260.0
        fl = b.get("floors")
        if fl:
            levels = int(max(1, min(8, fl)))
        elif b.get("height"):
            levels = int(max(1, min(8, round(b["height"] / FLOOR_M))))
        elif a < 30:
            levels = rng_py.choice([1, 1, 2])
        elif a < 80:
            levels = rng_py.choice([1, 2, 2, 3] + ([3] if near else []))
        elif a < 200:
            levels = rng_py.choice([2, 2, 3, 3] + ([4] if near else []))
        else:
            levels = rng_py.choice([2, 3, 3, 4])
        wall = rng_py.choices(range(len(WALL_WEIGHTS)), weights=WALL_WEIGHTS)[0]
        roof = rng_py.choices(range(len(ROOF_WEIGHTS)), weights=ROOF_WEIGHTS)[0]
        roof_type = 1 if rng_py.random() < (0.28 if a < 120 else 0.5) else 0
        r = min_area_rect(pts)
        rect_fill = a / max(r[1] * r[2], 1e-3) if r else 0
        if r and rect_fill > 0.72 and max(r[1], r[2]) <= 45.0 and min(r[1], r[2]) >= 3.0:
            c, w, d, yaw = r
            g = np.array([gx_[ri, ci], gz_[ri, ci]])
            if np.hypot(*g) > 0.05:   # turn so the front (+z) faces downhill; swap sides on quarter turns
                down = -g / np.hypot(*g)
                best, best_dot = 0, -9.0
                for q in range(4):
                    yy = math.radians(yaw + 90 * q)
                    dot = math.sin(yy) * down[0] + math.cos(yy) * down[1]
                    if dot > best_dot:
                        best, best_dot = q, dot
                yaw += 90 * best
                if best % 2 == 1:
                    w, d = d, w
            houses.append([float(c[0]), float(c[1]), float(w), float(d), float(yaw), levels, wall, roof, roof_type])
        else:
            footprints.append({"name": b.get("name", ""), "kind": "house", "levels": levels, "pts": pts})
    # keep trees off every footprint
    if ob:
        occ |= np.asarray(poly_mask([b["pts"] for b in ob], occ.shape[0], occ_cell)) > 0
    return houses, footprints


def place_trees(h, lake, lake_level, d_out, slope, urban, wood, rng, occ, occ_cell):
    trees = []
    spacing = 8.0
    rng_py = random.Random(SEED + 1)
    xs = np.arange(X0 + EDGE_MARGIN, X0 + 2 * HALF - EDGE_MARGIN, spacing)
    zs = np.arange(Z0 + EDGE_MARGIN, Z0 + 2 * HALF - EDGE_MARGIN, spacing)
    X, Z = np.meshgrid(xs, zs)
    X = X + rng.uniform(-3.6, 3.6, X.shape)
    Z = Z + rng.uniform(-3.6, 3.6, Z.shape)
    gx, gz = to_grid(X, Z)
    ci, ri = np.rint(gx).astype(int), np.rint(gz).astype(int)
    u = urban[ri, ci]
    s = slope[ri, ci]
    u_eff = u * np.where(s > 24, 0.35, 1.0)
    dens = np.where(wood[ri, ci] | (s > 28), 0.95, 0.66) * (1 - np.clip(u_eff * 1.6, 0, 0.9))
    dens *= np.where(s > 48, 0.25, 1.0)
    keep = (rng.random(X.shape) < dens) & (~lake[ri, ci]) & (d_out[ri, ci] > 5.0)
    occ_t = occ.copy()  # keep crowns ~2 m away from houses/roads
    for sh in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        occ_t |= np.roll(occ, sh, axis=(0, 1))
    oc = ((X - X0) / occ_cell).astype(int)
    orow = ((Z - Z0) / occ_cell).astype(int)
    keep &= ~occ_t[np.clip(orow, 0, occ.shape[0] - 1), np.clip(oc, 0, occ.shape[1] - 1)]
    rel = h[ri, ci] - lake_level
    for x, z, r in zip(X[keep], Z[keep], rel[keep]):
        roll = rng_py.random()
        if r > 220:
            t = 0 if roll < 0.45 else (2 if roll < 0.85 else 3)    # deodar / oak / cypress
        elif r < -60:
            t = 1 if roll < 0.7 else 2                             # chir pine lower down
        else:
            t = 0 if roll < 0.35 else (1 if roll < 0.55 else (2 if roll < 0.85 else 3))
        trees.append([x, z, t, rng_py.uniform(0.75, 1.3)])
    return trees


# ----------------------------------------------------------------------------- preview
def hillshade(h):
    gz_, gx_ = np.gradient(h, CELL)
    nx, nz, ny = -gx_, -gz_, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    L = np.array([-0.5, 0.7, 0.5]); L /= np.linalg.norm(L)
    return np.clip((nx * L[0] + ny * L[1] + nz * L[2]) / ln, 0, 1)


def write_preview(path, h, lake, osm, houses, trees, size=1400):
    shade = hillshade(h)
    rgb = np.stack([shade * 200 + 40, shade * 190 + 50, shade * 160 + 40], -1)
    rgb[lake] = [70, 110, 160]
    img = Image.fromarray(np.flipud(rgb).astype(np.uint8)).resize((size, size), Image.BILINEAR)
    dr = ImageDraw.Draw(img)
    k = size / area.SIZE_M

    def P(x, z):
        return ((x - X0) * k, (Z0 + 2 * HALF - z) * k)

    for t in trees[::3]:
        x, y = P(t[0], t[1])
        dr.point((x, y), fill=(30, 90, 40))
    for r in osm["roads"]:
        col = (90, 90, 90) if r["kind"] in MINOR_ROADS else (230, 220, 200)
        dr.line([P(x, z) for x, z in r["pts"]], fill=col, width=1 if r["kind"] in MINOR_ROADS else 2)
    for hs in houses:
        x, y = P(hs[0], hs[1])
        dr.rectangle([x - 1, y - 1, x + 1, y + 1], fill=(200, 60, 50))
    for b in osm["buildings"]:
        dr.polygon([P(x, z) for x, z in b["pts"]], fill=(255, 160, 0))
    for p in osm["pois"]:
        if p["kind"] in ("place_of_worship", "viewpoint", "attraction", "zoo", "museum", "stadium"):
            x, y = P(p["x"], p["z"])
            dr.ellipse([x - 4, y - 4, x + 4, y + 4], outline=(255, 255, 0), width=2)
            dr.text((x + 6, y - 6), p["name"][:22], fill=(255, 255, 255))
    img.save(path)


# ----------------------------------------------------------------------------- main
def main():
    raw, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    preview = pathlib.Path(sys.argv[3]) if len(sys.argv) > 3 else None
    out.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(SEED)

    osm = parse_osm(raw)
    print(f"OSM: lake {len(osm['lake'])} pts, roads {len(osm['roads'])}, buildings {len(osm['buildings'])}, "
          f"pois {len(osm['pois'])}, areas {len(osm['areas'])}")
    h, lake, lake_level, d_out, road_blend = build_terrain(raw, osm, rng)

    # The Flats (OSM "Nainital Stadium"): a level, bare ground made from the 1880 landslide debris
    flats_polys = [a["pts"] for a in osm["areas"] if a["kind"] == "stadium"]
    flats = (np.asarray(poly_mask(flats_polys)) > 0) if flats_polys else np.zeros_like(lake)
    if flats.any():
        level = lake_level + 3.0   # the DEM puts it ~9 m up; the real ground is a few metres above the water
        soft = np.clip(gauss(flats.astype(np.float64), 3, 2) * 1.6, 0.0, 1.0)
        h = h * (1 - soft) + level * soft
        print(f"  the Flats levelled at lake + {level - lake_level:.1f} m")

    shift_lookouts(osm["pois"], h, osm["lake"])

    # Occupancy grid (2 m) shared by houses and trees: roads, lake, special areas, OSM buildings, landmarks.
    occ_cell = 2.0
    on = int(area.SIZE_M / occ_cell)
    road_small = np.asarray(Image.fromarray((road_blend > 0.35).astype(np.uint8) * 255)
                            .filter(ImageFilter.MaxFilter(3)).resize((on, on), Image.NEAREST)) > 0
    lake_small = np.asarray(Image.fromarray(lake.astype(np.uint8) * 255).resize((on, on), Image.NEAREST)) > 0
    # Keep a 13 m promenade (Mall Road / Thandi Sadak side) free of houses and trees.
    promenade = (d_out < 13.0) & (~lake)
    prom_small = np.asarray(Image.fromarray(promenade.astype(np.uint8) * 255).resize((on, on), Image.NEAREST)) > 0
    occ = road_small | lake_small | prom_small
    blocked = [a["pts"] for a in osm["areas"]
               if a["kind"] in ("stadium", "pitch", "park", "zoo", "golf_course", "garden", "playground", "cemetery")]
    blocked += [b["pts"] for b in osm["buildings"]]
    blocked += [q["pts"] for q in osm["ponds"] if q.get("level")]   # nothing stands in the small lakes
    if blocked:
        occ |= np.asarray(poly_mask(blocked, on, occ_cell)) > 0
    reserve = {"place_of_worship": 28.0, "attraction": 22.0, "viewpoint": 14.0, "museum": 18.0, "waterfall": 24.0, "waterfall_pool": 18.0}
    for p in osm["pois"]:
        rad = reserve.get(p["kind"])
        if p["name"] == "Raj Bhavan":
            rad = 55.0
        if p["name"] == "Snow View":
            rad = 28.0   # the plaza on the top: shops, cafe and the ropeway station ring it
        if rad:
            yy, xx = np.ogrid[:on, :on]
            cx, cz = (p["x"] - X0) / occ_cell, (p["z"] - Z0) / occ_cell
            occ |= (xx - cx) ** 2 + (yy - cz) ** 2 <= (rad / occ_cell) ** 2

    ob = load_overture_buildings(raw)
    print("Placing houses ...")
    houses, slope, urban, wood = place_houses(h, lake, lake_level, d_out, road_blend, osm, rng, occ, occ_cell,
                                              generate=ob is None)
    if ob is not None:
        houses, extra = real_buildings(ob, h, lake, road_blend, d_out, lake_level, occ, occ_cell)
        osm["buildings"] += extra
        print(f"  real footprints (Overture): {len(ob)} -> {len(houses)} house-kit + {len(extra)} footprint buildings")
    if osm["mall_row"]:
        houses, osm["buildings"] = merge_mall_row(houses, osm["buildings"], osm["mall_row"])
        print(f"  Mall Road shop row: {len(osm['mall_row'])} houses")
    street_row = []
    tall = tallital_centre(osm)
    if tall is not None:
        avoid = [(np.array([p["x"], p["z"]]), r) for p in osm["pois"] for name, r in
                 (("Tallital Roadways Bus Station", 17.0), ("Bishop Shaw School", 18.0), ("Tallital Traffic Post", 6.0)) if p["name"] == name]
        placed = []
        street_row = street_rows(osm, d_out, tall, avoid, placed=placed)
        print(f"  Tallital street row: {len(street_row)} shop-houses")
        # Mallital and Bada Bazar (user's Street View / photo spheres): the same shops at the road edge, kept off the
        # landmarks, the Flats and the parks
        poi = {p["name"]: np.array([p["x"], p["z"]]) for p in osm["pois"]}
        keep_r = {"Naina Devi Temple": 26.0, "Gurudwara Sahib": 18.0, "Tibetan Market": 16.0, "Naina Devi Gate": 6.0,
                  "Mallital Police Booth": 5.0, "Bandstand": 8.0, "Mallital Boat Point": 10.0, "Children's Park (Capitol)": 14.0}
        avoid_m = [(poi[k], r) for k, r in keep_r.items() if k in poi]
        for b in osm["buildings"]:
            if b.get("name"):
                P = np.asarray(b["pts"], np.float64)
                avoid_m.append((P.mean(axis=0), float(np.max(np.hypot(*(P - P.mean(axis=0)).T))) + 2.0))
        keep_out = [a["pts"] for a in osm["areas"] if a["kind"] in ("stadium", "park", "playground", "pitch")]
        for name, centre, radius, seed in (("Mallital", poi.get("Nainital Stadium"), 175.0, 41), ("Bada Bazar", poi.get("Bada Bazar"), 105.0, 51)):
            if centre is None:
                continue
            more = street_rows(osm, d_out, centre, avoid_m, radius=radius, seed=seed, placed=placed, keep_out=keep_out)
            print(f"  {name} street row: {len(more)} shop-houses")
            street_row += more
        if street_row:
            houses, osm["buildings"] = merge_street_row(houses, osm["buildings"], street_row)
    houses, osm["buildings"] = off_the_roads(houses, osm["buildings"], osm["roads"])
    # the viewpoint plazas are built whole in LandmarkBuilder.cs (shops, stalls, stations): nothing else stands on them
    plaza = {"Snow View": 26.0, "Naina Peak summit": 12.0, "Tiffin Top": 12.0, "Land's End": 10.0, "Himalaya Darshan": 13.0,
             "Lovers Point": 14.0, "Barapathar": 15.0, "Khurpatal Lake View Point": 13.0, "Khurpatal View Point": 13.0}
    for name, rad in plaza.items():
        p = next((q for q in osm["pois"] if q["name"] == name), None)
        if p is not None:
            before = len(houses)
            houses = [b for b in houses if math.hypot(b[0] - p["x"], b[1] - p["z"]) > rad + max(b[2], b[3]) * 0.5]
            if before != len(houses):
                print(f"  {name}: {before - len(houses)} buildings off the plaza")
    print(f"  houses: {len(houses)}")
    print("Placing trees ...")
    trees = place_trees(h, lake, lake_level, d_out, slope, urban, wood, rng, occ, occ_cell)
    trees += mall_lake_trees(osm["roads"], d_out)
    centres = [np.array([p["x"], p["z"]]) for p in osm["pois"] if p["name"] in ("Nainital Stadium", "Tallital Roadways Bus Station")]
    forest = hill_forest(trees, h, lake, osm, houses, centres)
    trees += forest
    trees = clear_view_cones(trees, osm["pois"], osm["lake"])
    print(f"  hill forest round the town: {len(forest)} trees")
    if street_row:   # no trees growing through the new row
        SR = np.array([[r[0], r[1], max(r[2], r[3]) * 0.5 + 1.5] for r in street_row])
        trees = [t for t in trees if not np.any(np.hypot(SR[:, 0] - t[0], SR[:, 1] - t[1]) < SR[:, 2])]
    if osm.get("falls_clear"):   # the falls' face and pool
        FC = np.array(osm["falls_clear"])
        trees = [t for t in trees if not np.any(np.hypot(FC[:, 0] - t[0], FC[:, 1] - t[1]) < FC[:, 2])]
    print(f"  trees: {len(trees)}")

    # Terrain texture weights: R grass, G forest floor, B rock, A dirt/soil.
    # (re-textured 2026-11-11) Real ground in the 2025-26 videos: leaf litter wherever trees stand, dry mixed grass only in
    # the open, bare stony soil on cut banks and steep slopes, packed earth round the houses and along the road edges.
    # tree cover from the trees actually placed: under and between them the ground is leaf litter
    tpos = np.asarray([[t[0], t[1]] for t in trees], np.float64)
    cover = np.zeros(h.shape, np.float64)
    if len(tpos):
        tgx, tgz = to_grid(tpos[:, 0], tpos[:, 1])
        ok = (tgx >= 0) & (tgz >= 0) & (tgx < h.shape[1]) & (tgz < h.shape[0])
        np.add.at(cover, (tgz[ok].astype(np.int64), tgx[ok].astype(np.int64)), 1.0)
        cover = gauss(cover, max(1, int(round(4.0 / CELL))), 3) * (6.0 / CELL) ** 2   # trees per ~36 m2
    cover = np.clip(cover * 2.2, 0.0, 1.0)
    # the hills are wooded even where steep: stone and bare soil show only where few trees stand
    rock = smoothstep(34.0, 48.0, slope) * (1.0 - 0.6 * cover)
    # road cuts: the steep bank beside a road is broken stone and soil, whatever grows above it
    near_road = np.clip(gauss((road_blend > 0.05).astype(np.float64), max(1, int(round(5.0 / CELL))), 3) * 3.0, 0.0, 1.0)
    rock = np.maximum(rock, smoothstep(30.0, 42.0, slope) * near_road * 0.9)
    bare = smoothstep(24.0, 34.0, slope) * np.clip(0.45 + 0.8 * value_noise(h.shape, 14, rng), 0.0, 1.0) * (1.0 - 0.75 * cover)   # eroded soil patches
    if ob:   # real town ground: bare earth / paving around the actual footprints, not lawns
        built = np.asarray(poly_mask([b["pts"] for b in ob])) > 0
        urban = np.maximum(urban, np.clip(gauss(built.astype(np.float64), 5, 2) * 5.0, 0.0, 1.0))
    # Roads baked into the ground (user, 2026-11-11: "road ke side grass direct na ho, dono ke beech blend ho"): beside
    # the asphalt, grey grit and gravel, then trodden brown earth, then the grass or leaf litter - each edge ragged.
    d_road = road_edge_distance(osm["roads"], MOTOR_ROADS, 7.0)
    d_path = road_edge_distance(osm["roads"], MINOR_ROADS | {"pedestrian"}, 3.0)
    jag = np.clip(0.5 + 0.55 * value_noise(h.shape, 3, rng) + 0.3 * value_noise(h.shape, 9, rng), 0.0, 1.0)
    grit = 1.0 - smoothstep(0.4, 1.0 + 1.4 * jag, d_road)
    earth = np.maximum(1.0 - smoothstep(1.2, 2.8 + 2.6 * jag, d_road), 0.85 * (1.0 - smoothstep(0.3, 0.9 + 1.4 * jag, d_path)))
    rock = np.maximum(rock, grit * 0.8)
    # Houses baked in the same way (user, 2026-11-11: "house ek dam se ghaas pe na aaye"): round every footprint a band
    # of grey grit and broken concrete (the plinth apron, drains, steps), then trodden earth, then the natural ground.
    feet = []
    for hx, hz, hw, hd, hyaw in ((q[0], q[1], q[2], q[3], q[4]) for q in houses):
        a_ = math.radians(hyaw)
        Rv, Fv = np.array([math.cos(a_), -math.sin(a_)]) * hw * 0.5, np.array([math.sin(a_), math.cos(a_)]) * hd * 0.5
        cc = np.array([hx, hz])
        feet.append(np.array([cc - Rv + Fv, cc + Rv + Fv, cc + Rv - Fv, cc - Rv - Fv]))
    feet += [np.asarray(b_["pts"], np.float64) for b_ in osm["buildings"] if len(b_["pts"]) >= 3]   # only what is built (not the dropped footprints)
    d_house = dilation_distance(poly_mask(feet), int(math.ceil(6.0 / CELL))) * CELL
    apron = 1.0 - smoothstep(0.2, 0.9 + 1.0 * jag, d_house)
    yard = 1.0 - smoothstep(0.9, 2.4 + 2.8 * jag, d_house)
    rock = np.maximum(rock, apron * 0.75)
    earth = np.maximum(earth, yard * 0.9)
    # the built places (lookouts, temples, the pony stand, stalls): trodden earth round them, not lawn up to the plinth
    for q in osm["pois"]:
        if q["kind"] not in ("viewpoint", "attraction", "place_of_worship", "museum", "pony_stand", "gate", "lake_point"):
            continue
        cx, cz = (q["x"] - X0) / CELL, (q["z"] - Z0) / CELL
        rr = 16.0 / CELL
        i0, i1, j0, j1 = int(max(0, cz - rr - 2)), int(min(N, cz + rr + 2)), int(max(0, cx - rr - 2)), int(min(N, cx + rr + 2))
        yy_, xx_ = np.ogrid[i0:i1, j0:j1]
        dd = np.hypot(xx_ - cx, yy_ - cz) * CELL
        earth[i0:i1, j0:j1] = np.maximum(earth[i0:i1, j0:j1], 1.0 - smoothstep(8.0, 11.0 + 5.0 * jag[i0:i1, j0:j1], dd))
    # (2026-11-11, after the first true render) Earth only where feet and wheels make it: round the houses, along the
    # roads, at the built places and on eroded banks. A blanket of earth over the whole town read as a dry, bare hill;
    # between the houses Nainital is green - grass, weeds and leaf litter - so the town term is a light, patchy one.
    town_patch = np.clip(urban * (0.18 + 0.5 * np.clip(value_noise(h.shape, 11, rng), 0.0, 1.0)), 0.0, 1.0)
    dirt = np.clip(np.maximum(town_patch + road_blend * 0.22, np.maximum(earth * 0.9, bare * 0.55)), 0.0, 1.0)
    if flats.any():
        dirt = np.maximum(dirt, np.clip(gauss(flats.astype(np.float64), 2, 2) * 1.5, 0.0, 1.0))
    dirt = np.where(lake, 1.0, dirt)
    wood_soft = np.clip(gauss(wood.astype(np.float64), 4, 2), 0.0, 1.0)
    forest = np.clip(cover * 1.1 + wood_soft * 0.25 + 0.3 * value_noise(h.shape, 40, rng) - 0.1 - urban * 0.35, 0.0, 1.0)
    print(f"  ground: tree cover mean {cover.mean():.2f}, forest floor {forest.mean():.2f}, bare banks {bare.mean():.3f}, rock {rock.mean():.3f}")
    w_rock = rock
    w_dirt = (1 - rock) * dirt
    w_forest = (1 - rock) * (1 - dirt) * forest
    w_grass = (1 - rock) * (1 - dirt) * (1 - forest)
    splat = np.stack([w_grass, w_forest, w_rock, w_dirt], -1)
    splat /= np.maximum(splat.sum(-1, keepdims=True), 1e-6)
    # Resize each channel on its own: resizing an RGBA image premultiplies alpha and would
    # wipe the RGB weights wherever the dirt (alpha) weight is zero.
    chans = [Image.fromarray(np.flipud(np.rint(splat[..., i] * 255)).astype(np.uint8)).resize((SPLAT_RES, SPLAT_RES), Image.BILINEAR)
             for i in range(4)]
    Image.merge("RGBA", chans).save(out / "splat.png")

    h_min, h_max = float(h.min()) - 2.0, float(h.max()) + 2.0
    u16 = np.rint((h - h_min) / (h_max - h_min) * 65535).astype("<u2")
    (out / "heightmap.bytes").write_bytes(u16.tobytes())

    lake_pts = osm["lake"]
    if polygon_area(lake_pts) < 0:
        lake_pts = lake_pts[::-1]
    world = {
        "meta": {
            "sizeM": area.SIZE_M, "res": N, "hMin": round(h_min, 3), "hMax": round(h_max, 3),
            "lakeLevel": round(lake_level, 3), "mapCx": MAP_CX, "mapCz": MAP_CZ, "centerLat": area.CENTER_LAT, "centerLon": area.CENTER_LON,
            "attribution": "Elevation: AWS Terrain Tiles (SRTM, public domain). Map data (c) OpenStreetMap contributors, ODbL; buildings: Overture Maps Foundation (Google Open Buildings, Microsoft ML Buildings), ODbL/CDLA.",
        },
        "lake": {"name": "Naini Lake", "pts": flat(lake_pts)},
        "ponds": [{"name": p["name"], "pts": flat(p["pts"]), "level": p.get("level", 0.0)} for p in osm["ponds"]],
        "roads": [{"name": r["name"], "kind": r["kind"], "width": r["width"], "pts": flat(r["pts"])}
                  for r in osm["roads"]],
        "osmBuildings": [{"name": b["name"], "kind": b["kind"], "levels": b["levels"], "pts": flat(b["pts"])}
                         for b in osm["buildings"]],
        "houses": flat(np.array(houses, np.float64), 2),
        "trees": flat(np.array(trees, np.float64), 2),
        "pois": [{"name": p["name"], "kind": p["kind"], "sub": p["sub"], "x": round(p["x"], 2), "z": round(p["z"], 2)}
                 for p in osm["pois"]],
        "areas": [{"kind": a["kind"], "name": a["name"], "pts": flat(a["pts"])} for a in osm["areas"]],
    }
    (out / "world.json").write_text(json.dumps(world, separators=(",", ":")), encoding="utf-8")
    print(f"Wrote {out / 'heightmap.bytes'} and world.json; height range {h_min:.1f}..{h_max:.1f}, "
          f"lake {lake_level:.1f}")
    if preview:
        write_preview(preview, h, lake, osm, houses, trees)
        print(f"Preview: {preview}")


if __name__ == "__main__":
    main()

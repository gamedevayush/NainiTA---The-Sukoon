"""Every building of world.json drawn as an outline over the satellite crops (same frame as satcompare.py):
yellow = house rectangles, cyan = polygon footprints. Also counts how much of each crop the data covers."""
import json, math, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Shots", "satcompare")
CACHE = os.path.join(OUT, "_tiles")
CENTER_LAT, CENTER_LON = 29.3905, 79.4550
M_LAT, M_LON = 110_860.0, 111_320.0 * math.cos(math.radians(CENTER_LAT))
Z = 18
W = json.load(open(os.path.join(ROOT, r"Assets\Nainital\Data\world.json"), encoding="utf-8"))

def gpx(lat, lon):
    n = 256 * 2 ** Z
    s = math.sin(math.radians(lat))
    return (lon + 180.0) / 360.0 * n, (0.5 - math.log((1 + s) / (1 - s)) / (4 * math.pi)) * n

def sat_crop(cx, cz, half, mpp):
    """Satellite resampled to a north-up local-metre square, mpp metres a pixel."""
    n = int(2 * half / mpp)
    xs = cx - half + (np.arange(n) + 0.5) * mpp
    zs = cz + half - (np.arange(n) + 0.5) * mpp
    lon = CENTER_LON + xs / M_LON
    lat = CENTER_LAT + zs / M_LAT
    N = 256 * 2 ** Z
    gx = (lon + 180.0) / 360.0 * N
    s = np.sin(np.radians(lat))
    gy = (0.5 - np.log((1 + s) / (1 - s)) / (4 * np.pi)) * N
    tx0, ty0 = int(gx.min() // 256), int(gy.min() // 256)
    tx1, ty1 = int(gx.max() // 256), int(gy.max() // 256)
    mos = Image.new("RGB", ((tx1 - tx0 + 1) * 256, (ty1 - ty0 + 1) * 256))
    for ty in range(ty0, ty1 + 1):
        for tx in range(tx0, tx1 + 1):
            f = os.path.join(CACHE, f"{Z}_{tx}_{ty}.jpg")
            if os.path.exists(f):
                mos.paste(Image.open(f).convert("RGB"), ((tx - tx0) * 256, (ty - ty0) * 256))
    M = np.asarray(mos)
    ix = np.clip((gx - tx0 * 256).astype(int), 0, M.shape[1] - 1)
    iy = np.clip((gy - ty0 * 256).astype(int), 0, M.shape[0] - 1)
    return Image.fromarray(M[iy[:, None], ix[None, :]])

h = W["houses"]
rects = [h[i:i + 9] for i in range(0, len(h) - 8, 9)]
polys = []
for b in W["osmBuildings"]:
    p = b["pts"]
    polys.append(np.array([(p[i], p[i + 1]) for i in range(0, len(p) - 1, 2)]))

def rect_pts(x, z, w, d, yaw):
    a = math.radians(yaw)
    rv = np.array([math.cos(a), -math.sin(a)]) * w / 2
    fv = np.array([math.sin(a), math.cos(a)]) * d / 2
    c = np.array([x, z])
    return [c + rv + fv, c - rv + fv, c - rv - fv, c + rv - fv]

places = {
    "mallital_flats": (-60, 40), "mallital_bazar": (-150, 180), "boat_club_capitol": (60, 40),
    "mall_road_north": (250, 0), "mall_road_mid": (520, -260), "mall_road_south": (760, -700),
    "tallital": (840, -1140), "sukhatal": (-480, 480), "snowview_church": (820, -760),
}
half, mpp = 150.0, 0.25
font = ImageFont.load_default()
summary = []
for name, (cx, cz) in places.items():
    img = sat_crop(cx, cz, half, mpp)
    d = ImageDraw.Draw(img)
    P = lambda q: ((q[0] - (cx - half)) / mpp, ((cz + half) - q[1]) / mpp)
    nr = npg = 0
    area_r = area_p = 0.0
    for r in rects:
        if abs(r[0] - cx) > half + 30 or abs(r[1] - cz) > half + 30:
            continue
        pts = [P(q) for q in rect_pts(*r[:5])]
        d.polygon(pts, outline=(255, 230, 0), width=2)
        nr += 1
        area_r += r[2] * r[3]
    for p in polys:
        c = p.mean(0)
        if abs(c[0] - cx) > half + 30 or abs(c[1] - cz) > half + 30:
            continue
        d.polygon([P(q) for q in p], outline=(0, 230, 255), width=2)
        npg += 1
        area_p += abs(0.5 * np.sum(p[:, 0] * np.roll(p[:, 1], 1) - np.roll(p[:, 0], 1) * p[:, 1]))
    d.rectangle((0, 0, 1199, 22), fill=(0, 0, 0))
    d.text((6, 5), f"{name} ({cx},{cz}) 300 m | data: {nr} rectangles (yellow), {npg} polygons (cyan) | built-up {100 * (area_r + area_p) / (4 * half * half):.0f}% of the square", fill=(255, 255, 255), font=font)
    img.save(os.path.join(OUT, f"fp_{name}.jpg"), quality=90)
    summary.append(f"{name}: {nr} rects, {npg} polys, data covers {100 * (area_r + area_p) / (4 * half * half):.0f}%")
print("\n".join(summary))

"""Esri World Imagery around the Nainital fire station pin, north-up in local metres, with world.json drawn over it:
roads (red = motor, orange = foot), house rectangles (yellow, with their index), OSM footprints (cyan), POIs (magenta).

usage: python -I satview.py [cx cz half mpp zoom out.jpg]
defaults: the owner's pin (-237.9, 104.9), 45 m half size, 0.08 m a pixel, zoom 19 -> refs/sat_pin.jpg
Tiles are cached in Tools/firestation/_tiles (Esri World Imagery; never Google tiles)."""
import json, math, os, sys, time, urllib.request
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
CACHE = os.path.join(HERE, "_tiles")
os.makedirs(CACHE, exist_ok=True)
os.makedirs(os.path.join(HERE, "refs"), exist_ok=True)
CENTER_LAT, CENTER_LON = 29.3905, 79.4550
M_LAT, M_LON = 110_860.0, 111_320.0 * math.cos(math.radians(CENTER_LAT))

a = sys.argv[1:]
cx = float(a[0]) if len(a) > 0 else -237.9
cz = float(a[1]) if len(a) > 1 else 104.9
half = float(a[2]) if len(a) > 2 else 45.0
mpp = float(a[3]) if len(a) > 3 else 0.08
Z = int(a[4]) if len(a) > 4 else 19
out = a[5] if len(a) > 5 else os.path.join(HERE, "refs", "sat_pin.jpg")


def tile(tx, ty):
    f = os.path.join(CACHE, f"{Z}_{tx}_{ty}.jpg")
    if not os.path.exists(f):
        url = f"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{Z}/{ty}/{tx}"
        req = urllib.request.Request(url, headers={"User-Agent": "nainita-satcheck"})
        for _ in range(3):
            try:
                open(f, "wb").write(urllib.request.urlopen(req, timeout=30).read())
                break
            except Exception:
                time.sleep(2)
        time.sleep(0.05)
    return Image.open(f).convert("RGB") if os.path.exists(f) else None


n = int(2 * half / mpp)
xs = cx - half + (np.arange(n) + 0.5) * mpp
zs = cz + half - (np.arange(n) + 0.5) * mpp
lon = CENTER_LON + xs / M_LON
lat = CENTER_LAT + zs / M_LAT
N = 256 * 2 ** Z
gx = (lon + 180.0) / 360.0 * N
s = np.sin(np.radians(lat))
gy = (0.5 - np.log((1 + s) / (1 - s)) / (4 * np.pi)) * N
tx0, ty0, tx1, ty1 = int(gx.min() // 256), int(gy.min() // 256), int(gx.max() // 256), int(gy.max() // 256)
mos = Image.new("RGB", ((tx1 - tx0 + 1) * 256, (ty1 - ty0 + 1) * 256))
for ty in range(ty0, ty1 + 1):
    for tx in range(tx0, tx1 + 1):
        t = tile(tx, ty)
        if t:
            mos.paste(t, ((tx - tx0) * 256, (ty - ty0) * 256))
M = np.asarray(mos)
ix = np.clip((gx - tx0 * 256).astype(int), 0, M.shape[1] - 1)
iy = np.clip((gy - ty0 * 256).astype(int), 0, M.shape[0] - 1)
img = Image.fromarray(M[iy[:, None], ix[None, :]])
raw = img.copy()

W = json.load(open(os.path.join(ROOT, r"Assets\Nainital\Data\world.json"), encoding="utf-8"))
d = ImageDraw.Draw(img)
P = lambda q: ((q[0] - (cx - half)) / mpp, ((cz + half) - q[1]) / mpp)
foot = ("footway", "pedestrian", "path", "steps", "track", "bridleway")
for r in W["roads"]:
    p = r["pts"]
    L = [(p[i], p[i + 1]) for i in range(0, len(p) - 1, 2)]
    if any(abs(x - cx) < half + 60 and abs(z - cz) < half + 60 for x, z in L):
        wpx = max(2, int(r["width"] / mpp)) if r["kind"] not in foot else 3
        d.line([P(q) for q in L], fill=(255, 60, 60) if r["kind"] not in foot else (255, 160, 0), width=2)
        # carriageway edges
        for i in range(len(L) - 1):
            (x0, z0), (x1, z1) = L[i], L[i + 1]
            dx, dz = x1 - x0, z1 - z0
            ln = math.hypot(dx, dz)
            if ln < 1e-3:
                continue
            nx_, nz_ = -dz / ln * r["width"] / 2, dx / ln * r["width"] / 2
            for sg in (-1, 1):
                d.line([P((x0 + sg * nx_, z0 + sg * nz_)), P((x1 + sg * nx_, z1 + sg * nz_))], fill=(255, 120, 120), width=1)
h = W["houses"]
for i in range(0, len(h) - 8, 9):
    x, z, w, dd, yaw = h[i:i + 5]
    if abs(x - cx) > half + 20 or abs(z - cz) > half + 20:
        continue
    ya = math.radians(yaw)
    rv = np.array([math.cos(ya), -math.sin(ya)]) * w / 2
    fv = np.array([math.sin(ya), math.cos(ya)]) * dd / 2
    c = np.array([x, z])
    pts = [c + rv + fv, c - rv + fv, c - rv - fv, c + rv - fv]
    d.polygon([P(q) for q in pts], outline=(255, 230, 0), width=2)
    px, py = P((x, z))
    d.text((px - 12, py - 6), str(i // 9), fill=(255, 255, 0))
for b in W["osmBuildings"]:
    p = b["pts"]
    poly = [(p[i], p[i + 1]) for i in range(0, len(p) - 1, 2)]
    if abs(poly[0][0] - cx) < half + 30 and abs(poly[0][1] - cz) < half + 30:
        d.polygon([P(q) for q in poly], outline=(0, 230, 255), width=2)
for pt in W["pois"]:
    if abs(pt["x"] - cx) < half and abs(pt["z"] - cz) < half:
        x, y = P((pt["x"], pt["z"]))
        d.ellipse((x - 5, y - 5, x + 5, y + 5), fill=(255, 0, 255))
        d.text((x + 7, y - 6), pt["name"], fill=(255, 255, 0))
# the pin and a 10 m grid
px, py = P((-237.9, 104.9))
d.ellipse((px - 6, py - 6, px + 6, py + 6), outline=(0, 255, 0), width=3)
for g in range(int(cx - half) // 10 * 10, int(cx + half) + 1, 10):
    x, _ = P((g, cz))
    d.line([(x, 0), (x, 8)], fill=(255, 255, 255))
    d.text((x + 2, 10), str(g), fill=(255, 255, 255))
for g in range(int(cz - half) // 10 * 10, int(cz + half) + 1, 10):
    _, y = P((cx, g))
    d.line([(0, y), (8, y)], fill=(255, 255, 255))
    d.text((10, y + 2), str(g), fill=(255, 255, 255))
img.save(out, quality=92)
raw.save(out.replace(".jpg", "_raw.jpg"), quality=92)
print("saved", out, img.size)

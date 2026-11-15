"""Lay the game's top view (Shots/topview, local metres, north up) next to and over Esri World Imagery of the same
ground, resampled into the same frame. Local metres <-> lat/lon as in Tools/area.py."""
import io, math, os, sys, time, urllib.request
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
TV = os.path.join(ROOT, "Shots", "topview")
OUT = os.path.join(ROOT, "Shots", "satcompare")
CACHE = os.path.join(OUT, "_tiles")
os.makedirs(CACHE, exist_ok=True)

CENTER_LAT, CENTER_LON = 29.3905, 79.4550
M_LAT = 110_860.0
M_LON = 111_320.0 * math.cos(math.radians(CENTER_LAT))
Z = 18

x0, z0, x1, z1, mpp, tile, nx, nz = open(os.path.join(TV, "topview.txt")).read().split()
x0, z0, mpp, tile, nx, nz = float(x0), float(z0), float(mpp), float(tile), int(nx), int(nz)
tp = int(round(tile / mpp))
W, H = nx * tp, nz * tp
X1, Z1 = x0 + nx * tile, z0 + nz * tile

# ---- our top view, stitched (row 0 = north)
ours = Image.new("RGB", (W, H))
for iz in range(nz):
    for ix in range(nx):
        t = Image.open(os.path.join(TV, f"tile_{ix}_{iz}.png")).convert("RGB")
        ours.paste(t, (ix * tp, (nz - 1 - iz) * tp))

# ---- satellite mosaic at zoom Z covering the frame
def ll(x, z):
    return CENTER_LAT + z / M_LAT, CENTER_LON + x / M_LON

def gpx(lat, lon):   # global web-mercator pixel at zoom Z
    n = 256 * 2 ** Z
    gx = (lon + 180.0) / 360.0 * n
    s = math.sin(math.radians(lat))
    gy = (0.5 - math.log((1 + s) / (1 - s)) / (4 * math.pi)) * n
    return gx, gy

latN, lonW = ll(x0, Z1)
latS, lonE = ll(X1, z0)
gx0, gy0 = gpx(latN, lonW)
gx1, gy1 = gpx(latS, lonE)
tx0, ty0, tx1, ty1 = int(gx0 // 256), int(gy0 // 256), int(gx1 // 256), int(gy1 // 256)
mos = Image.new("RGB", ((tx1 - tx0 + 1) * 256, (ty1 - ty0 + 1) * 256))
need = (tx1 - tx0 + 1) * (ty1 - ty0 + 1)
got = 0
for ty in range(ty0, ty1 + 1):
    for tx in range(tx0, tx1 + 1):
        f = os.path.join(CACHE, f"{Z}_{tx}_{ty}.jpg")
        if not os.path.exists(f):
            url = f"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{Z}/{ty}/{tx}"
            req = urllib.request.Request(url, headers={"User-Agent": "nainita-topview-compare"})
            for attempt in range(3):
                try:
                    data = urllib.request.urlopen(req, timeout=30).read()
                    open(f, "wb").write(data)
                    break
                except Exception as e:
                    time.sleep(2)
            time.sleep(0.05)
        if os.path.exists(f):
            mos.paste(Image.open(f).convert("RGB"), ((tx - tx0) * 256, (ty - ty0) * 256))
            got += 1
print(f"satellite tiles {got}/{need} at zoom {Z}")

# resample the mosaic into the local frame
xs = x0 + (np.arange(W) + 0.5) * mpp
zs = Z1 - (np.arange(H) + 0.5) * mpp
lon = CENTER_LON + xs / M_LON
lat = CENTER_LAT + zs / M_LAT
n = 256 * 2 ** Z
gx = (lon + 180.0) / 360.0 * n - tx0 * 256
s = np.sin(np.radians(lat))
gy = (0.5 - np.log((1 + s) / (1 - s)) / (4 * np.pi)) * n - ty0 * 256
M = np.asarray(mos)
ix = np.clip(gx.round().astype(int), 0, M.shape[1] - 1)
iy = np.clip(gy.round().astype(int), 0, M.shape[0] - 1)
sat = Image.fromarray(M[iy[:, None], ix[None, :]])

# ---- outputs
def px(x, z):
    return int((x - x0) / mpp), int((Z1 - z) / mpp)

blend = Image.blend(sat, ours, 0.5)
small = lambda im: im.resize((W // 4, H // 4), Image.LANCZOS)
pair = Image.new("RGB", (W // 2, H // 4))
pair.paste(small(sat), (0, 0)); pair.paste(small(ours), (W // 4, 0))
pair.save(os.path.join(OUT, "overview_sat_vs_game.jpg"), quality=88)
small(blend).save(os.path.join(OUT, "overview_blend.jpg"), quality=88)

# crops of the main places: satellite | game | blend, 300 m square at full resolution scaled to 600 px
places = {
    "mallital_flats": (-60, 40), "mallital_bazar": (-150, 180), "boat_club_capitol": (60, 40),
    "mall_road_north": (250, 0), "mall_road_mid": (520, -260), "mall_road_south": (760, -700),
    "tallital": (840, -1140), "sukhatal": (-480, 480), "snowview_church": (820, -760),
}
half = 150
font = ImageFont.load_default()
for name, (cx, cz) in places.items():
    a, b = px(cx - half, cz + half), px(cx + half, cz - half)
    box = (a[0], a[1], b[0], b[1])
    row = Image.new("RGB", (1800, 630), (20, 20, 20))
    for k, (label, im) in enumerate((("satellite (Esri)", sat), ("game", ours), ("blend", blend))):
        c = im.crop(box).resize((600, 600), Image.LANCZOS)
        row.paste(c, (k * 600, 30))
        ImageDraw.Draw(row).text((k * 600 + 8, 8), f"{label}  {name}  ({cx}, {cz})  300 m", fill=(255, 220, 80), font=font)
    row.save(os.path.join(OUT, f"cmp_{name}.jpg"), quality=90)
print("done ->", OUT)

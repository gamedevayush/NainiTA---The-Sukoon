"""The hills beyond the 4 km map and the snow range on the horizon, as skylines for the far-ridges dome.

Usage:  python -I make_far_ridges.py <raw_dir> <out_json>

Downloads AWS Open Data Terrain Tiles (terrarium PNG, mostly SRTM; public) at three zooms into <raw_dir>/dem_far. Then for
every 0.1 deg of azimuth and each distance band it finds the highest elevation angle of the ground as seen from the map
centre, with the Earth's curvature and refraction, from two eye heights (lake level and the peaks). It records the ground
height and distance there, so the dome can put snow above the snow line and haze by distance.
WorldBuilder.FarRidges.cs builds the dome from the JSON.
"""
import json
import math
import pathlib
import sys
import time

import numpy as np
import requests
from PIL import Image

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import area  # noqa: E402

UA = {"User-Agent": "NainitalGame-dev/0.1 (hobby game, low volume)"}
R_EARTH = 6371000.0
K_REFRACTION = 0.13
AZ_STEP = 0.1                      # degrees
BANDS = [(4500, 7000), (7000, 12000), (12000, 20000), (20000, 35000), (35000, 60000), (60000, 170000)]   # from the map's centre; the square reaches 4.24 km
SNOW_LINE = 4600.0                 # m: the Kumaon peaks are white above this through most of the year
INNER = area.HALF - 50.0           # the map's own terrain covers this square


def deg2tile(lat, lon, z):
    n = 2 ** z
    return (lon + 180.0) / 360.0 * n, (1.0 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2.0 * n


class Mosaic:
    """Terrarium tiles at one zoom covering a box round the map centre, sampled bilinearly by lat/lon."""

    def __init__(self, raw, z, radius_m):
        self.z = z
        dlat = radius_m / area.M_PER_DEG_LAT
        dlon = radius_m / area.M_PER_DEG_LON
        x0, y1 = deg2tile(area.CENTER_LAT - dlat, area.CENTER_LON - dlon, z)
        x1, y0 = deg2tile(area.CENTER_LAT + dlat, area.CENTER_LON + dlon, z)
        self.tx0, self.ty0 = int(x0), int(y0)
        tx1, ty1 = int(x1), int(y1)
        out = raw / "dem_far"
        out.mkdir(parents=True, exist_ok=True)
        nx, ny = tx1 - self.tx0 + 1, ty1 - self.ty0 + 1
        self.h = np.zeros((ny * 256, nx * 256), np.float32)
        print(f"  z{z}: {nx * ny} tiles")
        for ty in range(self.ty0, ty1 + 1):
            for tx in range(self.tx0, tx1 + 1):
                path = out / f"{z}_{tx}_{ty}.png"
                if not path.exists():
                    urls = [f"https://elevation-tiles-prod.s3.amazonaws.com/terrarium/{z}/{tx}/{ty}.png",
                            f"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{tx}/{ty}.png"]
                    for attempt in range(6):   # this network drops connections now and then
                        try:
                            r = requests.get(urls[attempt % 2], headers=UA, timeout=30)
                            r.raise_for_status()
                            break
                        except requests.RequestException as e:
                            print(f"    retry {z}/{tx}/{ty}: {type(e).__name__}")
                            time.sleep(2.0)
                    else:
                        raise RuntimeError(f"could not fetch tile {z}/{tx}/{ty}")
                    path.write_bytes(r.content)
                    time.sleep(0.2)
                a = np.asarray(Image.open(path).convert("RGB"), np.float32)
                e = a[..., 0] * 256.0 + a[..., 1] + a[..., 2] / 256.0 - 32768.0
                oy, ox = (ty - self.ty0) * 256, (tx - self.tx0) * 256
                self.h[oy:oy + 256, ox:ox + 256] = e

    def sample(self, lat, lon):
        x, y = deg2tile(0.0, 0.0, self.z)   # placeholder to keep signature simple
        n = 2 ** self.z
        px = ((lon + 180.0) / 360.0 * n - self.tx0) * 256 - 0.5
        py = ((1.0 - np.arcsinh(np.tan(np.radians(lat))) / np.pi) / 2.0 * n - self.ty0) * 256 - 0.5
        H, W = self.h.shape
        px = np.clip(px, 0, W - 1.001)
        py = np.clip(py, 0, H - 1.001)
        i, j = px.astype(int), py.astype(int)
        fx, fy = px - i, py - j
        h = self.h
        return (h[j, i] * (1 - fx) * (1 - fy) + h[j, i + 1] * fx * (1 - fy) + h[j + 1, i] * (1 - fx) * fy + h[j + 1, i + 1] * fx * fy)


def main():
    raw, out = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    world = json.loads((pathlib.Path(sys.argv[3]) if len(sys.argv) > 3 else out.parent / "world.json").read_text(encoding="utf-8"))
    lake = float(world["meta"]["lakeLevel"])
    eyes = [lake + 6.0, lake + 706.0]          # standing by the lake; on the peaks round it
    print("Far ridges: fetching terrain tiles ...")
    mos = {11: Mosaic(raw, 11, 21000), 9: Mosaic(raw, 9, 62000), 8: Mosaic(raw, 8, 172000)}
    az = np.radians(np.arange(0.0, 360.0, AZ_STEP))
    bands = []
    for d0, d1 in BANDS:
        z = 11 if d1 <= 20000 else (9 if d1 <= 60000 else 8)
        step = max(30.0, d0 * 0.006)
        d = np.arange(d0, d1 + 1, step)
        D, A = np.meshgrid(d, az)                       # [azimuth, distance]
        x, zz = area.MAP_CX + D * np.sin(A), area.MAP_CZ + D * np.cos(A)   # seen from the map's centre
        lat = area.CENTER_LAT + zz / area.M_PER_DEG_LAT
        lon = area.CENTER_LON + x / (area.M_PER_DEG_LON * np.cos(np.radians(lat)) / math.cos(math.radians(area.CENTER_LAT)))
        h = mos[z].sample(lat, lon)
        h = np.where((np.abs(x - area.MAP_CX) < INNER) & (np.abs(zz - area.MAP_CZ) < INNER), -1e4, h)   # the map's own ground stands there
        drop = D * D / (2.0 * R_EARTH) * (1.0 - K_REFRACTION)
        band = {"d0": d0, "d1": d1}
        for name, eye in (("lo", eyes[0]), ("hi", eyes[1])):
            ang = np.degrees(np.arctan2(h - drop - eye, D))
            k = np.argmax(ang, axis=1)
            rows = np.arange(len(az))
            band[name] = np.round(ang[rows, k], 3).tolist()
            if name == "lo":
                band["elev"] = np.round(h[rows, k], 1).tolist()
                band["dist"] = np.round(D[rows, k], 0).tolist()
        bands.append(band)
        a = np.array(band["lo"])
        print(f"  band {d0 / 1000:5.1f}-{d1 / 1000:5.1f} km: skyline {a.min():6.2f} .. {a.max():5.2f} deg, highest ground {max(band['elev']):6.0f} m")
    out.write_text(json.dumps({"azStep": AZ_STEP, "eyeLo": eyes[0], "eyeHi": eyes[1], "snowLine": SNOW_LINE, "bands": bands},
                              separators=(",", ":")), encoding="utf-8")
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()

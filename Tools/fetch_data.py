"""Download raw real-world data for Nainital.

Usage:  python -I fetch_data.py <raw_output_dir>

Sources (both free/open):
  * Elevation: AWS Open Data "Terrain Tiles" (terrarium PNG encoding, mostly SRTM 30 m here).
  * Map features: OpenStreetMap via the Overpass API (ODbL, (c) OpenStreetMap contributors).
"""
import math
import pathlib
import sys
import time

import requests

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import area  # noqa: E402

DEM_ZOOM = 15
UA = {"User-Agent": "NainitalGame-dev/0.1 (hobby game, low volume)"}
OVERPASS_ENDPOINTS = [
    "https://overpass-api.de/api/interpreter",
    "https://overpass.kumi.systems/api/interpreter",
    "https://overpass.private.coffee/api/interpreter",
    "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    "https://overpass-api.de/api/interpreter",   # once more, after the others
]


def deg2tile(lat, lon, z):
    n = 2 ** z
    x = (lon + 180.0) / 360.0 * n
    y = (1.0 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2.0 * n
    return x, y


def fetch_dem(out: pathlib.Path):
    dem_dir = out / "dem"
    dem_dir.mkdir(parents=True, exist_ok=True)
    x0, y1 = deg2tile(area.SOUTH, area.WEST, DEM_ZOOM)
    x1, y0 = deg2tile(area.NORTH, area.EAST, DEM_ZOOM)
    tiles = [(tx, ty) for ty in range(int(y0), int(y1) + 1) for tx in range(int(x0), int(x1) + 1)]
    print(f"DEM: {len(tiles)} tiles at z{DEM_ZOOM}")
    for tx, ty in tiles:
        path = dem_dir / f"{DEM_ZOOM}_{tx}_{ty}.png"
        if path.exists() and path.stat().st_size > 0:
            continue
        url = f"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{DEM_ZOOM}/{tx}/{ty}.png"
        r = requests.get(url, headers=UA, timeout=60)
        r.raise_for_status()
        if not r.content.startswith(b"\x89PNG"):
            raise RuntimeError(f"Not a PNG: {url}")
        path.write_bytes(r.content)
        time.sleep(0.05)
    print("DEM done")


def overpass_query():
    b = f"({area.SOUTH:.6f},{area.WEST:.6f},{area.NORTH:.6f},{area.EAST:.6f})"
    return f"""
[out:json][timeout:300];
(
  way["natural"="water"]{b};
  relation["natural"="water"]{b};
  way["highway"]{b};
  way["building"]{b};
  relation["building"]{b};
  node["natural"="peak"]{b};
  node["amenity"]{b};
  way["amenity"]{b};
  node["tourism"]{b};
  way["tourism"]{b};
  node["historic"]{b};
  way["historic"]{b};
  way["leisure"]{b};
  way["landuse"]{b};
  way["natural"="wood"]{b};
  way["natural"="scrub"]{b};
  way["natural"="grassland"]{b};
  node["shop"]{b};
);
out geom;
"""


def fetch_osm(out: pathlib.Path):
    path = out / "osm.json"
    if path.exists() and path.stat().st_size > 1000:
        print("OSM already downloaded")
        return
    q = overpass_query()
    last_err = None
    for ep in OVERPASS_ENDPOINTS:
        try:
            print(f"Overpass: {ep}")
            r = requests.post(ep, data={"data": q}, headers=UA, timeout=240)
            r.raise_for_status()
            if not r.text.lstrip().startswith("{"):
                raise RuntimeError("Overpass did not return JSON")
            path.write_text(r.text, encoding="utf-8")
            print(f"OSM done ({len(r.text) // 1024} KB)")
            return
        except Exception as e:  # try next mirror
            last_err = e
            print(f"  failed: {e}")
    raise RuntimeError(f"All Overpass endpoints failed: {last_err}")


if __name__ == "__main__":
    out_dir = pathlib.Path(sys.argv[1])
    out_dir.mkdir(parents=True, exist_ok=True)
    print(f"Area: S{area.SOUTH:.5f} W{area.WEST:.5f} N{area.NORTH:.5f} E{area.EAST:.5f}")
    fetch_dem(out_dir)
    fetch_osm(out_dir)

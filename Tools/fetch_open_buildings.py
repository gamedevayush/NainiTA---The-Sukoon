"""Download Google Open Buildings 2.5D Temporal for the Nainital map square and give every footprint a height.

Run:  Tools/.venv/Scripts/python.exe -I Tools/fetch_open_buildings.py RawData

Needs a finished `ee.Authenticate` and the Earth Engine project below (non-commercial registration).
Writes to RawData/open_buildings:
  raster_<date>.npz       building_presence, building_height, building_fractional_count on a ~4 m lat/lon grid
  building_heights.json   {overture id: {"h": metres, "conf": presence, "px": pixels used}}
"""
import datetime
import glob
import json
import os
import re
import sys

import ee
import numpy as np
from PIL import Image, ImageDraw

PROJECT = "nainital-game"
DATASET = "GOOGLE/Research/open-buildings-temporal/v1"
WEST, EAST, SOUTH, NORTH = 79.4325, 79.4775, 29.3710, 29.4100
DX, DY = 4.127e-5, 3.593e-5  # about 4 m at this latitude
PRESENCE_MIN = 0.34          # below this a pixel is treated as "no building"
BANDS = ["building_presence", "building_height", "building_fractional_count"]


def download(raw):
    ee.Initialize(project=PROJECT)
    roi = ee.Geometry.Rectangle([WEST, SOUTH, EAST, NORTH])
    col = ee.ImageCollection(DATASET).filterBounds(roi)
    print("tiles over the map:", col.size().getInfo())
    stamps = sorted(set(col.aggregate_array("system:time_start").getInfo()))
    if not stamps:
        sys.exit("no Open Buildings data over this area")
    days = [datetime.datetime.fromtimestamp(s / 1000, datetime.timezone.utc).strftime("%Y-%m-%d") for s in stamps]
    print("epochs:", days)
    latest = stamps[-1]
    img = col.filter(ee.Filter.eq("system:time_start", latest)).mosaic().select(BANDS).unmask(0).toFloat()
    width = int(round((EAST - WEST) / DX))
    height = int(round((NORTH - SOUTH) / DY))
    grid = {
        "dimensions": {"width": width, "height": height},
        "affineTransform": {"scaleX": DX, "shearX": 0, "translateX": WEST, "shearY": 0, "scaleY": -DY, "translateY": NORTH},
        "crsCode": "EPSG:4326",
    }
    arr = ee.data.computePixels({"expression": img, "fileFormat": "NUMPY_NDARRAY", "grid": grid})
    out = {b: np.asarray(arr[b], dtype=np.float32) for b in BANDS}
    path = os.path.join(raw, f"raster_{days[-1]}.npz")
    np.savez_compressed(path, **out)
    print("saved", path, out[BANDS[0]].shape)
    return out


def rings(wkt):
    """Outer ring of each polygon in a POLYGON / MULTIPOLYGON WKT."""
    found = []
    for part in re.findall(r"\(\s*([-0-9. ,]+?)\s*\)", wkt):
        pts = [tuple(map(float, p.split())) for p in part.split(",")]
        if len(pts) >= 3:
            found.append(pts)
    return found[:1] if wkt.startswith("POLYGON") else found


def sample(raster, buildings):
    presence, height = raster["building_presence"], raster["building_height"]
    rows, cols = presence.shape
    result, footprint = {}, np.zeros(presence.shape, dtype=bool)
    for b in buildings:
        heights, confs = [], []
        for ring in rings(b["wkt"]):
            xy = [((lon - WEST) / DX, (NORTH - lat) / DY) for lon, lat in ring]
            x0 = max(int(min(p[0] for p in xy)) - 1, 0)
            y0 = max(int(min(p[1] for p in xy)) - 1, 0)
            x1 = min(int(max(p[0] for p in xy)) + 2, cols)
            y1 = min(int(max(p[1] for p in xy)) + 2, rows)
            if x1 <= x0 or y1 <= y0:
                continue
            mask_img = Image.new("1", (x1 - x0, y1 - y0))
            ImageDraw.Draw(mask_img).polygon([(x - x0, y - y0) for x, y in xy], fill=1, outline=1)
            mask = np.asarray(mask_img, dtype=bool)
            footprint[y0:y1, x0:x1] |= mask
            p, h = presence[y0:y1, x0:x1][mask], height[y0:y1, x0:x1][mask]
            good = (p >= PRESENCE_MIN) & (h > 0)
            heights.extend(h[good].tolist())
            confs.extend(p[good].tolist())
        if heights:
            # upper value, not the median: on small roofs the edge pixels blend down towards the ground
            result[b["id"]] = {"h": round(float(np.percentile(heights, 80)), 2), "conf": round(float(np.median(confs)), 3), "px": len(heights)}
    return result, footprint


def report(raster, result, footprint, total):
    presence = raster["building_presence"]
    built = presence >= PRESENCE_MIN
    h = np.array([v["h"] for v in result.values()])
    print(f"footprints with a height: {len(result)} / {total}")
    print("height m  p10/p50/p90/max:", *[round(float(np.percentile(h, q)), 1) for q in (10, 50, 90, 100)])
    floors = np.clip(np.round(h / 3.0).astype(int), 1, 6)
    print("floors at 3 m (1..6+):", [int((floors == f).sum()) for f in range(1, 7)])
    area = (DX * 111320 * np.cos(np.radians(29.39))) * (DY * 110900)
    print(f"built-up by raster: {built.sum() * area / 1e6:.3f} km2 | covered by footprints: {(built & footprint).sum() * area / 1e6:.3f} km2 | "
          f"built-up with no footprint: {(built & ~footprint).sum() * area / 1e6:.3f} km2")


def main():
    raw = os.path.join(sys.argv[1], "open_buildings")
    os.makedirs(raw, exist_ok=True)
    saved = sorted(glob.glob(os.path.join(raw, "raster_*.npz")))
    raster = dict(np.load(saved[-1])) if saved else download(raw)
    with open(os.path.join(sys.argv[1], "overture", "buildings.json"), encoding="utf-8") as f:
        buildings = json.load(f)
    result, footprint = sample(raster, buildings)
    with open(os.path.join(raw, "building_heights.json"), "w", encoding="utf-8") as f:
        json.dump(result, f)
    report(raster, result, footprint, len(buildings))


if __name__ == "__main__":
    main()

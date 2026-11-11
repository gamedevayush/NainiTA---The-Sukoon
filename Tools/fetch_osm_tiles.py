"""OSM for the whole map, fetched in small tiles and merged (the Overpass servers time out on one big query).

Usage:  python -I fetch_osm_tiles.py <raw_dir> [base_osm.json]

Asks Overpass for each ~2 km tile of the map with the same query as fetch_data.py, retrying across mirrors, and merges
the elements by type and id into <raw_dir>/osm.json. An earlier osm.json can seed the merge, so only missing tiles need
to come down. (c) OpenStreetMap contributors, ODbL.
"""
import json
import pathlib
import sys
import time

import requests

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import area  # noqa: E402
import fetch_data  # noqa: E402

TILE = 2000.0


def query(s, w, n, e):
    q = fetch_data.overpass_query()
    full = f"({area.SOUTH:.6f},{area.WEST:.6f},{area.NORTH:.6f},{area.EAST:.6f})"
    return q.replace(full, f"({s:.6f},{w:.6f},{n:.6f},{e:.6f})")


def main():
    raw = pathlib.Path(sys.argv[1])
    merged = {}
    if len(sys.argv) > 2 and pathlib.Path(sys.argv[2]).exists():
        for el in json.loads(pathlib.Path(sys.argv[2]).read_text(encoding="utf-8"))["elements"]:
            merged[(el["type"], el["id"])] = el
        print(f"seeded with {len(merged)} elements")
    tiles_dir = raw / "osm_tiles"
    tiles_dir.mkdir(parents=True, exist_ok=True)
    x0, z0, size = area.X0, area.Z0, area.SIZE_M
    nt = int(round(size / TILE))
    for i in range(nt):
        for j in range(nt):
            path = tiles_dir / f"tile_{i}_{j}.json"
            if not path.exists():
                xa, xb = x0 + i * TILE, x0 + (i + 1) * TILE
                za, zb = z0 + j * TILE, z0 + (j + 1) * TILE
                s, n = area.CENTER_LAT + za / area.M_PER_DEG_LAT, area.CENTER_LAT + zb / area.M_PER_DEG_LAT
                w, e = area.CENTER_LON + xa / area.M_PER_DEG_LON, area.CENTER_LON + xb / area.M_PER_DEG_LON
                q = query(s, w, n, e)
                ok = False
                for attempt in range(8):
                    ep = fetch_data.OVERPASS_ENDPOINTS[attempt % len(fetch_data.OVERPASS_ENDPOINTS)]
                    try:
                        r = requests.post(ep, data={"data": q}, headers=fetch_data.UA, timeout=200)
                        r.raise_for_status()
                        if not r.text.lstrip().startswith("{"):
                            raise RuntimeError("not JSON")
                        path.write_text(r.text, encoding="utf-8")
                        ok = True
                        break
                    except Exception as ex:
                        print(f"  tile {i},{j} via {ep.split('/')[2]}: {type(ex).__name__} {str(ex)[:60]}")
                        time.sleep(4 + attempt * 3)
                if not ok:
                    print(f"tile {i},{j}: gave up")
                    continue
                time.sleep(2)
            els = json.loads(path.read_text(encoding="utf-8"))["elements"]
            for el in els:
                merged[(el["type"], el["id"])] = el
            print(f"tile {i},{j}: {len(els)} elements, {len(merged)} merged")
    (raw / "osm.json").write_text(json.dumps({"version": 0.6, "elements": list(merged.values())}), encoding="utf-8")
    print(f"Wrote {raw / 'osm.json'} with {len(merged)} elements")


if __name__ == "__main__":
    main()

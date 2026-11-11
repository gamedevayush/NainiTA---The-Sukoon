"""Download real building footprints, places and roads for the Nainital map from Overture Maps.

Usage:  .venv/Scripts/python.exe -I fetch_overture.py <raw_output_dir> [release]

Overture (https://overturemaps.org, ODbL / CDLA) merges OpenStreetMap with Microsoft and Google
machine-learned building footprints, so it has nearly every roof in town, not only the ~60 that
are mapped in OSM. Only the rows inside the map square (area.py) are read: DuckDB pushes the bbox
filter down to the GeoParquet row groups on S3, so the download stays small.

Writes <raw>/overture/{buildings,places,segments}.json (lists of dicts, WKT geometry in WGS84).
"""
import json
import pathlib
import sys
import time

import duckdb

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import area  # noqa: E402

RELEASE = sys.argv[2] if len(sys.argv) > 2 else "2026-10-21.0"
BASE = f"s3://overturemaps-us-west-2/release/{RELEASE}"
PAD = 0.002   # ~200 m margin so features crossing the edge are complete


def main():
    out = pathlib.Path(sys.argv[1]) / "overture"
    out.mkdir(parents=True, exist_ok=True)
    con = duckdb.connect()
    for ext in ("spatial", "httpfs"):
        con.execute(f"INSTALL {ext}; LOAD {ext};")
    con.execute("SET s3_region='us-west-2'; SET s3_url_style='path';")
    w, e, s, n = area.WEST - PAD, area.EAST + PAD, area.SOUTH - PAD, area.NORTH + PAD
    where = f"bbox.xmin > {w} AND bbox.xmax < {e} AND bbox.ymin > {s} AND bbox.ymax < {n}"
    queries = {
        "buildings": f"""
            SELECT id, names.primary AS name, class, subtype, height, num_floors, roof_shape, roof_color,
                   facade_color, facade_material, roof_material, is_underground,
                   sources[1].dataset AS source, ST_AsText(geometry) AS wkt
            FROM read_parquet('{BASE}/theme=buildings/type=building/*', hive_partitioning=1)
            WHERE {where}""",
        "places": f"""
            SELECT id, names.primary AS name, basic_category AS category, confidence,
                   ST_AsText(geometry) AS wkt
            FROM read_parquet('{BASE}/theme=places/type=place/*', hive_partitioning=1)
            WHERE {where}""",
        "segments": f"""
            SELECT id, names.primary AS name, subtype, class, ST_AsText(geometry) AS wkt
            FROM read_parquet('{BASE}/theme=transportation/type=segment/*', hive_partitioning=1)
            WHERE {where}""",
    }
    for name, q in queries.items():
        if (out / f"{name}.json").exists() and "--force" not in sys.argv:
            print(f"{name}: already downloaded")
            continue
        t0 = time.time()
        df = con.execute(q).fetchdf()
        rows = json.loads(df.to_json(orient="records"))
        (out / f"{name}.json").write_text(json.dumps(rows))
        print(f"{name}: {len(rows)} rows in {time.time() - t0:.0f}s")
        if name == "buildings":
            src = df["source"].value_counts().to_dict()
            print("  by source:", src)
            print("  with height:", int(df["height"].notna().sum()), "with floors:", int(df["num_floors"].notna().sum()))


if __name__ == "__main__":
    main()

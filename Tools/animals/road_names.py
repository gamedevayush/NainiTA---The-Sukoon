"""List the RoadNetwork's roads / walks (name, width, points, centre) - to choose where the animals live (22 Nov 2026).
  python -I Tools/animals/road_names.py [filter]
"""
import os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
src = open(os.path.join(ROOT, "Assets", "Nainital", "Generated", "RoadNetwork.asset"), encoding="utf8").read().split("\n")
flt = sys.argv[1].lower() if len(sys.argv) > 1 else None
section, cur, items = None, None, []
pt = re.compile(r"\{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}")
for line in src:
    if line.startswith("  roads:"):
        section = "road"; continue
    if line.startswith("  walks:"):
        section = "walk"; continue
    if section and line.startswith("  - name:"):
        cur = {"kind": section, "name": line[9:].strip().strip("'\""), "pts": [], "width": 0}
        items.append(cur); continue
    if cur and line.startswith("    width:"):
        cur["width"] = float(line.split(":")[1]); continue
    m = pt.search(line)
    if cur and m and line.startswith("    - "):
        cur["pts"].append(tuple(float(v) for v in m.groups()))
for it in items:
    if flt and flt not in it["name"].lower():
        continue
    p = it["pts"]
    if not p:
        continue
    cx = sum(q[0] for q in p) / len(p); cz = sum(q[2] for q in p) / len(p)
    length = sum(((p[i][0] - p[i - 1][0]) ** 2 + (p[i][2] - p[i - 1][2]) ** 2) ** 0.5 for i in range(1, len(p)))
    print("%-4s %-40s w %4.1f  n %4d  len %6.0f  centre (%7.0f, %7.0f)  y %5.0f" % (it["kind"], it["name"][:40], it["width"], len(p), length, cx, cz, sum(q[1] for q in p) / len(p)))
print(len(items), "paths")

import sys, os
import numpy as np, json, math
from PIL import Image, ImageDraw, ImageFont
exec(open(os.path.join(os.path.dirname(__file__), "footprints_on_sat.py")).read().split("h = W[\"houses\"]")[0])
cx, cz, half, mpp = float(sys.argv[1]), float(sys.argv[2]), float(sys.argv[3]), 0.15
img = sat_crop(cx, cz, half, mpp)
d = ImageDraw.Draw(img)
P = lambda q: ((q[0] - (cx - half)) / mpp, ((cz + half) - q[1]) / mpp)
for ar in W["areas"]:
    p = ar["pts"]; poly = [(p[i], p[i+1]) for i in range(0, len(p)-1, 2)]
    if ar["kind"] in ("park", "stadium", "playground"):
        d.polygon([P(q) for q in poly], outline=(0, 255, 0), width=3)
for r in W["roads"]:
    p = r["pts"]; L = [(p[i], p[i+1]) for i in range(0, len(p)-1, 2)]
    if any(abs(x-cx) < half+40 and abs(z-cz) < half+40 for x, z in L):
        d.line([P(q) for q in L], fill=(255, 60, 60) if r["kind"] not in ("footway","pedestrian","path","steps") else (255, 160, 0), width=3)
for b in W["osmBuildings"]:
    p = b["pts"]; poly = [(p[i], p[i+1]) for i in range(0, len(p)-1, 2)]
    if abs(poly[0][0]-cx) < half+30 and abs(poly[0][1]-cz) < half+30:
        d.polygon([P(q) for q in poly], outline=(0, 230, 255), width=2)
for pt in W["pois"]:
    if abs(pt["x"]-cx) < half and abs(pt["z"]-cz) < half:
        x, y = P((pt["x"], pt["z"])); d.ellipse((x-5, y-5, x+5, y+5), fill=(255, 0, 255)); d.text((x+7, y-6), pt["name"], fill=(255, 255, 0))
for gx in range(int(cx-half)//10*10, int(cx+half), 10):
    x, _ = P((gx, cz)); d.line([(x, 0), (x, 6)], fill=(255,255,255)); 
    if gx % 50 == 0: d.text((x+2, 8), str(gx), fill=(255,255,255))
for gz in range(int(cz-half)//10*10, int(cz+half), 10):
    _, y = P((cx, gz)); d.line([(0, y), (6, y)], fill=(255,255,255))
    if gz % 50 == 0: d.text((8, y+2), str(gz), fill=(255,255,255))
img.save(sys.argv[4], quality=92)

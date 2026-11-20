"""Per seat of every vehicle: the cushion under the hips (from the ProfileZ lines in the Editor.log), its z extent, the
floor ahead of it, and where the hips / feet are now against them. Vehicle space, metres. Writes geom.json for the fixer."""
import os, re, json, glob, sys
L = os.path.join(os.environ["LOCALAPPDATA"], "Unity", "Editor", "Editor.log")
D = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "VehicleSetup"))
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "geom.json")
POSE = {0: "Drive", 1: "Sit", 2: "Ride", 3: "Pillion", 4: "Side"}
OVER = {0: 0.51, 1: 0.51, 2: 0.40, 3: 0.40, 4: 0.51}

# ---- the profiles: model -> x -> [(z, [(y, back, part)])]
prof = {}
cur = None; xl = None
for ln in open(L, encoding="utf-8", errors="replace").read().splitlines():
    m = re.match(r"\[Nainital\] profileZ (\w+):", ln)
    if m: cur = m.group(1); prof[cur] = {}; continue
    if not cur: continue
    if not ln.startswith("  "): cur = None; continue
    m = re.match(r"\s+x (-?[\d.]+):", ln)
    if m: xl = float(m.group(1)); prof[cur][xl] = []; continue
    m = re.match(r"\s+z (-?[\d.]+): ?(.*)", ln)
    if m:
        hs = []
        for tok in m.group(2).split():
            mm = re.match(r"(-?[\d.]+)(\*?)(?:\((.*)\))?", tok)
            if not mm: continue
            hs.append((float(mm.group(1)), mm.group(2) == "*", mm.group(3) or ""))
        prof[cur][xl].append((float(m.group(1)), hs))

def line(model, x):
    xs = prof[model]
    k = min(xs, key=lambda q: abs(q - x))
    return xs[k], k

def at(rows, z):
    return min(rows, key=lambda r: abs(r[0] - z))

def up_surfaces(hs, lo, hi):
    return [y for (y, back, part) in hs if not back and lo <= y <= hi and not part.startswith("Wheel")]

geom = {}
for f in sorted(glob.glob(os.path.join(D, "*.json"))):
    j = json.load(open(f, encoding="utf-8"))
    model = j["model"]
    if model not in prof: print("no profile", model); continue
    two = any(k in model for k in ("bike", "scooty"))
    print(f"\n== {model}")
    geom[model] = []
    for i, s in enumerate(j["seats"]):
        p = s["pose"]; r = s["pos"]; over = OVER.get(p, 0.51)
        hips = r["y"] + over
        rows, lx = line(model, r["x"])
        face = -1 if abs(s["euler"]["y"] - 180) < 30 else 1
        # cushion: the highest up-facing surface from hips-0.6 to hips+0.25 at the hips' z
        _, hs = at(rows, r["z"])
        cand = up_surfaces(hs, hips - 0.6, hips + 0.25)
        cushion = max(cand) if cand else None
        # its z run: neighbouring rows with a surface within 3 cm of it
        zr = None
        if cushion is not None:
            zs = [z for (z, h) in rows if any(abs(y - cushion) <= 0.03 and not b for (y, b, _) in h)]
            zs.sort()
            # contiguous around the hips' z
            run = [min(zs, key=lambda z: abs(z - r["z"]))]
            for z in zs:
                pass
            lo_ = hi_ = run[0]
            while any(abs(z - (lo_ - 0.05)) < 0.02 for z in zs): lo_ -= 0.05
            while any(abs(z - (hi_ + 0.05)) < 0.02 for z in zs): hi_ += 0.05
            zr = (round(lo_, 2), round(hi_, 2))
        # floor under each foot target: the lowest up-facing surface from (cushion or hips)-0.8 up to the foot + 0.3
        feet = []
        if s.get("feet"):
            for k in ("footL", "footR"):
                fp = s[k]
                frows, fx = line(model, fp["x"])
                _, fh = at(frows, fp["z"])
                base = (cushion if cushion is not None else hips)
                fl = up_surfaces(fh, base - 0.9, fp["y"] + 0.35)
                backs = [y for (y, b, part) in fh if b and base - 0.9 <= y <= fp["y"] + 0.35 and not part.startswith("Wheel")]
                feet.append({"x": fp["x"], "y": fp["y"], "z": fp["z"], "floor_up": fl, "backs": backs, "line": fx})
        # the roof's inside over the hips: the first back face from the top that is over the hips + 0.4
        roofs = [y for (y, b, part) in hs if b and y > hips + 0.4]
        roof_in = max(roofs) if roofs else None
        txt = f"  s{i} {POSE.get(p, p):7s} root ({r['x']:+.2f},{r['y']:+.2f},{r['z']:+.2f}) face {'+z' if face > 0 else '-z'} hips {hips:.2f}"
        if roof_in: txt += f" roof-in {roof_in:.2f} (head room {roof_in - hips:.2f})"
        if cushion is not None:
            txt += f" | cushion {cushion:.2f} z {zr[0]:+.2f}..{zr[1]:+.2f} -> hips-cushion {hips - cushion:+.2f}"
        else:
            txt += f" | NO cushion (column: {' '.join(f'{y:.2f}{chr(42) if b else chr(32)}'.strip() for (y, b, _) in hs)})"
        for k, ft in zip(("L", "R"), feet):
            fl = ft["floor_up"]; top = max(fl) if fl else None
            txt += f" | foot{k} y {ft['y']:.2f} z {ft['z']:+.2f} up-surfs {fl} back {ft['backs'][:3]}"
        print(txt)
        geom[model].append({"seat": i, "pose": p, "hips": hips, "cushion": cushion, "cz": zr, "face": face, "line": lx, "feet": feet, "roof_in": roof_in})
json.dump(geom, open(OUT, "w"), indent=1)

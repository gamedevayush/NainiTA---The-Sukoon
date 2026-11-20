"""Fit every seat of the saved vehicle setups to the model's own shape (owner, 21 Nov: "perfect precision").

Reads the ProfileZ lines (every surface straight down, every 5 cm along each seat's line) from the Editor.log and the
setups in Assets/Resources/VehicleSetup, and for each seat:
  * a cushion under the hips: hips = cushion + NB (HumanoidRig.NominalButt; each character's own body moves it at play
    time), the hips 0.17 m in front of the backrest's foot (further forward if the floor is out of the shortest legs'
    reach), the feet on the floor just past the cushion's front edge (ankle = floor + 0.08, the nominal sole);
  * no cushion (an empty Tripo shell: sedan, van...): the hips keep the snap rule over the floor (car 0.27, chair 0.42);
  * a two-wheeler: hips = the seat's top at the hips + NB; the pillion at least 0.32 m behind the rider;
  * surface / roof stored (the sitting surface, the roof's inside) for the play-time fit and the checks.
Some seats are moved first (listed in MOVES: no seat under them in the model, two people in one seat, the wrong row).
Before running: in the editor, Play the VehicleSetup scene and `call VehicleSetupShots.RunProfileZ` with Bridge/arg.txt
= all and Bridge/arg2.txt = seats (the profiles land in the Editor.log).
python fit_vehicle_seats.py          -> the table of changes only
python fit_vehicle_seats.py --write  -> writes Assets/Resources/VehicleSetup/*.json (approved kept)
Source: always the owner's own setups as saved on 21 Nov 01:46 (setups_before_fit_21nov/), so a re-run never fits a
fitted seat again. Run on 21 Nov 2026 ~02:40 (docs/VEHICLES.md section 0).
"""
import os, re, json, glob, sys, math, copy
L = os.path.join(os.environ["LOCALAPPDATA"], "Unity", "Editor", "Editor.log")
D = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "VehicleSetup"))
WRITE = "--write" in sys.argv
NB = 0.12            # HumanoidRig.NominalButt
SOLE = 0.08          # HumanoidRig.NominalSoleSeated
OVER = {0: 0.51, 1: 0.51, 2: 0.40, 3: 0.40, 4: 0.51}
REACH = 0.50         # hip joint -> ankle, the shortest adult legs are 0.53 m
POSE = {0: "Drive", 1: "Sit", 2: "Ride", 3: "Pillion", 4: "Side"}

# ---- the profiles
prof = {}
cur = xl = None
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
            if mm: hs.append((float(mm.group(1)), mm.group(2) == "*", mm.group(3) or ""))
        prof[cur][xl].append((float(m.group(1)), hs))

def rows_at(model, x):
    xs = prof[model]; k = min(xs, key=lambda q: abs(q - x)); return xs[k]

def row(rows, z): return min(rows, key=lambda r: abs(r[0] - z))[1]

def ups(hs, lo, hi):
    return [y for (y, b, p) in hs if not b and lo <= y <= hi and not p.startswith("Wheel")]

def roof_in(hs, above):
    r = [y for (y, b, p) in hs if b and y > above]
    return max(r) if r else None

def cushion_run(rows, z, c):
    """The z run around z where an up surface stands within 3 cm of c."""
    zs = sorted(zz for (zz, h) in rows if any(abs(y - c) <= 0.035 and not b for (y, b, _) in h))
    if not zs: return None
    z0 = min(zs, key=lambda q: abs(q - z)); lo = hi = z0
    while any(abs(q - (lo - 0.05)) < 0.02 for q in zs): lo -= 0.05
    while any(abs(q - (hi + 0.05)) < 0.02 for q in zs): hi += 0.05
    return lo, hi

# ---- seats that must move before fitting (model: {seat: dict of new root x / z / yaw / entry z})
MOVES = {
    # the model has no seat at the back (open floor z -3.7..-2.7): the four back passengers onto the back-to-back row
    # (cushion z -2.56..-2.16, its backrest on the forward side), facing the back; the fifth onto the empty front-left seat
    "bus_roadways": {13: dict(z=-2.30, yaw=180), 14: dict(z=-2.30, yaw=180), 15: dict(z=-2.30, yaw=180), 16: dict(z=-2.30, yaw=180),
                     17: dict(x=-0.55, z=2.70, yaw=0)},
    # seats 19 / 20 sat 7 cm from seats 15 / 16 (two people in one seat): onto the empty left row z -1.36..-0.91
    "bus_tourist": {19: dict(z=-1.15), 20: dict(z=-1.15)},
    # the pillion sat in the rider's lap (17 cm apart): onto the pillion pad z -0.79..-0.49
    "bike_street160": {1: dict(z=-0.60)},
}
# per model: the two-wheelers' footpegs where the scan found them (x half-spread, ankle y, z) - else the feet stay
PEGS = {"bike_street160": {0: (0.22, 0.42, -0.15)}}
# the backrest's foot at the rear of each cushion is found from the run; drivers want their feet this far past the front
DRIVE_EXTRA = 0.10

# ---- seats set by hand from the profiles / the cut views (after the automatic fit; "auto": False = keep the old seat)
# keys: auto, rx/ry/rz (root), surface, feetY, feetZ, footLX/footRX, footLY/footRY, handL/handR (x,y,z), bend
FILLED = 0.30   # a Tripo row whose footwell is filled up to the cushion: the feet where a floor 0.30 m under it would be
OVR = {
    # the driver's cab: 0.91 is the cab floor (no cushion under him in the model); only the feet onto it
    "bus_tourist": {0: dict(auto=False, feetY=0.99),
                    **{i: dict(feetY=1.63) for i in range(1, 25)},
                    # the last row: the next seat's backrest stands 5 cm past the cushion - feet under its overhang
                    21: dict(feetY=1.63, feetZ=-1.66), 22: dict(feetY=1.63, feetZ=-1.66),
                    23: dict(feetY=1.63, feetZ=-1.66), 24: dict(rz=-1.99, feetY=1.63, feetZ=-1.66)},
    "bus_roadways": {14: dict(feetY=1.37), 15: dict(feetY=1.37)},
    # cushions 0.58-0.61 (the run detection split them); the backrest's foot at z -0.05 (front) / -0.83 (rear)
    "car_hatch_small": {0: dict(rx=0.37, ry=0.21, rz=0.12, surface=0.60, feetY=0.47, feetZ=0.51, footLX=0.22, footRX=0.52),
                        1: dict(rx=-0.37, ry=0.20, rz=0.12, surface=0.59, feetY=0.47, feetZ=0.55, footLX=-0.52, footRX=-0.22),
                        2: dict(ry=0.21, rz=-0.66, surface=0.60, feetY=0.47, feetZ=-0.36),
                        3: dict(ry=0.21, rz=-0.66, surface=0.60, feetY=0.47, feetZ=-0.36, footLX=-0.15, footRX=0.15),
                        4: dict(ry=0.21, rz=-0.66, surface=0.60, feetY=0.47, feetZ=-0.36)},
    # middle seat as its neighbours; the third row's footwell is filled to the cushion (0.73)
    "car_mpv_large": {3: dict(ry=0.33, rz=-0.77, surface=0.72, feetY=0.64, feetZ=-0.37, footLX=-0.15, footRX=0.15),
                      5: dict(ry=0.34, rz=-1.77, surface=0.73, feetY=0.73 - FILLED + SOLE, feetZ=-1.37, footLX=-0.65, footRX=-0.35),
                      6: dict(ry=0.34, rz=-1.77, surface=0.73, feetY=0.73 - FILLED + SOLE, feetZ=-1.37, footLX=-0.15, footRX=0.15),
                      7: dict(ry=0.34, rz=-1.77, surface=0.73, feetY=0.73 - FILLED + SOLE, feetZ=-1.37, footLX=0.35, footRX=0.65)},
    # the rear three sat behind the rear seat's backrest (in the boot): onto the seat (0.88, backrest at z -1.17),
    # its footwell filled up to the cushion
    "car_suv_boxy": {i: dict(ry=0.49, rz=-0.90, surface=0.88, feetY=0.88 - FILLED + SOLE, feetZ=-0.52) for i in (2, 3, 4)},
    # the driver sat 22 cm back, inside his backrest: onto the cushion (0.64, z -0.06..0.39), feet in the footwell (0.52)
    "car_tallboy": {0: dict(rx=0.36, ry=0.25, rz=0.11, surface=0.64, feetY=0.60, feetZ=0.56, footLX=0.21, footRX=0.51)},
    # the floor is 0.37 in the middle, a ledge 0.58 at the sides; the left grip is the bar's own end (z 0.67-0.74)
    "tirri_erickshaw": {0: dict(handL=(-0.34, 1.07, 0.69)),
                        1: dict(feetZ=-0.75, footLY=0.45, footRY=0.66), 2: dict(feetZ=-0.75, footLY=0.66, footRY=0.45)},
    # the passengers' floor (0.60) runs to z 0.06: the feet out on it, not tucked against the bench
    "tuktuk_auto": {1: dict(feetZ=-0.05), 2: dict(feetZ=-0.05)},
    # the pillion pegs high and near (a street bike's step seat), the grips symmetric at the bar ends (+-0.40) less 6 cm
    "bike_street160": {1: dict(feetY=0.54, feetZ=-0.42, footLX=-0.20, footRX=0.20)},
    "scooty_neoretro": {0: dict(handL=(-0.34, 1.07, 0.19), handR=(0.34, 1.07, 0.19), bend=8)},
}

out_rows = []
changed = {}
# always from the owner's own setups as saved at 01:46 (the backup), so a re-run never fits a fitted seat again
SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "setups_before_fit_21nov")
for src in sorted(glob.glob(os.path.join(SRC, "*.json"))):
    f = os.path.join(D, os.path.basename(src))
    j = json.load(open(src, encoding="utf-8"))
    model = j["model"]
    if model not in prof: print("NO PROFILE", model); continue
    two = any(k in model for k in ("bike", "scooty"))
    chair = any(k in model for k in ("bus", "truck", "pickup", "van", "tuktuk", "tirri", "rickshaw"))
    new = copy.deepcopy(j)
    for i, s in enumerate(new["seats"]):
        o = j["seats"][i]
        mv = MOVES.get(model, {}).get(i)
        if mv:
            if "x" in mv: dx = mv["x"] - s["pos"]["x"]; s["pos"]["x"] = mv["x"]; s["entry"]["x"] = s["entry"]["x"] if abs(s["entry"]["x"]) > 1 else s["entry"]["x"] + dx
            if "z" in mv: s["pos"]["z"] = mv["z"]; s["entry"]["z"] = mv["z"]
            if "yaw" in mv: s["euler"]["y"] = mv["yaw"]
        p = s["pose"]; over = OVER.get(p, 0.51)
        x, z = s["pos"]["x"], s["pos"]["z"]
        hips = s["pos"]["y"] + over
        rows = rows_at(model, x)
        hs = row(rows, z)
        face = -1 if abs(((s["euler"]["y"] + 360) % 360) - 180) < 30 else 1
        note = []
        if two:
            # the seat's top at the hips on the centre line
            crow = rows_at(model, 0.0)
            top = [y for (y, b, pp) in row(crow, z) if not b and not pp.startswith("Handle") and not pp.startswith("Wheel")]
            if top:
                surf = max(top)
                s["pos"]["y"] = round(surf + NB - over, 3); s["surface"] = round(surf, 3); s["roof"] = -1
            if model in PEGS and i in PEGS[model]:
                hx, ay, fz = PEGS[model][i]
                s["footL"] = {"x": round(x - hx, 3), "y": ay, "z": fz}; s["footR"] = {"x": round(x + hx, 3), "y": ay, "z": fz}; s["feet"] = True
                note.append("feet on the pegs")
            if i > 0 and (p == 3):
                # pillion feet: keep the rider-relative pegs if the pillion moved
                if mv and "z" in mv:
                    dz = mv["z"] - o["pos"]["z"]
                    for k in ("footL", "footR"): s[k]["z"] = round(o[k]["z"] + dz, 3)
                    note.append("feet moved with the pillion")
        else:
            cand = ups(hs, hips - 0.6, hips + 0.25)
            floor_hs = None
            cushion = max(cand) if cand else None
            run = cushion_run(rows, z, cushion) if cushion is not None else None
            # a "cushion" that is the floor itself (open floor, an empty shell): no cushion
            if cushion is not None and run and (run[1] - run[0]) > 1.2: cushion = None; note.append("long flat run = floor")
            if cushion is not None and run:
                rear = run[0] if face > 0 else run[1]
                front = run[1] if face > 0 else run[0]
                depth = run[1] - run[0]
                # the floor ahead of the front edge: the highest up surface 8+ cm under the cushion
                fz = None; floor = None
                for k in range(1, 14):
                    zz = front + face * 0.05 * k
                    fl = ups(row(rows, zz), cushion - 0.9, cushion - 0.08)
                    if fl: fz = zz; floor = max(fl); break
                if floor is None:
                    note.append("NO FLOOR ahead")
                    floor = cushion - 0.42; fz = front + face * 0.08
                foot_z = front + face * (0.07 + (DRIVE_EXTRA if p == 0 else 0.0))
                # not past the floor's own start
                if face * (foot_z - fz) < 0: foot_z = fz
                hips_y = cushion + NB
                hz = rear + face * 0.17
                def dist(hz_):
                    return math.sqrt((foot_z - hz_) ** 2 + ((hips_y - 0.07) - (floor + SOLE)) ** 2 + 0.06 ** 2)
                while dist(hz) > REACH and face * (hz - rear) < depth * 0.6: hz += face * 0.01
                if dist(hz) > REACH and p == 0:
                    # a driver: pedals nearer
                    while dist(hz) > REACH and face * (foot_z - front) > 0.04: foot_z -= face * 0.01
                s["pos"]["y"] = round(hips_y - over, 3)
                s["pos"]["z"] = round(hz, 3)
                if not mv or "z" not in mv:
                    if abs(s["entry"]["z"] - o["pos"]["z"]) < 0.02: s["entry"]["z"] = round(hz, 3)
                s["surface"] = round(cushion, 3)
                rf = roof_in(hs, hips_y + 0.3); s["roof"] = round(rf, 3) if rf else -1
                # feet: the same spread about the seat's x as they had (else 0.15 each side), on the floor ahead
                spread = abs(o["footR"]["x"] - o["footL"]["x"]) / 2 if o.get("feet") else 0.15
                if spread < 0.10 or spread > 0.22: spread = 0.15
                s["feet"] = True
                lx, rx = (x - spread, x + spread) if face > 0 else (x + spread, x - spread)
                s["footL"] = {"x": round(lx, 3), "y": round(floor + SOLE, 3), "z": round(foot_z, 3)}
                s["footR"] = {"x": round(rx, 3), "y": round(floor + SOLE, 3), "z": round(foot_z, 3)}
                note.append(f"cushion {cushion:.2f} z {run[0]:+.2f}..{run[1]:+.2f} floor {floor:.2f}, reach {dist(hz):.2f}")
            else:
                # an empty shell: the floor = the underbody's inside under the hips
                backs = [y for (y, b, pp) in hs if b and y < hips]
                fl = min(backs) if backs else None
                if fl is not None:
                    want = fl + (0.42 if chair else 0.27)
                    s["pos"]["y"] = round(want - over, 3)
                    s["surface"] = round(want - NB, 3)
                    rf = roof_in(hs, want + 0.3); s["roof"] = round(rf, 3) if rf else -1
                    for k in ("footL", "footR"):
                        frow = row(rows_at(model, s[k]["x"]), s[k]["z"])
                        fb = [y for (y, b, pp) in frow if b and y < want]
                        fu = [y for (y, b, pp) in frow if not b and y < want - 0.1 and not pp.startswith("Wheel")]
                        fy = max(fu) if fu else (min(fb) if fb else fl)
                        s[k]["y"] = round(fy + SOLE, 3)
                    note.append(f"empty shell, floor {fl:.2f}")
        ov = OVR.get(model, {}).get(i)
        if ov:
            if ov.get("auto") is False:
                keep = {k: copy.deepcopy(o[k]) for k in ("pos", "footL", "footR")}
                s.update(keep)
                s["surface"] = -1   # nothing measured under him
                hs0 = row(rows_at(model, o["pos"]["x"]), o["pos"]["z"])
                rf = roof_in(hs0, o["pos"]["y"] + over + 0.3); s["roof"] = round(rf, 3) if rf else -1
            for k, c in (("rx", "x"), ("ry", "y"), ("rz", "z")):
                if k in ov:
                    s["pos"][c] = ov[k]
                    if c == "z" and abs(s["entry"]["z"] - o["pos"]["z"]) < 0.05: s["entry"]["z"] = ov[k]
            if "surface" in ov: s["surface"] = ov["surface"]
            if "rz" in ov or "ry" in ov:
                hs1 = row(rows_at(model, s["pos"]["x"]), s["pos"]["z"])
                rf = roof_in(hs1, s["pos"]["y"] + over + 0.3); s["roof"] = round(rf, 3) if rf else s.get("roof", -1)
            for k in ("footL", "footR"):
                if "feetY" in ov: s[k]["y"] = round(ov["feetY"], 3)
                if "feetZ" in ov: s[k]["z"] = ov["feetZ"]
            if "footLX" in ov: s["footL"]["x"] = ov["footLX"]
            if "footRX" in ov: s["footR"]["x"] = ov["footRX"]
            if "footLY" in ov: s["footL"]["y"] = ov["footLY"]
            if "footRY" in ov: s["footR"]["y"] = ov["footRY"]
            if "handL" in ov: s["handL"] = dict(zip("xyz", ov["handL"]))
            if "handR" in ov: s["handR"] = dict(zip("xyz", ov["handR"]))
            if "bend" in ov: s["bendSet"] = True; s["bend"] = float(ov["bend"])
            note.append("hand-set")
        # what changed
        d = []
        for k in ("x", "y", "z"):
            if abs(s["pos"][k] - o["pos"][k]) >= 0.005: d.append(f"root.{k} {o['pos'][k]:+.2f}->{s['pos'][k]:+.2f}")
        if abs(s["euler"]["y"] - o["euler"]["y"]) > 1: d.append(f"yaw {o['euler']['y']:.0f}->{s['euler']['y']:.0f}")
        for k in ("footL", "footR"):
            if s.get("feet") and o.get("feet"):
                dd = math.dist([s[k][c] for c in "xyz"], [o[k][c] for c in "xyz"])
                if dd >= 0.01: d.append(f"{k} ({o[k]['x']:+.2f},{o[k]['y']:.2f},{o[k]['z']:+.2f})->({s[k]['x']:+.2f},{s[k]['y']:.2f},{s[k]['z']:+.2f})")
        out_rows.append(f"{model:18s} s{i:<2d} {POSE.get(p, p):7s} {'; '.join(d) if d else '-'}   [{' | '.join(note)}]")
    changed[f] = new

print("\n".join(out_rows))
if WRITE:
    for f, j in changed.items():
        open(f, "w", encoding="utf-8").write(json.dumps(j, indent=4))
    print(f"wrote {len(changed)} files")

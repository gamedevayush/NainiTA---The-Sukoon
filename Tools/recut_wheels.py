"""
Cut the four tyres out of a game vehicle again, better (user, 2026-11-18: "tyres theek se cut nahi huye, sabke hi").
Works on the installed game FBX (make_vehicle_lods.py output: Mesh_L0/L1/L2 + Wheel_0..3, front -Y, x across, z up,
ground at z = 0): the old wheel pieces go back into Mesh_L0, the tyres are found again and cut out again.

  blender -b --factory-startup --python-exit-code 1 -P recut_wheels.py -- <model.fbx> <sidecar.json>

How a tyre is found (per wheel, from a first guess at the hub):
- radius: circles touching the ground (centre at height r) are scored by how many vertices lie on them; the tread is
  the LARGEST radius that still scores at least half the best (the old cut took the strongest circle, often the rim,
  leaving the tyre's rubber on the body: the ambulance, the buses);
- width: the spread across (x) of the vertices inside that circle below the hub (only tyre is down there), so wide
  and twin rear tyres (bus, truck) come out whole; the old cut used a fixed 22 cm;
- a face goes with the wheel if all its corners are inside the circle (4 % slack) and inside that width.
Writes the FBX back with the same object names and updates the sidecar (wheels, triangle counts).
"""
import bpy, bmesh, sys, os, json, math
import numpy as np
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
FBX, JSON = argv[0], argv[1]
side = json.load(open(JSON, encoding="utf-8"))
NAME = side["name"]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
objs = {o.name: o for o in bpy.context.scene.objects if o.type == "MESH"}
body = objs.get("Mesh_L0")
assert body, "no Mesh_L0 in " + FBX
for o in list(bpy.context.scene.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
for o in bpy.context.scene.objects:
    o.select_set(False)
# everything in world space, no parents (the importer may add a root and a 100x scale)
for o in bpy.context.scene.objects:
    mw = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = mw
bpy.context.view_layer.objects.active = body
for o in bpy.context.scene.objects:
    o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in bpy.context.scene.objects:
    o.select_set(False)

# old wheel pieces back into the body
old = [o for n, o in objs.items() if n.startswith("Wheel_")]
if old:
    for o in old:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "Mesh_L0"
me = body.data
co = np.array([v.co[:] for v in me.vertices])
zmin = co[:, 2].min()
Wh, Lh = np.abs(co[:, 0]).max(), np.abs(co[:, 1]).max()


def first_guess(sx, sy):
    sel = (co[:, 2] < zmin + 0.08) & (co[:, 0] * sx > Wh * 0.3) & (co[:, 1] * sy > Lh * 0.15)
    if sel.sum() < 3:
        return None
    return float(np.median(co[sel, 0])), float(np.median(co[sel, 1]))


RMAX = 0.62 if Lh * 2 < 6.5 else 0.95   # buses and trucks have big tyres
RS = np.arange(0.22, RMAX, 0.01)


def curve(cx, ay0):
    """For every radius: the best score and where the hub is. The tread is a circle touching the ground whose rim is
    dense with vertices in the tyre's LOWER half (no body there) while just outside it there is air; the wheel's rim
    has rubber just outside it (penalised); a circle too big runs through air (few vertices on it)."""
    slab = co[(np.abs(co[:, 0] - cx) < 0.3) & (np.abs(co[:, 1] - ay0) < 1.4) & (co[:, 2] < zmin + RMAX + 0.1)]
    out = []
    for r in RS:
        low = slab[slab[:, 2] < zmin + r]
        best = (-1e9, ay0)
        for ay in np.arange(ay0 - 0.25, ay0 + 0.251, 0.02):
            d = np.hypot(low[:, 1] - ay, low[:, 2] - zmin - r)
            ring = np.count_nonzero(np.abs(d - r) < 0.02)
            air = np.count_nonzero((d > r + 0.025) & (d < r + 0.08))
            score = ring - 1.5 * air
            if score > best[0]:
                best = (score, float(ay))
        out.append(best)
    return out


raw = []
for sy in (-1, 1):
    for sx in (-1, 1):
        g = first_guess(sx, sy)
        if g is None:
            print("RECUT", NAME, "no wheels found; left as it is")
            sys.exit(0)
        raw.append((g[0], curve(*g)))

# a car's four tyres are one size (the radius best for all four together); a bus or truck: one size per axle
def pick(ids):
    tot = np.zeros(len(RS))
    for i in ids:
        sc = np.array([c[0] for c in raw[i][1]], dtype=float)
        tot += sc / max(1.0, sc.max())
    return int(np.argmax(tot))

groups = [(0, 1, 2, 3)] if Lh * 2 < 6.5 else [(0, 1), (2, 3)]
chosen = {}
for gids in groups:
    k = pick(gids)
    for i in gids:
        chosen[i] = k
wheels_hub = []
for i, (cx, cur) in enumerate(raw):
    k = chosen[i]
    wheels_hub.append([cx, cur[k][1], float(RS[k])])
for a, b in ((0, 1), (2, 3)):   # an axle's wheels line up
    ay = (wheels_hub[a][1] + wheels_hub[b][1]) / 2
    wheels_hub[a][1] = wheels_hub[b][1] = ay
raw = wheels_hub

wheels = []
for cx, ay, r in raw:
    hub = np.array([0, ay, zmin + r])
    d = np.hypot(co[:, 1] - ay, co[:, 2] - hub[2])
    low = co[(d <= r * 1.02) & (co[:, 2] < hub[2] - r * 0.15) & (np.abs(co[:, 0] - cx) < 0.6)]
    if len(low) < 10:
        x0, x1 = cx - 0.12, cx + 0.12
    else:
        x0, x1 = np.percentile(low[:, 0], 2), np.percentile(low[:, 0], 98)
        # the tyre side towards the middle of the car stops at the body's inner parts: keep it to <= 0.6 m wide
        if x1 - x0 > 0.62:
            if cx > 0: x0 = x1 - 0.62
            else: x1 = x0 + 0.62
    wheels.append((float((x0 + x1) / 2), float(ay), float(r), float(x0) - 0.02, float(x1) + 0.02))

bm = bmesh.new()
bm.from_mesh(me)
owner = {}
for f in bm.faces:
    for k, (cx, ay, r, x0, x1) in enumerate(wheels):
        hz = zmin + r
        # the upper half tighter: the fender's lip sits just above the tread
        if all(math.hypot(v.co.y - ay, v.co.z - hz) <= r * (0.97 if v.co.z > hz + r * 0.3 else 1.04) and x0 <= v.co.x <= x1 for v in f.verts):
            owner[f.index] = k
            break
bm.free()


def part(keep, name):
    o = body.copy()
    o.data = body.data.copy()
    o.name = name
    o.data.name = NAME + "_" + name
    bpy.context.scene.collection.objects.link(o)
    b2 = bmesh.new()
    b2.from_mesh(o.data)
    b2.faces.ensure_lookup_table()
    bmesh.ops.delete(b2, geom=[f for f in b2.faces if not keep(f.index)], context="FACES")
    b2.to_mesh(o.data)
    b2.free()
    return o


new_body = part(lambda i: i not in owner, "Mesh_L0_new")
outs = []
for k, (cx, ay, r, x0, x1) in enumerate(wheels):
    o = part(lambda i, k=k: owner.get(i) == k, f"Wheel_{k}")
    o.data.transform(Matrix.Translation((-cx, -ay, -(zmin + r))))   # pivot at the hub
    o.location = (cx, ay, zmin + r)
    outs.append(o)
bpy.data.objects.remove(body, do_unlink=True)
new_body.name = "Mesh_L0"
new_body.data.name = NAME + "_Mesh_L0"


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


print("RECUT", NAME, [(round(cx, 2), round(ay, 2), round(r, 2), round(x1 - x0, 2), sum(1 for v in owner.values() if v == k))
                      for k, (cx, ay, r, x0, x1) in enumerate(wheels)])

# every changed object keeps only the material slots it uses; the sidecar says which (global index), in sub-mesh order
gname = [m["name"] for m in side["materials"]]
changed = {}
for o in [new_body] + outs:
    for x in bpy.context.scene.objects:
        x.select_set(x == o)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.material_slot_remove_unused()
    mats, per = [], []
    for k, sl in enumerate(o.material_slots):
        n = sl.material.name if sl.material else ""
        base = n.split(".")[0] if n not in gname else n
        mats.append(gname.index(n) if n in gname else (gname.index(base) if base in gname else k))
        per.append(sum(len(p.vertices) - 2 for p in o.data.polygons if p.material_index == k))
    changed[o.name] = {"name": o.name, "mats": mats, "tris": per}

for o in bpy.context.scene.objects:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={"MESH"}, mesh_smooth_type="FACE",
                         path_mode="STRIP", embed_textures=False, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", bake_space_transform=True)

side["wheels"] = [[round(cx, 3), round(ay, 3), round(r, 3)] for cx, ay, r, x0, x1 in wheels]
side["wheel_tris"] = sum(tris(o) for o in outs)
side["tris"][0] = tris(new_body)
objs_out = [e for e in side["objects"] if e["name"] not in changed and not e["name"].startswith("Wheel_")]
objs_out = [changed["Mesh_L0"]] + [e for e in objs_out if e["name"] != "Mesh_L0"] + [changed[o.name] for o in outs]
side["objects"] = objs_out
json.dump(side, open(JSON, "w", encoding="utf-8"), indent=1)
print("RECUT", NAME, "written", FBX, "L0", tris(new_body), "wheels", side["wheel_tris"])

"""
Game-ready vehicle from the user's model library (gamedevayush/SamjhaaoSkills models/library), in Blender (headless).
Same output shape as make_building_lods.py (Mesh_L0/L1/L2 FBX + JSON sidecar + textures), so one Unity kit reads both.

Usage:
  blender -b --factory-startup --python-exit-code 1 -P make_vehicle_lods.py -- \
      <in.glb|fbx> <out.fbx> <front +x|-x|+y|-y> <length_m> <lod0_tris> <lod1_tris> <lod2_tris>

- Joins all meshes, turns the vehicle so its front faces Blender -Y (Unity +Z after export), scales it to the real
  length, pivot at the bottom centre.
- LOD0 is capped at lod0_tris (user, 2026-11-11: no vehicle above 20k).
- Writes the base colour, normal and ORM (occlusion/roughness/metal) textures next to the FBX.
Prints: VEH <name> <w> <l> <h> <full tris> <l0> <l1> <l2>
"""
import bpy, sys, os, json, math, shutil
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
IN, OUT, FRONT = argv[0], argv[1], argv[2]
LENGTH = float(argv[3])
TARGETS = [int(argv[4]), int(argv[5]), int(argv[6])]
NAME = os.path.splitext(os.path.basename(OUT))[0]

bpy.ops.wm.read_factory_settings(use_empty=True)
ext = os.path.splitext(IN)[1].lower()
if ext in (".glb", ".gltf"):
    bpy.ops.import_scene.gltf(filepath=IN)
else:
    bpy.ops.import_scene.fbx(filepath=IN)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
assert meshes, "no mesh in " + IN
for o in bpy.context.scene.objects:
    o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
if obj.data.shape_keys:   # morph targets block the decimate modifier; a vehicle needs none
    obj.shape_key_clear()
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(bpy.context.scene.objects):
    if o != obj:
        bpy.data.objects.remove(o, do_unlink=True)

# front -> -Y
yaw = {"+x": -90.0, "-x": 90.0, "+y": 180.0, "-y": 0.0}[FRONT]
me = obj.data
me.transform(Matrix.Rotation(math.radians(yaw), 4, "Z"))
xs = [v.co for v in me.vertices]
mn = Vector((min(c.x for c in xs), min(c.y for c in xs), min(c.z for c in xs)))
mx = Vector((max(c.x for c in xs), max(c.y for c in xs), max(c.z for c in xs)))
s = LENGTH / (mx.y - mn.y)
me.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z)))
me.update()
size = (mx - mn) * s


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


full = tris(obj)
class _Slot:
    def __init__(self, m): self.material = m
full_slots = [_Slot(sl.material) for sl in obj.material_slots]


def decimate_to(o, target):
    t0 = tris(o)
    if t0 <= target:
        return
    for x in bpy.context.scene.objects:
        x.select_set(x == o)
    bpy.context.view_layer.objects.active = o
    for _ in range(5):
        if tris(o) <= target:
            break
        m = o.modifiers.new("Decimate", "DECIMATE")
        m.ratio = max(0.01, target / tris(o) * 0.98)
        m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier="Decimate")


# ---- wheels: found on the full mesh, cut out as four spinning parts for LOD0 (user, 2026-11-11: "tyre nahi ghoomre")
import numpy as np
import bmesh
from mathutils.bvhtree import BVHTree

co = np.array([v.co[:] for v in me.vertices])
Wh, Lh = np.abs(co[:, 0]).max(), np.abs(co[:, 1]).max()


def find_wheel(sx, sy):
    sel = (co[:, 2] < 0.07) & (co[:, 0] * sx > Wh * 0.35) & (co[:, 1] * sy > Lh * 0.2)
    if sel.sum() < 3:
        return None
    ay0, cx = float(np.median(co[sel, 1])), float(np.median(co[sel, 0]))
    slab = co[(np.abs(co[:, 0] - cx) < 0.2) & (np.abs(co[:, 1] - ay0) < 1.0) & (co[:, 2] < 1.5)]
    rs = np.arange(0.22, 0.70, 0.01)
    curve = []   # for each radius: the best count of vertices on that circle, and where its hub is
    for r in rs:
        bn, bay = 0, ay0
        for ay in np.arange(ay0 - 0.18, ay0 + 0.181, 0.03):
            n = np.count_nonzero(np.abs(np.hypot(slab[:, 1] - ay, slab[:, 2] - r) - r) < 0.02)
            if n > bn:
                bn, bay = n, float(ay)
        curve.append((bn, bay))
    return cx, rs, curve


raw = []
for sy in (-1, 1):          # front (-Y) first, then rear
    for sx in (-1, 1):
        raw.append(find_wheel(sx, sy))
wheels = []
if all(w is not None for w in raw):
    # one tyre size for all four: the radius whose circle collects the most vertices over the four wheels together
    # (density along the circle, so a big circle crossing the body side does not win by length alone)
    rs = raw[0][1]
    total = np.zeros(len(rs))
    for cx, _, curve in raw:
        n = np.array([c[0] for c in curve], dtype=float)
        total += n / max(1.0, n.max())
    total /= rs ** 0.5
    if os.environ.get("WHEEL_DEBUG"):
        print("WHEELDBG", [(round(float(r), 2), round(float(t), 2)) for r, t in zip(rs, total)])
    k = int(np.argmax(total))
    wheels = [(cx, curve[k][1], float(rs[k])) for cx, _, curve in raw]
    # left and right wheels of an axle must line up, or the cut would take a wrong piece of the body
    if abs(wheels[0][1] - wheels[1][1]) > 0.12 or abs(wheels[2][1] - wheels[3][1]) > 0.12:
        print("WHEELS do not line up", [round(w[1], 2) for w in wheels])
        wheels = []


def _unused(): pass


if len(wheels) != 4:
    print("WHEELS not found, kept on the body", len(wheels))
    wheels = []

wheel_objs = []
body_src = obj
if wheels:
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    owner = {}
    for f in bm.faces:
        for k, (cx, ay, r) in enumerate(wheels):
            inside = all(math.hypot(v.co.y - ay, v.co.z - r) <= r * 1.04 and abs(v.co.x - cx) <= 0.22 for v in f.verts)
            if inside:
                owner[f.index] = k
                break
    bm.free()

    def part(keep, name):
        o = obj.copy()
        o.data = obj.data.copy()
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

    body_src = part(lambda i: i not in owner, "Body_full")
    for k, (cx, ay, r) in enumerate(wheels):
        o = part(lambda i, k=k: owner.get(i) == k, f"Wheel_{k}")
        o.data.transform(Matrix.Translation((-cx, -ay, -r)))   # pivot at the hub
        o.location = (cx, ay, r)
        decimate_to(o, 650)
        wheel_objs.append(o)
    print("WHEELS", [(round(cx, 2), round(ay, 2), round(r, 2), sum(1 for v in owner.values() if v == k)) for k, (cx, ay, r) in enumerate(wheels)])

# ---- lamps: where rays from in front of and behind the vehicle meet its body (head lamps, tail lamps)
lamps = {"front": [], "rear": []}
bvh = BVHTree.FromObject(body_src, bpy.context.evaluated_depsgraph_get())
r_hub = wheels[0][2] if wheels else 0.35
for key, sy in (("front", -1), ("rear", 1)):
    for zf in (1.9, 1.6, 2.2, 1.3, 2.6):
        z = r_hub * zf + 0.05
        hit = bvh.ray_cast(Vector((Wh * 0.62, sy * (Lh + 1.0), z)), Vector((0, -sy, 0)), Lh * 2)
        if hit[0] is not None and abs(hit[1].y) > 0.3:   # facing out, not a grazing side hit
            lamps[key] = [round(abs(hit[0].x), 3), round(hit[0].z, 3), round(abs(hit[0].y), 3)]
            break
print("LAMPS", lamps)

wheel_tris = sum(tris(o) for o in wheel_objs)
lods = []
for i, target in enumerate(TARGETS):
    src = body_src if (i == 0 and wheel_objs) else obj
    o = src.copy()
    o.data = src.data.copy()
    o.name = f"Mesh_L{i}"
    o.data.name = f"{NAME}_L{i}"
    bpy.context.scene.collection.objects.link(o)
    if i == 0 and wheel_objs:
        target = target - wheel_tris
    ratio = min(1.0, target / max(tris(o), 1))
    if ratio < 0.999:
        m = o.modifiers.new("Decimate", "DECIMATE")
        m.ratio = ratio
        m.use_collapse_triangulate = True
        for x in bpy.context.scene.objects:
            x.select_set(x == o)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier="Decimate")
    # a collapse can land a little over the target: trim again until it fits
    guard = 0
    while tris(o) > target and guard < 4:
        m = o.modifiers.new("Decimate", "DECIMATE")
        m.ratio = target / tris(o) * 0.98
        m.use_collapse_triangulate = True
        for x in bpy.context.scene.objects:
            x.select_set(x == o)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier="Decimate")
        guard += 1
    lods.append(o)
bpy.data.objects.remove(obj, do_unlink=True)
if body_src is not obj:
    bpy.data.objects.remove(body_src, do_unlink=True)

# every object keeps only the material slots it uses; the sidecar says which, in sub-mesh order
parts = lods + wheel_objs
obj_mats = {}
for o in parts:
    for x in bpy.context.scene.objects:
        x.select_set(x == o)
    bpy.context.view_layer.objects.active = o
    names_before = [s.material.name if s.material else "" for s in o.material_slots]
    bpy.ops.object.material_slot_remove_unused()
    obj_mats[o.name] = ([names_before.index(s.material.name if s.material else "") for s in o.material_slots],
                        [sum(len(p.vertices) - 2 for p in o.data.polygons if p.material_index == k) for k in range(len(o.material_slots))])
for x in bpy.context.scene.objects:
    x.select_set(x in parts)
out_dir = os.path.dirname(OUT)
os.makedirs(out_dir, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=OUT, use_selection=True, object_types={"MESH"}, mesh_smooth_type="FACE",
    path_mode="STRIP", embed_textures=False, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y", bake_space_transform=True)


def linked_image(node_tree, socket_name):
    for n in node_tree.nodes:
        if n.type != "BSDF_PRINCIPLED":
            continue
        sock = n.inputs.get(socket_name)
        if not sock or not sock.is_linked:
            return None
        src = sock.links[0].from_node
        for _ in range(3):   # through normal-map / separate-colour nodes
            if src.type == "TEX_IMAGE":
                break
            nxt = next((i for i in src.inputs if i.is_linked), None)
            if not nxt:
                break
            src = nxt.links[0].from_node
        return src.image if src.type == "TEX_IMAGE" else None
    return None


def save_image(img, dst_noext):
    if img.packed_file:
        data = bytes(img.packed_file.data)
        dst = dst_noext + (".png" if data[:4] == b"\x89PNG" else ".jpg")
        with open(dst, "wb") as fh:
            fh.write(data)
        return dst
    src = bpy.path.abspath(img.filepath) if img.filepath else ""
    if src and os.path.exists(src):
        dst = dst_noext + os.path.splitext(src)[1].lower()
        shutil.copyfile(src, dst)
        return dst
    dst = dst_noext + ".png"   # glb: in memory only
    img.filepath_raw = dst
    img.file_format = "PNG"
    img.save()
    return dst


all_slots = full_slots
tex_dir = os.path.join(out_dir, "Textures")
os.makedirs(tex_dir, exist_ok=True)
mats = []
for i, slot in enumerate(all_slots):
    m = slot.material
    entry = {"name": m.name if m else "", "color": "", "normal": "", "orm": "", "rgba": [1, 1, 1, 1]}
    if m and m.use_nodes:
        bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf:   # the flat colour of an untextured part (tyres, glass, chrome)
            entry["rgba"] = [round(float(c), 3) for c in bsdf.inputs["Base Color"].default_value]
    elif m:
        entry["rgba"] = [round(float(c), 3) for c in m.diffuse_color]
    if m and m.use_nodes:
        for key, sock in (("color", "Base Color"), ("normal", "Normal"), ("orm", "Roughness")):
            img = linked_image(m.node_tree, sock)
            if img:
                entry[key] = os.path.basename(save_image(img, os.path.join(tex_dir, f"{NAME}_m{i}_{key}")))
    mats.append(entry)
with open(os.path.join(out_dir, NAME + ".json"), "w") as f:
    json.dump({"name": NAME, "size": [round(size.x, 3), round(size.y, 3), round(size.z, 3)],
               "tris": [tris(o) for o in lods], "materials": mats, "wheel_tris": wheel_tris,
               "wheels": [[round(c, 3) for c in w] for w in wheels], "lamps": lamps,
               "objects": [{"name": k, "mats": v[0], "tris": v[1]} for k, v in obj_mats.items()]}, f, indent=1)
print(f"VEH {NAME} {size.x:.2f} {size.y:.2f} {size.z:.2f} {full} {tris(lods[0])} {tris(lods[1])} {tris(lods[2])} mats={len(mats)} tex={[m['color'] != '' for m in mats]}")

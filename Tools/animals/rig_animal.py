"""
Rig a Tripo street animal for Naini Sukoon (24 Nov 2026) and key its legacy clips.
  blender -b --factory-startup --python-exit-code 1 -P rig_animal.py -- <decoded.glb> <kind> <outdir> [front]
kind: cow | dog | cat | monkey.  front: the model's facing before the turn (+X, -X, +Y, -Y; default from FRONT below).
Output (outdir): <kind>.fbx (mesh + armature + clips idle / walk / run / <specials>), <kind>_color.png, <kind>.blend,
<kind>_check.png (poses on magenta: any magenta inside the body = a hole). Prints the clips' own speeds (CLIPSPEED)
for Animal.Spec clipWalk / clipRun.
Conventions: front -Y (Unity +Z after the FBX axis bake), ground z = 0, real size (LENGTH, nose to tail end).
Bones come from the mesh itself: four feet = clusters of the low vertices, the belly line on the centre, the nose = the
front-most vertex, the tail = the vertices behind the back feet. Weights are computed here (inverse distance to each bone
segment, top two, legs only on their own side and half) - Blender's heat weights fail on Tripo meshes.
"""
import bpy, bmesh, sys, math, os
from mathutils import Vector, Matrix, Quaternion
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
GLB, KIND, OUT = argv[0], argv[1], argv[2]
FRONT = {"cow": "+X", "dog": "+X", "cat": "+X", "monkey": "+X"}
front = argv[3] if len(argv) > 3 else FRONT[KIND]
LENGTH = {"cow": 2.2, "dog": 0.95, "cat": 0.55, "monkey": 0.7}[KIND]
SPECIALS = {"cow": ["chew"], "dog": ["sit"], "cat": ["sit", "lick"], "monkey": ["sit"]}[KIND]
RUN_T = {"cow": 0.7, "dog": 0.42, "cat": 0.36, "monkey": 0.45}[KIND]
FPS = 30
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = FPS
bpy.ops.import_scene.gltf(filepath=GLB)
meshes = [o for o in sc.objects if o.type == "MESH"]
for o in sc.objects: o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
for o in list(sc.objects):
    if o != ob: bpy.data.objects.remove(o)
ob.parent = None
ob.matrix_world = ob.matrix_world.copy()
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
ob.name = KIND
me = ob.data

# weld glTF seam splits, normals out, no back-face culling
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(me); bm.free()
for m in me.materials:
    if m: m.use_backface_culling = False

# turn so the animal faces -Y, scale to its real length, feet on z 0, centred
turn = {"+X": -math.pi / 2, "-X": math.pi / 2, "+Y": math.pi, "-Y": 0.0}[front]
me.transform(Matrix.Rotation(turn, 4, "Z"))
V = np.array([v.co[:] for v in me.vertices])
# Tripo keeps the photo's 3/4 yaw: straighten on the feet (low vertices) - their long axis goes to Y (smallest turn)
lowv = V[V[:, 2] < V[:, 2].min() + 0.12 * (V[:, 2].max() - V[:, 2].min())][:, :2]
ev, evec = np.linalg.eigh(np.cov((lowv - lowv.mean(axis=0)).T))
ax = evec[:, ev.argmax()]
yaw = math.atan2(ax[0], ax[1])            # angle of the feet line from +Y
if yaw > math.pi / 2: yaw -= math.pi
if yaw < -math.pi / 2: yaw += math.pi
me.transform(Matrix.Rotation(yaw, 4, "Z"))
print("YAW straightened by %.1f deg" % math.degrees(yaw))
V = np.array([v.co[:] for v in me.vertices])
s = LENGTH / (V[:, 1].max() - V[:, 1].min())
V *= s
V[:, 0] -= (V[:, 0].max() + V[:, 0].min()) / 2
V[:, 1] -= (V[:, 1].max() + V[:, 1].min()) / 2
V[:, 2] -= V[:, 2].min()
me.vertices.foreach_set("co", V.ravel())
me.update()
H = V[:, 2].max(); L = LENGTH; W = V[:, 0].max() - V[:, 0].min()
print("SIZE %s length %.2f width %.2f height %.2f verts %d tris %d" % (KIND, L, W, H, len(V), sum(len(p.vertices) - 2 for p in me.polygons)))

# ---------------------------------------------------------------- skeleton from the mesh
low = V[V[:, 2] < 0.12 * H]
ymid = (low[:, 1].max() + low[:, 1].min()) / 2
xmid = np.median(low[:, 0])
feet = {}
for fb, ysel in (("F", low[:, 1] < ymid), ("B", low[:, 1] >= ymid)):
    for lr, xsel in (("L", low[:, 0] > xmid), ("R", low[:, 0] <= xmid)):   # -Y front: +X is the animal's left
        c = low[ysel & xsel]
        if len(c) < 5: c = low[ysel]
        feet[fb + lr] = Vector((c[:, 0].mean(), c[:, 1].mean(), 0.0))
yF = (feet["FL"].y + feet["FR"].y) / 2
yB = (feet["BL"].y + feet["BR"].y) / 2
sep = abs(feet["FL"].x - feet["FR"].x) + abs(feet["BL"].x - feet["BR"].x)

def band(y, w=0.05):
    return V[np.abs(V[:, 1] - y) < w * L]

def belly_top(y):
    b = band(y)
    centre = b[np.abs(b[:, 0] - xmid) < max(0.12 * sep, 0.02 * L)]
    if len(centre) < 4: centre = b
    upper = centre[centre[:, 2] > 0.2 * H]
    belly = upper[:, 2].min() if len(upper) else 0.45 * H
    return belly, b[:, 2].max()

_, tF = belly_top(yF); _, tB = belly_top(yB)
# the belly line is measured under the barrel between the leg pairs (at the legs the dewlap / udder / the legs themselves
# sit on the centre line and read far too low)
mid = band(yF + 0.45 * (yB - yF), 0.08)
mc = mid[(np.abs(mid[:, 0] - xmid) < 0.04 * L) & (mid[:, 2] > 0.15 * H)]
belly = float(np.percentile(mc[:, 2], 3)) if len(mc) > 10 else 0.5 * H
bF = bB = belly
zHip = bB + 0.45 * (tB - bB); zChest = bF + 0.45 * (tF - bF)
nose_i = V[:, 1].argmin(); nose = Vector(V[nose_i])
headpts = V[V[:, 1] < V[:, 1].min() + 0.12 * L]
headc = Vector(headpts.mean(axis=0))
tailpts = V[(V[:, 1] > yB + 0.08 * L) & (V[:, 2] > 0.08 * H)]
print("SKEL feet", {k: tuple(round(c, 2) for c in v) for k, v in feet.items()}, "belly F %.2f B %.2f top F %.2f B %.2f" % (bF, bB, tF, tB),
      "nose", tuple(round(c, 2) for c in nose), "tail verts", len(tailpts))

arm_data = bpy.data.armatures.new(KIND + "_rig")
rig = bpy.data.objects.new(KIND + "_rig", arm_data)
sc.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
eb = arm_data.edit_bones
J = {}

def bone(name, head, tail, parent=None, connect=False):
    b = eb.new(name); b.head = head; b.tail = tail
    if parent: b.parent = eb[parent]; b.use_connect = connect
    J[name] = (Vector(head), Vector(tail))
    return b

hip = Vector((xmid, yB, zHip)); chest = Vector((xmid, yF, zChest))
bone("root", Vector((xmid, (yB + yF) / 2, 0)), Vector((xmid, (yB + yF) / 2 - 0.15 * L, 0)))
bone("hips", hip, hip.lerp(chest, 0.33), "root")
bone("spine", hip.lerp(chest, 0.33), hip.lerp(chest, 0.66), "hips", True)
bone("chest", hip.lerp(chest, 0.66), chest, "spine", True)
neck_end = headc.lerp(chest, 0.25)
neck_end.z = max(neck_end.z, chest.z)
bone("neck", chest, neck_end, "chest", True)
bone("head", neck_end, nose, "neck", True)
for k, f in feet.items():
    front_leg = k[0] == "F"
    top = Vector((f.x, f.y, zChest if front_leg else zHip))
    belly = bF if front_leg else bB
    knee = Vector((f.x, f.y + (0.03 if front_leg else -0.04) * L, 0.5 * belly))
    ankle = Vector((f.x, f.y, 0.13 * belly))
    toe = Vector((f.x, f.y - 0.05 * L, 0.0))
    par = "chest" if front_leg else "hips"
    bone("leg_" + k + "1", top, knee, par)
    bone("leg_" + k + "2", knee, ankle, "leg_" + k + "1", True)
    bone("leg_" + k + "3", ankle, toe, "leg_" + k + "2", True)
tail_names = []
if len(tailpts) > 20:
    base = Vector((xmid, yB + 0.06 * L, zHip + 0.15 * (tB - zHip)))
    d = np.linalg.norm(tailpts - np.array(base), axis=1)
    order = np.argsort(d); pts = [base]
    for q in (0.33, 0.66, 1.0):
        sel = tailpts[order[int(q * (len(order) - 1) * 0.9):int(q * (len(order) - 1)) + 1]]
        pts.append(Vector(sel.mean(axis=0)))
    par = "hips"
    for i in range(3):
        n = "tail%d" % (i + 1)
        if (pts[i + 1] - pts[i]).length < 1e-3: pts[i + 1] = pts[i] + Vector((0, 0.03 * L, 0))
        bone(n, pts[i], pts[i + 1], par, i > 0); par = n; tail_names.append(n)
bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------- weights
def seg_dist(P, a, b):
    a = np.array(a); b = np.array(b); ab = b - a
    t = np.clip(((P - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)

names = [n for n in J if n != "root"]
FK = list(feet.keys())
FP = np.array([[feet[k].x, feet[k].y] for k in FK])
nearest_foot = np.array(FK)[np.argmin(((V[:, None, :2] - FP[None]) ** 2).sum(-1), axis=1)]
D = np.stack([seg_dist(V, *J[n]) for n in names], axis=1)
for j, n in enumerate(names):
    if n.startswith("leg_"):
        k = n[4:6]
        D[nearest_foot != k, j] = 1e9      # a leg takes only the vertices whose nearest foot (in plan) is its own
        bel, tp = (bF, tF) if k[0] == "F" else (bB, tB)
        # leg bones take only the leg (a little above the belly line), never the flank / dewlap
        D[V[:, 2] > bel + 0.15 * (tp - bel), j] = 1e9
        if not n.endswith("1"): D[V[:, 2] > bel, j] = 1e9
    if n.startswith("tail"):
        D[V[:, 1] < yB, j] = 1e9
w = 1.0 / (D ** 3 + 1e-12)
top2 = np.argsort(-w, axis=1)[:, :3]
groups = {n: ob.vertex_groups.new(name=n) for n in names}
for i in range(len(V)):
    idx = top2[i]; ws = w[i, idx]; t = ws.sum()
    for j, wj in zip(idx, ws):
        if wj / t > 0.02: groups[names[j]].add([i], float(wj / t), "REPLACE")
mod = ob.modifiers.new("Armature", "ARMATURE"); mod.object = rig
ob.parent = rig

# ---------------------------------------------------------------- clips
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="POSE")
pb = rig.pose.bones
for p in pb: p.rotation_mode = "QUATERNION"
LAT = Vector((1, 0, 0))   # the side axis: legs swing about it
UP = Vector((0, 0, 1))

def rot(name, axis, deg):
    """rotation about a world axis, expressed in the bone's local (rest) space"""
    r = rig.data.bones[name].matrix_local.to_3x3()
    q = Quaternion(axis, math.radians(deg))
    pb[name].rotation_quaternion = (r.inverted() @ q.to_matrix() @ r).to_quaternion() if name in pb else None

def reset():
    for p in pb:
        p.rotation_quaternion = (1, 0, 0, 0); p.location = (0, 0, 0)

leg_len = {k: (J["leg_" + k + "1"][0] - J["leg_" + k + "1"][1]).length + (J["leg_" + k + "2"][0] - J["leg_" + k + "2"][1]).length for k in feet}
LEG = sum(leg_len.values()) / 4

def new_action(name, frames):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = act
    act.use_frame_range = True; act.frame_start = 0; act.frame_end = frames; act.use_cyclic = True
    return act

def key(f):
    for p in pb:
        p.keyframe_insert("rotation_quaternion", frame=f)
        p.keyframe_insert("location", frame=f)

def gait(name, T, amp, duty, phases, bob, spine_flex, head_amp):
    n = max(8, round(T * FPS)); new_action(name, n)
    for f in range(n + 1):
        t = f / n; reset()
        for k, ph in phases.items():
            u = (t + ph) % 1.0
            # stance u < duty: the foot sweeps back (+amp -> -amp); swing: comes forward with the knee lifted
            # +angle about X swings the foot BACK (+Y): stance -amp -> +amp, swing back to -amp with the knee folded
            if u < duty: a = -amp + 2 * amp * (u / duty); lift = 0.0
            else:
                v = (u - duty) / (1 - duty); a = amp - 2 * amp * (0.5 - 0.5 * math.cos(math.pi * v)); lift = math.sin(math.pi * v)
            front_leg = k[0] == "F"
            rot("leg_" + k + "1", LAT, a)
            rot("leg_" + k + "2", LAT, (55 if front_leg else 35) * lift)
        pb["hips"].location = (0, 0, 0)
        pb["root"].location = (0, 0, bob * LEG * math.cos(4 * math.pi * t))
        rot("spine", LAT, spine_flex * math.sin(2 * math.pi * t))
        rot("head", LAT, head_amp * math.sin(4 * math.pi * t))
        for i, tn in enumerate(tail_names):
            rot(tn, UP, (6 + 4 * i) * math.sin(2 * math.pi * t - i * 0.6))
        key(f)
    return n

# walk: lateral sequence (left hind, left fore, right hind, right fore); distance per cycle = 2 leg sin(amp) / duty
amp_w, duty_w = 20.0, 0.62
d_walk = 2 * LEG * math.sin(math.radians(amp_w)) / duty_w
T_walk = max(0.5, min(1.4, d_walk / {"cow": 1.0, "dog": 0.95, "cat": 0.57, "monkey": 0.7}[KIND]))
nw = gait("walk", T_walk, amp_w, duty_w, {"BL": 0.0, "FL": 0.25, "BR": 0.5, "FR": 0.75}, 0.015, 2.0, 3.0)
amp_r, duty_r = 34.0, 0.38
nr = gait("run", RUN_T, amp_r, duty_r, {"BL": 0.0, "BR": 0.08, "FL": 0.5, "FR": 0.58}, 0.05, 9.0, 6.0)
d_run = 2 * LEG * math.sin(math.radians(amp_r)) / duty_r
print("CLIPSPEED %s clipWalk %.2f clipRun %.2f (walk T %.2f s, run T %.2f s, leg %.2f m)" % (
    KIND, d_walk / (nw / FPS), d_run / (nr / FPS), nw / FPS, nr / FPS, LEG))

n = 60; new_action("idle", n)
for f in range(n + 1):
    t = f / n; reset()
    rot("chest", LAT, 1.2 * math.sin(2 * math.pi * t))
    rot("neck", LAT, 2.0 * math.sin(2 * math.pi * t + 1))
    rot("head", UP, 6 * math.sin(2 * math.pi * t))
    for i, tn in enumerate(tail_names): rot(tn, UP, 10 * math.sin(2 * math.pi * t - i * 0.7))
    key(f)

def sit_pose(t):
    """sit: the whole body pivots about the front feet until the hips are a quarter of their height off the ground;
    front legs turned back to vertical, hind legs folded forward under the body, head kept level"""
    span = max(yB - yF, 1e-3)
    th = -math.degrees(math.asin(min(0.8, 0.72 * zHip / span)))        # rear down
    P = Vector((xmid, yF, 0.0)); H0 = rig.data.bones["root"].head_local.copy()
    R = Quaternion(LAT, math.radians(th)).to_matrix()
    off = P - H0 - R @ (P - H0)
    rot("root", LAT, th)
    pb["root"].location = rig.data.bones["root"].matrix_local.to_3x3().inverted() @ off
    for k in ("FL", "FR"):
        rot("leg_" + k + "1", LAT, -th)
    for k in ("BL", "BR"):
        rot("leg_" + k + "1", LAT, -40); rot("leg_" + k + "2", LAT, 150); rot("leg_" + k + "3", LAT, -60)
    rot("neck", LAT, -th * 0.7 + 2 * math.sin(2 * math.pi * t))
    for i, tn in enumerate(tail_names): rot(tn, UP, 5 * math.sin(2 * math.pi * t))
    return th

for sp in SPECIALS:
    n = 60 if sp != "lick" else 40; new_action(sp, n)
    for f in range(n + 1):
        t = f / n; reset()
        if sp == "chew":
            rot("neck", LAT, -4 + 1.5 * math.sin(2 * math.pi * t))     # standing, chewing the cud
            rot("head", LAT, 3 * math.sin(6 * math.pi * t)); rot("head", UP, 4 * math.sin(2 * math.pi * t))
            for i, tn in enumerate(tail_names): rot(tn, UP, 14 * math.sin(2 * math.pi * t - i * 0.7))
        elif sp == "sit":
            sit_pose(t)
        elif sp == "lick":
            th = sit_pose(0)
            rot("leg_FL1", LAT, -th - 45); rot("leg_FL2", LAT, 70)
            rot("neck", LAT, -th * 0.7 + 35); rot("head", LAT, 30 + 8 * math.sin(4 * math.pi * t))   # +about X = a forward bone tips DOWN
        key(f)
bpy.ops.object.mode_set(mode="OBJECT")
rig.animation_data.action = bpy.data.actions["idle"]

# ---------------------------------------------------------------- texture out, export
img = None
for m in me.materials:
    if m and m.node_tree:
        for nd in m.node_tree.nodes:
            if nd.type == "TEX_IMAGE" and nd.image:
                img = nd.image; break
    if img: break
if img:
    out_img = bpy.data.images.new(KIND + "_color", *img.size)
    out_img.pixels = img.pixels[:]
    out_img.filepath_raw = os.path.join(OUT, KIND + "_color.png"); out_img.file_format = "PNG"; out_img.save()
    print("TEX", img.size[:], "->", out_img.filepath_raw)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, KIND + ".blend"))
for o in sc.objects: o.select_set(o in (rig, ob))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, KIND + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5, path_mode="STRIP",
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",   # metres in Unity (24 Nov: the default came in 100x)
                         use_armature_deform_only=True, mesh_smooth_type="FACE")
print("FBX", os.path.join(OUT, KIND + ".fbx"), "actions", [a.name for a in bpy.data.actions])

# ---------------------------------------------------------------- check sheet: side + 3/4 views of each clip, magenta world
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 420, 320
w = bpy.data.worlds.new("w"); sc.world = w; w.color = (1, 0, 1)
sc.display.shading.background_type = "WORLD" if hasattr(sc.display.shading, "background_type") else None
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = max(L, H) * 1.25
shots = []
clips = [("idle", 0), ("walk", 0), ("walk", nw // 4), ("walk", nw // 2), ("run", 0), ("run", nr // 3)] + [(sp, 30 if sp != "lick" else 20) for sp in SPECIALS]
views = [("side", Vector((-4 * L, 0, H * 0.5))), ("q34", Vector((-3 * L, -3 * L, H * 1.2)))]
for an, fr in clips:
    rig.animation_data.action = bpy.data.actions[an]
    for vn, pos in views:
        sc.frame_set(fr)
        cam.location = pos
        cam.rotation_euler = (Vector((0, 0, H * 0.45)) - pos).to_track_quat("-Z", "Y").to_euler()
        p = os.path.join(OUT, "_chk_%s_%s_%d.png" % (vn, an, fr)); sc.render.filepath = p
        bpy.ops.render.render(write_still=True); shots.append((p, "%s %s f%d" % (vn, an, fr)))
open(os.path.join(OUT, "_chk_list.txt"), "w").write("\n".join(p + "|" + lab for p, lab in shots))
print("CHECK", len(shots), "frames")

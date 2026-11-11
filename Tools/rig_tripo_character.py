"""
Auto-rig a Tripo A-pose character GLB in Blender (headless) and export a skinned FBX for Unity.

Usage:
  blender -b --python-exit-code 1 -P rig_tripo_character.py -- <in.glb> <out_dir> <name> <height_m> [child]

Writes into <out_dir>:
  <name>.fbx           armature + "Body" (full) + "Body_LOD1" (decimated), both skinned
  <name>.jpg           base-colour texture
  <name>_rig.json      detected joint positions (metres, Unity-style: x right, y up, z forward)
  <name>_check.png     test-pose render (front + side) to eyeball the weights

Bone names follow Unity's Humanoid naming (Hips, Spine, Chest, Neck, Head, LeftShoulder,
LeftUpperArm, LeftLowerArm, LeftHand, LeftUpperLeg, LeftLowerLeg, LeftFoot, LeftToes, ...),
so the FBX also works as a Unity Humanoid avatar.

Joint placement is measured from the mesh, not guessed: the front silhouette is sampled to find
the armpits, the arm line, the hand tips, the crotch, the neck, the leg centres and the toes.
Proportional fallbacks are used only where the silhouette cannot show a joint (e.g. legs inside a
saree), and every fallback is reported in the JSON.
"""
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
IN_GLB, OUT_DIR, NAME, HEIGHT = argv[0], argv[1], argv[2], float(argv[3])
# body type: man | woman | boy | girl | oldman | oldwoman (drives proportional fallbacks)
CATEGORY = next((a for a in argv[4:] if a in ("man", "woman", "boy", "girl", "oldman", "oldwoman")), "man")
CHILD = CATEGORY in ("boy", "girl")
ELDER = CATEGORY.startswith("old")
FEMALE = CATEGORY in ("woman", "girl", "oldwoman")
AUTO_FACING = "autofacing" in argv[4:]   # default: Tripo's fixed export orientation
# proportions used only where the silhouette cannot show a joint (fractions of standing height)
PROP = dict(
    armpit=0.70 if CHILD else (0.71 if ELDER else 0.73),       # elders stand a little stooped
    crotch=0.43 if CHILD else (0.45 if FEMALE else 0.46),
    hip_above_crotch=0.05 if CHILD else (0.06 if FEMALE else 0.055),
    hip_x=0.055 if CHILD else (0.058 if FEMALE else 0.052),     # wider pelvis for women
    shoulder_x=0.10 if (CHILD or FEMALE) else 0.11,             # broader shoulders for men
    shoulder_above_armpit=0.045 if CHILD else 0.05,
    hand=0.10 if (CHILD or FEMALE) else 0.108,
    ankle=0.04 if CHILD else 0.045,
)
os.makedirs(OUT_DIR, exist_ok=True)
notes = []

# ---------------------------------------------------------------- import + normalise
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=IN_GLB)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
assert meshes, "no mesh in " + IN_GLB
for o in bpy.context.scene.objects:
    o.select_set(False)
for m in meshes:
    m.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
if len(meshes) > 1:
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(bpy.context.scene.objects):
    if o != body:
        bpy.data.objects.remove(o, do_unlink=True)
body.name = "Body"
me = body.data
me.name = NAME + "_Body"


def verts():
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


V = verts()
ext = V.max(0) - V.min(0)


def toes_say_front_is_neg_y():
    W = verts()
    zmin_, zmax_ = W[:, 2].min(), W[:, 2].max()
    H0 = zmax_ - zmin_
    feet = W[W[:, 2] < zmin_ + 0.06 * H0]
    torso = W[(W[:, 2] > zmin_ + 0.45 * H0) & (W[:, 2] < zmin_ + 0.75 * H0)]
    yc = np.median(torso[:, 1])
    return (yc - feet[:, 1].min()) >= (feet[:, 1].max() - yc)


if AUTO_FACING:
    # lateral axis = the wider horizontal axis (A-pose arms); make it X, then toes decide front
    if ext[1] > ext[0]:
        me.transform(Matrix.Rotation(math.radians(90), 4, "Z"))
        notes.append("rotated 90deg: model was facing along X")
    if not toes_say_front_is_neg_y():
        me.transform(Matrix.Rotation(math.radians(180), 4, "Z"))
        notes.append("rotated 180deg: toes said the model faced +Y")
else:
    # Tripo exports every character facing glTF -X (Blender -X): turn it to face Blender -Y.
    # (Measured on 12 Tripo models; the toe heuristic got 3 of them backwards, so it only warns.)
    assert ext[1] > ext[0], "not a Tripo-oriented model (arms are not along glTF Z); use autofacing"
    me.transform(Matrix.Rotation(math.radians(-90), 4, "Z"))
    if not toes_say_front_is_neg_y():
        notes.append("warning: toe heuristic disagrees with the Tripo orientation (kept Tripo's)")
V = verts()
zmin, zmax = V[:, 2].min(), V[:, 2].max()
s = HEIGHT / (zmax - zmin)
cx = 0.5 * (V[:, 0].min() + V[:, 0].max())
tors = V[(V[:, 2] > zmin + 0.45 * (zmax - zmin)) & (V[:, 2] < zmin + 0.75 * (zmax - zmin))]
cy = 0.5 * (tors[:, 1].min() + tors[:, 1].max())
me.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -zmin)))
me.update()
V = verts()
H = V[:, 2].max()

# ---------------------------------------------------------------- surface samples + front silhouette
me.calc_loop_triangles()
tri = np.empty(len(me.loop_triangles) * 3, dtype=np.int64)
me.loop_triangles.foreach_get("vertices", tri)
tri = tri.reshape(-1, 3)
A, B, C = V[tri[:, 0]], V[tri[:, 1]], V[tri[:, 2]]
area = 0.5 * np.linalg.norm(np.cross(B - A, C - A), axis=1)
rng = np.random.default_rng(7)
N = 400000
idx = rng.choice(len(tri), size=N, p=area / area.sum())
r1, r2 = rng.random(N), rng.random(N)
flip = r1 + r2 > 1
r1[flip], r2[flip] = 1 - r1[flip], 1 - r2[flip]
P = A[idx] + (B[idx] - A[idx]) * r1[:, None] + (C[idx] - A[idx]) * r2[:, None]

ROWS = 420
pix = H / ROWS
x0 = P[:, 0].min() - 2 * pix
COLS = int((P[:, 0].max() + 2 * pix - x0) / pix) + 1
ci = ((P[:, 0] - x0) / pix).astype(int).clip(0, COLS - 1)
ri = (P[:, 2] / pix).astype(int).clip(0, ROWS - 1)
img = np.zeros((ROWS, COLS), bool)
img[ri, ci] = True
# close 1-px holes
dil = img.copy()
dil[1:, :] |= img[:-1, :]; dil[:-1, :] |= img[1:, :]; dil[:, 1:] |= img[:, :-1]; dil[:, :-1] |= img[:, 1:]
ero = dil.copy()
ero[1:, :] &= dil[:-1, :]; ero[:-1, :] &= dil[1:, :]; ero[:, 1:] &= dil[:, :-1]; ero[:, :-1] &= dil[:, 1:]
img = ero | img


def X(c):
    return x0 + (c + 0.5) * pix


def Z(r):
    return (r + 0.5) * pix


def R(z):
    return int(np.clip(z / pix, 0, ROWS - 1))


def segs(r):
    row = img[r]
    out, c = [], 0
    while c < COLS:
        if row[c]:
            s0 = c
            while c < COLS and row[c]:
                c += 1
            out.append([X(s0) - pix / 2, X(c - 1) + pix / 2])
        else:
            c += 1
    merged = []
    for sgm in out:
        if merged and sgm[0] - merged[-1][1] <= 1.5 * pix:
            merged[-1][1] = sgm[1]
        else:
            merged.append(sgm)
    return merged


def center_seg(r):
    best = None
    for sgm in segs(r):
        if sgm[0] <= 0 <= sgm[1]:
            return sgm
        d = min(abs(sgm[0]), abs(sgm[1]))
        if best is None or d < best[0]:
            best = (d, sgm)
    return best[1] if best else None


def mid_extent(mask, axis=1):
    pts = P[mask]
    if len(pts) < 20:
        return None
    lo, hi = np.percentile(pts[:, axis], [2, 98])
    return 0.5 * (lo + hi)


def depth_at(x, z, wx, wz):
    m = (np.abs(P[:, 0] - x) < wx) & (np.abs(P[:, 2] - z) < wz)
    y = mid_extent(m)
    return 0.0 if y is None else y


# ---------------------------------------------------------------- arms
arm = {}
for side, sg in (("Left", 1), ("Right", -1)):
    # Trace the arm UP from the hand: the hand tip is the most lateral silhouette point in the
    # lower-middle body; follow its segment row by row until it merges into the torso (= armpit).
    # Starting from the hand means hair (bobs, plaits hanging beside the neck) can never be
    # mistaken for an arm, because it is not connected to the hand.
    top, track = None, []
    tip_r, tip_seg = None, None
    for r in range(R(0.25 * H), R(0.58 * H)):   # A-pose hands hang between ~0.4H and ~0.58H
        row = segs(r)
        if not row:
            continue
        q = row[-1] if sg > 0 else row[0]
        if tip_seg is None or (q[1] if sg > 0 else -q[0]) > (tip_seg[1] if sg > 0 else -tip_seg[0]):
            tip_r, tip_seg = r, q
    if tip_seg is not None and not (tip_seg[0] <= 0 <= tip_seg[1]):
        cur = tip_seg
        up = []
        for r in range(tip_r, R(0.90 * H)):
            row = segs(r)
            cands = [q for q in row if q[1] >= cur[0] - 2 * pix and q[0] <= cur[1] + 2 * pix]
            if not cands:
                break
            q = max(cands, key=lambda q: min(q[1], cur[1]) - max(q[0], cur[0]))
            if q[0] <= 0 <= q[1]:                       # merged with the torso: armpit reached
                top = r
                break
            bseg = center_seg(r)
            torso_edge = (bseg[1] if sg > 0 else bseg[0]) if bseg else 0.0
            up.append((Z(r), 0.5 * (q[0] + q[1]), q[1] if sg > 0 else q[0], torso_edge))
            cur = q
        track = up[::-1]                                 # top-down, like the code below expects
        if top is not None and track:
            top = R(track[0][0])
        # an arm hanging close to a loose top only separates from it near the hand: the traced
        # part is then just the hand, not an arm line, so use the shoulder-edge fallback with this tip
        if top is not None and (Z(top) - Z(tip_r) < 0.14 * H or Z(top) < 0.58 * H):
            notes.append(f"{side} arm: gap only near the hand (arm close to body); shoulder from edge")
            top, track = None, []
            known_tip = Vector((tip_seg[1] if sg > 0 else tip_seg[0], 0, Z(tip_r)))
        else:
            known_tip = None
    else:
        known_tip = None
    if top is None or not (0.55 * H < Z(top) < 0.84 * H) or len(track) < 10:
        # no gap between arm and body (shawl / pallu hanging from the arm): the hand tip is still
        # the most lateral point of the silhouette below the shoulders, and the shoulder joint sits
        # just inside the outer edge of the shoulder line
        notes.append(f"{side} arm: no armpit gap (cloth?), using lateral extreme + shoulder edge")
        z_ap = PROP['armpit'] * H
        if known_tip is not None:
            tip = known_tip
        else:
            best_tip = None
            for r in range(R(0.30 * H), R(0.58 * H)):
                sg_row = segs(r)
                if not sg_row:
                    continue
                edge = sg_row[-1][1] if sg > 0 else sg_row[0][0]
                if best_tip is None or edge * sg > best_tip[0] * sg:
                    best_tip = (edge, Z(r))
            tip = Vector((best_tip[0], 0, best_tip[1]))
        z_sh = z_ap + PROP['shoulder_above_armpit'] * H
        row = segs(R(z_sh - 0.02 * H))
        edge = (row[-1][1] if sg > 0 else row[0][0]) if row else sg * 0.14 * H
        sh = Vector((edge - sg * 0.035 * H, 0, z_sh))
        if abs(sh.x) > 0.16 * H or abs(sh.x) < 0.07 * H:
            sh.x = sg * PROP['shoulder_x'] * H
            notes.append(f"{side} arm: shoulder edge implausible, proportional shoulder")
    else:
        z_ap = Z(top)
        tr = np.array([(t[0], t[1]) for t in track])
        outer_edge = np.array([t[2] for t in track])
        k = int(np.argmax(outer_edge * sg))                 # hand tip = most lateral point
        tip = Vector((outer_edge[k], 0, tr[k, 0]))
        upper = tr[: max(6, int(0.75 * (k + 1)))]
        mean = upper.mean(0)
        _, _, vt = np.linalg.svd(upper - mean)
        d = vt[0]                                           # (z, x) direction of the arm
        if d[0] > 0:
            d = -d                                          # point downwards (shoulder -> hand)
        z_sh = z_ap + PROP['shoulder_above_armpit'] * H
        t = (z_sh - mean[0]) / d[0]
        x_sh = mean[1] + t * d[1]
        torso_edge = track[0][3]
        x_sh = float(np.clip(x_sh * sg, abs(torso_edge) - 0.035 * H, abs(torso_edge) + 0.015 * H)) * sg
        sh = Vector((x_sh, 0, z_sh))
    hand_len = PROP['hand'] * H
    dirv = (tip - sh).normalized()
    wrist = tip - dirv * hand_len
    elbow = sh + (wrist - sh) * 0.52
    for nm, p, w in (("sh", sh, 0.03), ("el", elbow, 0.025), ("wr", wrist, 0.02), ("tip", tip, 0.02)):
        p.y = depth_at(p.x, p.z, w * H, 0.012 * H)
    arm[side] = dict(armpit=z_ap, sh=sh, el=elbow, wr=wrist, tip=tip)

# ---------------------------------------------------------------- legs
arm_x = min(abs(arm["Left"]["wr"].x), abs(arm["Right"]["wr"].x))
crotch = None
for r in range(R(0.56 * H), R(0.25 * H), -1):
    legs = [q for q in segs(r) if abs(0.5 * (q[0] + q[1])) < 0.13 * H and q[1] - q[0] < 0.6 * arm_x * 2]
    if len(legs) >= 2 and legs[0][1] < 0.02 * H and legs[-1][0] > -0.02 * H:
        crotch = Z(r)
        break
if crotch is None or crotch < 0.33 * H:
    notes.append("legs: no crotch gap in silhouette (dress/saree?), proportional hips")
    crotch = PROP['crotch'] * H
z_hip = crotch + PROP['hip_above_crotch'] * H


def leg_x(z, sg, fallback):
    legs = [q for q in segs(R(z)) if abs(0.5 * (q[0] + q[1])) < 0.13 * H]
    side = [q for q in legs if 0.5 * (q[0] + q[1]) * sg > 0.01 * H]
    if len(legs) >= 2 and side:
        q = min(side, key=lambda q: abs(0.5 * (q[0] + q[1])))
        return 0.5 * (q[0] + q[1])
    return fallback


leg = {}
for side, sg in (("Left", 1), ("Right", -1)):
    xh = leg_x(crotch - 0.04 * H, sg, sg * PROP['hip_x'] * H)
    xh = sg * float(np.clip(abs(xh) * 0.9, 0.035 * H, 0.075 * H))
    z_ank = PROP['ankle'] * H
    xa = leg_x(0.12 * H, sg, xh)
    z_kn = z_ank + 0.48 * (z_hip - z_ank)
    xk = leg_x(z_kn, sg, 0.5 * (xh + xa))
    hip = Vector((xh, 0, z_hip))
    knee = Vector((xk, 0, z_kn))
    ank = Vector((xa, 0, z_ank))
    hip.y = depth_at(hip.x, hip.z, 0.04 * H, 0.015 * H)
    knee.y = depth_at(knee.x, knee.z, 0.035 * H, 0.012 * H)
    ank.y = depth_at(ank.x, 0.075 * H, 0.03 * H, 0.015 * H)
    fm = (P[:, 2] < 0.05 * H) & (P[:, 0] * sg > 0.005 * H)
    fp = P[fm]
    if len(fp) > 30:
        tp = fp[np.argmin(fp[:, 1])]
        toe = Vector((tp[0], tp[1], 0.015 * H))
    else:
        toe = Vector((xa, ank.y - 0.14 * H, 0.015 * H))
        notes.append(f"{side} foot: toe not found")
    ball = ank.lerp(toe, 0.72)
    ball.z = 0.018 * H
    leg[side] = dict(hip=hip, knee=knee, ank=ank, ball=ball, toe=toe)

# ---------------------------------------------------------------- spine, neck, head
z_sh = 0.5 * (arm["Left"]["sh"].z + arm["Right"]["sh"].z)
best = None
for r in range(R(z_sh + 0.03 * H), R(H - 0.06 * H)):
    cs = center_seg(r)
    if cs is None:
        continue
    w = cs[1] - cs[0]
    if best is None or w < best[0]:
        best = (w, Z(r))
neck_mid = best[1] if best else z_sh + 0.06 * H
z_nb = z_sh + 0.005 * H
z_hb = min(neck_mid + 0.02 * H, H - 0.10 * H)
z_s1 = z_hip + 0.075 * H
z_s2 = z_s1 + 0.45 * (z_nb - z_s1)


def spine_pt(z):
    return Vector((0, depth_at(0, z, 0.06 * H, 0.01 * H), z))


hips_p, s1_p, s2_p, nb_p = spine_pt(z_hip), spine_pt(z_s1), spine_pt(z_s2), spine_pt(z_nb)
hb_p = Vector((0, depth_at(0, z_hb, 0.03 * H, 0.008 * H), z_hb))
top_p = Vector((0, hb_p.y, H))

# ---------------------------------------------------------------- armature
arm_data = bpy.data.armatures.new(NAME + "_Armature")
arm_obj = bpy.data.objects.new("Armature", arm_data)
bpy.context.scene.collection.objects.link(arm_obj)
bpy.context.view_layer.objects.active = arm_obj
bpy.ops.object.mode_set(mode="EDIT")
eb = arm_data.edit_bones


def bone(name, head, tail, parent=None, connect=False):
    b = eb.new(name)
    b.head, b.tail = head, tail
    if parent:
        b.parent = eb[parent]
        b.use_connect = connect
    return b


bone("Hips", hips_p, s1_p)
bone("Spine", s1_p, s2_p, "Hips", True)
bone("Chest", s2_p, nb_p, "Spine", True)
bone("Neck", nb_p, hb_p, "Chest", True)
bone("Head", hb_p, top_p, "Neck", True)
for side in ("Left", "Right"):
    a, l = arm[side], leg[side]
    clav = Vector((a["sh"].x * 0.18, nb_p.y, z_nb - 0.012 * H))
    bone(side + "Shoulder", clav, a["sh"], "Chest")
    bone(side + "UpperArm", a["sh"], a["el"], side + "Shoulder", True)
    bone(side + "LowerArm", a["el"], a["wr"], side + "UpperArm", True)
    bone(side + "Hand", a["wr"], a["tip"], side + "LowerArm", True)
    bone(side + "UpperLeg", l["hip"], l["knee"], "Hips")
    bone(side + "LowerLeg", l["knee"], l["ank"], side + "UpperLeg", True)
    bone(side + "Foot", l["ank"], l["ball"], side + "LowerLeg", True)
    bone(side + "Toes", l["ball"], l["toe"], side + "Foot", True)
# consistent rolls: every bone's local X points to the character's left (+X in Blender)
ROLL = {"GLOBAL_NEG_Y": ["Hips", "Spine", "Chest", "Neck", "Head", "LeftShoulder", "RightShoulder",
                         "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand"],
        "GLOBAL_POS_Y": ["LeftUpperLeg", "LeftLowerLeg", "RightUpperLeg", "RightLowerLeg"],
        "GLOBAL_NEG_Z": ["LeftFoot", "LeftToes", "RightFoot", "RightToes"]}
for typ, names in ROLL.items():
    bpy.ops.armature.select_all(action="DESELECT")
    for nm in names:
        eb[nm].select = eb[nm].select_head = eb[nm].select_tail = True
    bpy.ops.armature.calculate_roll(type=typ)
bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------- skin weights
# glTF splits vertices along UV seams, so the imported mesh is many open islands and Blender's
# bone-heat solver often fails on it. Weights are therefore solved on a seam-welded copy (then a
# voxel-remeshed proxy, then plain bone distance as a last resort) and copied back by position.
import bmesh
from mathutils.kdtree import KDTree

BONES = [b.name for b in arm_data.bones]


def make_copy(src, name):
    o = src.copy()
    o.data = src.data.copy()
    o.name = name
    for g in list(o.vertex_groups):
        o.vertex_groups.remove(g)
    o.modifiers.clear()
    bpy.context.scene.collection.objects.link(o)
    return o


def heat(obj):
    """Bone heat on obj; returns (N x bones) weight matrix."""
    for o in bpy.context.scene.objects:
        o.select_set(False)
    obj.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    W = np.zeros((len(obj.data.vertices), len(BONES)))
    col = {vg.index: BONES.index(vg.name) for vg in obj.vertex_groups if vg.name in BONES}
    for v in obj.data.vertices:
        for g in v.groups:
            if g.group in col:
                W[v.index, col[g.group]] = g.weight
    return W


def coords(obj):
    c = np.empty(len(obj.data.vertices) * 3)
    obj.data.vertices.foreach_get("co", c)
    return c.reshape(-1, 3)


def transfer(src_co, src_W, dst_co, k=1):
    ok = src_W.sum(1) > 1e-4
    src_co, src_W = src_co[ok], src_W[ok]
    if len(src_co) == 0:                      # solver produced nothing: report all as unweighted
        return np.zeros((len(dst_co), src_W.shape[1]))
    k = min(k, len(src_co))
    kd = KDTree(len(src_co))
    for i, c in enumerate(src_co):
        kd.insert(c, i)
    kd.balance()
    out = np.zeros((len(dst_co), src_W.shape[1]))
    for i, c in enumerate(dst_co):
        hits = kd.find_n(c, k)
        ws = np.array([1.0 / (h[2] + 1e-5) for h in hits])
        out[i] = (src_W[[h[1] for h in hits]] * ws[:, None]).sum(0) / ws.sum()
    return out


def geometric(co):
    """Last resort: inverse-distance to bone segments, with left/right and arm/torso separation."""
    D = np.full((len(co), len(BONES)), np.inf)
    for j, b in enumerate(arm_data.bones):
        if b.name.endswith("Toes") or b.name.endswith("Shoulder"):
            continue
        a, t = np.array(b.head_local), np.array(b.tail_local)
        ab = t - a
        u = np.clip(((co - a) @ ab) / (ab @ ab), 0, 1)
        d = np.linalg.norm(co - (a + u[:, None] * ab), axis=1)
        if b.name.startswith("Left"):
            d[co[:, 0] < -0.01 * H] = np.inf
        if b.name.startswith("Right"):
            d[co[:, 0] > 0.01 * H] = np.inf
        if "Arm" in b.name or "Hand" in b.name:
            side = 1 if b.name.startswith("Left") else -1
            d[co[:, 0] * side < abs(arm["Left" if side > 0 else "Right"]["sh"].x) * 0.6] = np.inf
        D[:, j] = d
    W = 1.0 / (D + 0.01 * H) ** 4
    W[~np.isfinite(D)] = 0
    top = np.argsort(-W, axis=1)[:, 3:]
    np.put_along_axis(W, top, 0, axis=1)
    return W


def bad_count(W):
    return int((~(np.nan_to_num(W, nan=0.0).sum(1) > 1e-4)).sum())


body_co = coords(body)
method = "bone heat on seam-welded mesh"
weld = make_copy(body, "Weld")
bm = bmesh.new()
bm.from_mesh(weld.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5 * H)
bm.to_mesh(weld.data)
bm.free()
Wb = transfer(coords(weld), heat(weld), body_co)
bpy.data.objects.remove(weld, do_unlink=True)
if bad_count(Wb) > 0.005 * len(body_co):
    method = "bone heat on voxel proxy"
    notes.append(f"welded bone heat left {bad_count(Wb)} verts unweighted; trying voxel proxy")
    proxy = make_copy(body, "Proxy")
    rm = proxy.modifiers.new("Remesh", "REMESH")
    rm.mode = "VOXEL"
    rm.voxel_size = 0.01 * H
    for o in bpy.context.scene.objects:
        o.select_set(o == proxy)
    bpy.context.view_layer.objects.active = proxy
    bpy.ops.object.modifier_apply(modifier="Remesh")
    Wb = transfer(coords(proxy), heat(proxy), body_co, k=3)
    bpy.data.objects.remove(proxy, do_unlink=True)
if bad_count(Wb) > 0.05 * len(body_co):
    method = "bone distance (bone heat failed)"
    notes.append(f"proxy bone heat left {bad_count(Wb)} verts unweighted; using bone-distance weights")
    Wb = geometric(body_co)


def seg_dist(P_, a, b):
    ab = b - a
    u = np.clip(((P_ - a) @ ab) / max(ab @ ab, 1e-12), 0, 1)
    return np.linalg.norm(P_ - (a + u[:, None] * ab), axis=1)


def clean_limb_islands(W, co):
    """Bone heat lets a bone 'see' nearby but unconnected surfaces (a skirt next to the hand, a hand
    resting on a thigh), which then stretch when that limb moves. Keep each limb's weights only on
    vertices connected over the mesh to that limb: flood-fill along edges from vertices hugging the
    limb's bones, through vertices that carry that limb's weight; strip it everywhere else."""
    key = np.round(co / (1e-5 * H)).astype(np.int64)            # weld UV-seam duplicates
    _, wid = np.unique(key, axis=0, return_inverse=True)
    wid = wid.ravel()
    nW = int(wid.max()) + 1
    ed = np.empty(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", ed)
    ed = wid[ed.reshape(-1, 2)]
    a = np.concatenate([ed[:, 0], ed[:, 1]])
    b = np.concatenate([ed[:, 1], ed[:, 0]])
    order = np.argsort(a, kind="stable")
    a, b = a[order], b[order]
    start = np.searchsorted(a, np.arange(nW + 1))
    wco = np.zeros((nW, 3))
    wco[wid] = co
    stripped = {}
    limbs = []
    for side in ("Left", "Right"):
        limbs.append((side + " arm", [side + n for n in ("UpperArm", "LowerArm", "Hand")]))
        limbs.append((side + " leg", [side + n for n in ("UpperLeg", "LowerLeg", "Foot", "Toes")]))
    for label, names in limbs:
        cols = [BONES.index(n) for n in names]
        lw_v = W[:, cols].sum(1)
        lw = np.zeros(nW)
        np.maximum.at(lw, wid, lw_v)
        d = np.min([seg_dist(wco, np.array(arm_data.bones[n].head_local), np.array(arm_data.bones[n].tail_local))
                    for n in names], axis=0)
        seed = np.nonzero((lw > 0.5) & (d < 0.06 * H))[0]
        if len(seed) == 0:
            continue
        reached = np.zeros(nW, bool)
        reached[seed] = True
        stack = list(seed)
        while stack:
            v = stack.pop()
            for u in b[start[v]:start[v + 1]]:
                if not reached[u] and lw[u] > 0.02:
                    reached[u] = True
                    stack.append(u)
        strip = (~reached[wid]) & (lw_v > 1e-4)
        if strip.any():
            W[np.ix_(strip, cols)] = 0
            stripped[label] = int(strip.sum())
    s = W.sum(1, keepdims=True)
    W = np.where(s > 1e-6, W / np.maximum(s, 1e-6), 0)
    return W, stripped


Wb = np.nan_to_num(Wb, nan=0.0)
Wb, stripped = clean_limb_islands(Wb, body_co)
if stripped:
    notes.append("unconnected limb weights removed: " + ", ".join(f"{k} {v}" for k, v in stripped.items()))
filled = bad_count(Wb)
if filled:
    Wb = np.where((Wb.sum(1) < 1e-4)[:, None], transfer(body_co, Wb, body_co, k=4), Wb)
    notes.append(f"{filled} stray verts took their nearest neighbours' weights")
notes.append("weights: " + method)

for g in list(body.vertex_groups):
    body.vertex_groups.remove(g)
body.modifiers.clear()
groups = [body.vertex_groups.new(name=n) for n in BONES]
for j, g in enumerate(groups):
    idx = np.nonzero(Wb[:, j] > 1e-3)[0]
    for i in idx:
        g.add([int(i)], float(Wb[i, j]), "REPLACE")
body.parent = arm_obj
am = body.modifiers.new("Armature", "ARMATURE")
am.object = arm_obj
bad = bad_count(Wb)

bpy.context.view_layer.objects.active = body
for o in bpy.context.scene.objects:
    o.select_set(o == body)
bpy.ops.object.vertex_group_limit_total(limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)

# ---------------------------------------------------------------- LOD1
lod = body.copy()
lod.data = body.data.copy()
lod.name = "Body_Low"          # not "_LOD1": Unity's FBX importer would treat it as an LOD set
lod.data.name = NAME + "_Body_Low"
bpy.context.scene.collection.objects.link(lod)
dec = lod.modifiers.new("Decimate", "DECIMATE")
dec.ratio = 0.3
bpy.context.view_layer.objects.active = lod
for o in bpy.context.scene.objects:
    o.select_set(o == lod)
bpy.ops.object.modifier_move_to_index(modifier="Decimate", index=0)
bpy.ops.object.modifier_apply(modifier="Decimate")

# ---------------------------------------------------------------- T-pose rest
# Unity's Humanoid avatar (and the Mixamo walk/run clips from G.O.A.N.) expect a T-pose bind pose.
# Swing each arm chain out to horizontal, bake that deformation into both meshes, and make it the
# new rest pose (weights are unchanged).
def to_tpose():
    for o in bpy.context.scene.objects:
        o.select_set(o == arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="POSE")
    for side, sg in (("Left", 1), ("Right", -1)):
        target = Vector((sg, 0, 0))
        for bn in ("UpperArm", "LowerArm", "Hand"):
            bpy.context.view_layer.update()
            pb = arm_obj.pose.bones[side + bn]
            cur = pb.tail - pb.head
            if cur.length < 1e-6:
                continue
            q = cur.normalized().rotation_difference(target)
            head = pb.head.copy()
            pb.matrix = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")
    for ob in (body, lod):
        for o in bpy.context.scene.objects:
            o.select_set(o == ob)
        bpy.context.view_layer.objects.active = ob
        mod = next(m for m in ob.modifiers if m.type == "ARMATURE")
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for o in bpy.context.scene.objects:
        o.select_set(o == arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for ob in (body, lod):
        m = ob.modifiers.new("Armature", "ARMATURE")
        m.object = arm_obj


to_tpose()

# ---------------------------------------------------------------- texture
tex_path = os.path.join(OUT_DIR, NAME + ".jpg")
imgs = [i for i in bpy.data.images if i.size[0] > 0]
if imgs:
    im = max(imgs, key=lambda i: i.size[0] * i.size[1])
    bpy.context.scene.render.image_settings.file_format = "JPEG"
    bpy.context.scene.render.image_settings.quality = 92
    im.save_render(tex_path)

# ---------------------------------------------------------------- check render (test pose)
def render_check(path):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x, sc.render.resolution_y = 600, 860
    sc.render.film_transparent = False
    sc.world = bpy.data.worlds.new("W")
    sc.render.image_settings.file_format = "PNG"
    lod.hide_render = True
    pb = arm_obj.pose.bones
    # world-axis test pose (Blender: character faces -Y, its left is +X, up is +Z):
    # left arm lowered to the side, right arm raised forward with a bent elbow,
    # left leg stepping forward with a bent knee, head turned
    pose = {"LeftUpperArm": (0, 40, 0), "RightUpperArm": (-60, 0, 0), "RightLowerArm": (-70, 0, 0),
            "LeftUpperLeg": (-40, 0, 0), "LeftLowerLeg": (55, 0, 0), "Head": (0, 0, 25)}
    for nm, e in pose.items():
        b = pb[nm]
        M = arm_data.bones[nm].matrix_local.to_3x3()
        Rw = Matrix.Rotation(math.radians(e[0]), 3, "X") @ Matrix.Rotation(math.radians(e[1]), 3, "Y") @ Matrix.Rotation(math.radians(e[2]), 3, "Z")
        b.rotation_mode = "QUATERNION"
        b.rotation_quaternion = (M.inverted() @ Rw @ M).to_quaternion()
    cam_data = bpy.data.cameras.new("C")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = H * 1.12
    cam = bpy.data.objects.new("C", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    shots = []
    for i, (loc, rot) in enumerate((((0, -6, H / 2), (90, 0, 0)), ((6, 0, H / 2), (90, 0, 90)))):
        cam.location = loc
        cam.rotation_euler = [math.radians(v) for v in rot]
        p = path.replace(".png", f"_{i}.png")
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
        shots.append(p)
    for nm in pose:
        pb[nm].rotation_quaternion = (1, 0, 0, 0)
    bpy.data.objects.remove(cam, do_unlink=True)
    lod.hide_render = False
    return shots


shots = render_check(os.path.join(OUT_DIR, NAME + "_check.png"))

# ---------------------------------------------------------------- export
for o in bpy.context.scene.objects:
    o.select_set(o in (arm_obj, body, lod))
bpy.context.view_layer.objects.active = arm_obj
fbx = os.path.join(OUT_DIR, NAME + ".fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx, use_selection=True, object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=True,
    mesh_smooth_type="FACE", add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
    armature_nodetype="NULL", bake_anim=False, path_mode="STRIP", embed_textures=False,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", use_armature_deform_only=True)


def u(v):  # Blender (x right, -y forward, z up) -> Unity-style (x right, y up, z forward), character's left = -x
    return [round(-v.x, 4), round(v.z, 4), round(-v.y, 4)]


info = dict(name=NAME, category=CATEGORY, height=round(H, 4), child=CHILD, elder=ELDER, female=FEMALE, verts=len(me.vertices), tris=len(me.loop_triangles),
            lod1_tris=sum(len(p.vertices) - 2 for p in lod.data.polygons), unweighted=bad, notes=notes,
            armpit=[round(arm[s]["armpit"], 4) for s in ("Left", "Right")], crotch=round(crotch, 4),
            joints={b.name: u(b.head_local) for b in arm_data.bones}, shots=shots, fbx=fbx, texture=tex_path)
with open(os.path.join(OUT_DIR, NAME + "_rig.json"), "w") as f:
    json.dump(info, f, indent=1)
print("RIG_OK", json.dumps({k: info[k] for k in ("name", "height", "verts", "tris", "lod1_tris", "unweighted", "notes")}))

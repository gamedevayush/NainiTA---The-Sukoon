"""
Which part of the body each texel of a rigged Tripo character's texture belongs to (shops, 22 Nov 2026: outfits).

Blender, headless:
  blender -b --factory-startup --python-exit-code 1 -P Tools/outfits/outfit_regions.py -- <model.fbx> <out.npz> [res]

The skin weights say it: every vertex's weight per body part (head, hands, feet, legs, hips, torso + arms) is
interpolated over each triangle in UV space, so a texel gets the part its surface belongs to. make_outfits.py then
recolours only the clothes on the torso / arms / hips / legs and leaves the face, hair and hands as they are.

Writes <out.npz>: part (res x res uint8; 0 = no surface, 1 head, 2 hands, 3 feet, 4 legs, 5 hips, 6 torso) and
z (the texel's height on the body in the rest pose, 0 = soles, 1 = top of the hair), image row 0 = the texture's top.
"""
import bpy, sys
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
FBX, OUT = argv[0], argv[1]
RES = int(argv[2]) if len(argv) > 2 else 1024

PARTS = ["none", "head", "hands", "feet", "legs", "hips", "torso"]


def part_of(group):
    g = group.lower().replace("mixamorig:", "")
    if any(k in g for k in ("head", "neck", "eye", "jaw")):
        return 1
    if any(k in g for k in ("hand", "thumb", "index", "middle", "ring", "little", "pinky", "finger")):
        return 2
    if any(k in g for k in ("foot", "toe")):
        return 3
    if any(k in g for k in ("upperleg", "lowerleg", "upleg", "leg", "knee", "thigh", "calf", "shin")):
        return 4
    if g in ("hips", "pelvis"):
        return 5
    return 6   # spine, chest, shoulders, arms


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
body = next((o for o in meshes if o.name == "Body"), None) or max(meshes, key=lambda o: len(o.data.vertices))
me = body.data
print(f"[regions] {body.name}: {len(me.vertices)} verts, groups {[g.name for g in body.vertex_groups]}")

gpart = {g.index: part_of(g.name) for g in body.vertex_groups}
W = np.zeros((len(me.vertices), len(PARTS)), np.float32)
for v in me.vertices:
    for ge in v.groups:
        W[v.index, gpart.get(ge.group, 6)] += ge.weight
s = W.sum(1, keepdims=True)
W = np.where(s > 1e-6, W / np.maximum(s, 1e-6), 0)

me.calc_loop_triangles()
uv = me.uv_layers.active.data
acc = np.zeros((RES, RES, len(PARTS)), np.float32)
cov = np.zeros((RES, RES), np.float32)
zacc = np.zeros((RES, RES), np.float32)
co = np.array([(body.matrix_world @ v.co).z for v in me.vertices], np.float32)
Z = (co - co.min()) / max(float(co.max() - co.min()), 1e-6)   # 0 at the soles, 1 at the top of the hair
for tri in me.loop_triangles:
    P = np.array([[uv[l].uv[0] * RES, (1.0 - uv[l].uv[1]) * RES] for l in tri.loops], np.float64)
    Wt = W[list(tri.vertices)]
    Zt = Z[list(tri.vertices)]
    x0, y0 = np.floor(P.min(0)).astype(int)
    x1, y1 = np.ceil(P.max(0)).astype(int)
    x0, y0 = max(x0, 0), max(y0, 0)
    x1, y1 = min(x1, RES - 1), min(y1, RES - 1)
    if x1 < x0 or y1 < y0:
        continue
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, ay), (bx, by), (cx, cy) = P
    d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(d) < 1e-12:
        continue
    l0 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
    l1 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
    l2 = 1.0 - l0 - l1
    m = (l0 >= -0.02) & (l1 >= -0.02) & (l2 >= -0.02)   # a hair over the edge: no pin holes between triangles
    if not m.any():
        continue
    w = l0[..., None] * Wt[0] + l1[..., None] * Wt[1] + l2[..., None] * Wt[2]
    acc[y0:y1 + 1, x0:x1 + 1][m] += w[m]
    cov[y0:y1 + 1, x0:x1 + 1][m] += 1.0
    zacc[y0:y1 + 1, x0:x1 + 1][m] += (l0 * Zt[0] + l1 * Zt[1] + l2 * Zt[2])[m]

part = np.where(cov > 0, acc.argmax(2), 0).astype(np.uint8)
z01 = np.where(cov > 0, zacc / np.maximum(cov, 1), 0).astype(np.float16)
np.savez_compressed(OUT, part=part, z=z01)
counts = {PARTS[i]: int((part == i).sum()) for i in range(len(PARTS))}
print(f"[regions] {RES}x{RES} -> {OUT}: {counts}")

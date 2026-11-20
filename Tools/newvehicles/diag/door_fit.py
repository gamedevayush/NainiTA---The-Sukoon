"""Where does a bus door's mesh belong? Match its open edges against its Body's vertices over a wide 3D grid
(coarse, then fine) and print the best shifts. blender -b <blend> -P door_fit.py -- <door object> <frame>"""
import bpy, bmesh, sys
from mathutils import Vector
from mathutils.kdtree import KDTree

args = sys.argv[sys.argv.index("--") + 1:]
door = bpy.data.objects[args[0]]
frame = int(args[1])
sc = bpy.context.scene
sc.frame_set(frame)
col = door.users_collection[0]
body = next(o for o in col.all_objects if o.type == "MESH" and o.name.split(".")[0] == "Body")
Mb, Md = body.matrix_world, door.matrix_world
bw = [Mb @ v.co for v in body.data.vertices]
kd = KDTree(len(bw))
for i, p in enumerate(bw):
    kd.insert(p, i)
kd.balance()
bm = bmesh.new()
bm.from_mesh(door.data)
edge = [Md @ v.co for v in bm.verts if any(e.is_boundary for e in v.link_edges)]
allv = [Md @ v.co for v in bm.verts]
print("door", door.name, "verts", len(allv), "edge verts", len(edge))
xs = [p.x for p in allv]; ys = [p.y for p in allv]; zs = [p.z for p in allv]
print("door world box x %.3f..%.3f y %.3f..%.3f z %.3f..%.3f" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))
bx = [p.x for p in bw]; by = [p.y for p in bw]
print("body world box x %.3f..%.3f y %.3f..%.3f" % (min(bx), max(bx), min(by), max(by)))
sample = edge[::max(1, len(edge) // 80)]


def score(t, cap=0.15):
    return sum(min(kd.find(p + t)[2], cap) for p in sample) / len(sample)


res = []
for ix in range(-30, 31):
    for iy in range(-30, 31):
        for iz in range(-4, 5):
            t = Vector((ix * 0.05, iy * 0.05, iz * 0.05))
            res.append((score(t), t))
res.sort(key=lambda r: r[0])
print("coarse best:")
for s, t in res[:8]:
    print("  %.4f  (%.2f, %.2f, %.2f)" % (s, t.x, t.y, t.z))
s0, t0 = res[0]
best = (s0, t0)
for ix in range(-6, 7):
    for iy in range(-6, 7):
        for iz in range(-6, 7):
            t = t0 + Vector((ix, iy, iz)) * 0.01
            s = score(t)
            if s < best[0]:
                best = (s, t)
t = best[1]
within = sum(1 for p in edge if kd.find(p + t)[2] < 0.01) / len(edge)
print("FIT %.4f at (%.3f, %.3f, %.3f), %.0f%% of edge within 1 cm, zero-shift score %.4f" % (best[0], t.x, t.y, t.z, within * 100, score(Vector())))

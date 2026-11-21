"""Where is the windscreen? Rays from 3 m in front of the vehicle straight back along it (Unity -z) at a grid of x / y,
against every mesh of the vehicle (BVH): the first hits' Unity position, normal and object. Read-only.
  blender -b NewVehicles_v06.blend --python-exit-code 1 -P diag/glass_rays.py -- tuktuk_auto
"""
import bpy, sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree

name = sys.argv[sys.argv.index("--") + 1]
col = next(c for c in bpy.data.collections if c.name[3:] == name)
objs = [o for o in col.all_objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
U = lambda w: Vector((-(w.x - cx), w.z, -(w.y - cy)))
B = lambda u: Vector((cx - u.x, cy - u.z, u.y))
Un = lambda n: Vector((-n.x, n.z, -n.y))
dg = bpy.context.evaluated_depsgraph_get()
trees = []
for o in objs:
    bm_ = __import__("bmesh").new(); bm_.from_mesh(o.data); bm_.transform(o.matrix_world)
    trees.append((o.name, BVHTree.FromBMesh(bm_), bm_))
d = Vector((0, 1, 0))   # Unity -z = Blender +Y
for y in (1.15, 1.25, 1.35, 1.45, 1.55):
    row = []
    for x in (-0.35, 0.0, 0.35):
        o = B(Vector((x, y, 3.0)))
        hits = []
        for nm, t, _ in trees:
            loc = o.copy(); left = 6.0
            for _ in range(6):
                h = t.ray_cast(loc, d, left)
                if h[0] is None: break
                hits.append((U(h[0]).z, Un(h[1]).normalized(), nm, h[2]))
                loc = h[0] + d * 0.002; left -= (h[0] - o).length
        hits.sort(key=lambda t: -t[0])
        row.append("x%+.2f: " % x + " | ".join("z%.2f n(%.1f,%.1f,%.1f) %s#%d" % (z, n.x, n.y, n.z, nm.split('.')[0], fi) for z, n, nm, fi in hits[:3]))
    print("y %.2f  " % y + "   ".join(row))

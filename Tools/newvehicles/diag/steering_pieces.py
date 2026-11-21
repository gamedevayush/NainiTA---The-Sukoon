"""Is each car's / bus's steering wheel a loose piece of its Body? (21 Nov 2026: a Steering object only where it can be
taken out cleanly - no cutting.) For each vehicle and its driver's wrists (the setup's handL / handR, Unity vehicle
space, passed as name:lx,ly,lz,rx,ry,rz), lists the Body's loose pieces (connected vertex sets) that lie wholly within
0.45 m of the rim's centre (the wrists' middle, 5 cm down): their size and vertex count. Read-only.
  blender -b NewVehicles_v06.blend --python-exit-code 1 -P diag/steering_pieces.py -- car_hatch_small:0.26,0.94,0.46,0.50,0.95,0.46 ...
"""
import bpy, bmesh, sys
from mathutils import Vector

jobs = {}
for a in sys.argv[sys.argv.index("--") + 1:]:
    n, v = a.split(":")
    f = [float(x) for x in v.split(",")]
    jobs[n] = (Vector(f[0:3]), Vector(f[3:6]))
for col in bpy.data.collections:
    if len(col.name) < 4 or col.name[3:] not in jobs:
        continue
    objs = [o for o in col.all_objects if o.type == "MESH"]
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
    cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
    to_b = lambda u: Vector((cx - u.x, cy - u.z, u.y))          # Unity vehicle space -> Blender world
    l, r = jobs[col.name[3:]]
    c = to_b((l + r) / 2 - Vector((0, 0.05, 0)))
    body = next(o for o in objs if o.name.split(".")[0] in ("Body", col.name))
    mw = body.matrix_world
    bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
    seen, near = set(), []
    for v in bm.verts:
        if v.index in seen: continue
        stack, idx = [v], []
        seen.add(v.index)
        while stack:
            a = stack.pop(); idx.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen: seen.add(b.index); stack.append(b)
        ws = [mw @ x.co for x in idx]
        if max((w - c).length for w in ws) < 0.45:
            lo = Vector((min(w.x for w in ws), min(w.y for w in ws), min(w.z for w in ws)))
            hi = Vector((max(w.x for w in ws), max(w.y for w in ws), max(w.z for w in ws)))
            near.append((len(idx), hi - lo))
    total = len(bm.verts); bm.free()
    print("== %s: body %d verts, pieces wholly within 0.45 m of the rim centre: %d" % (col.name, total, len(near)))
    for n, s in sorted(near, key=lambda t: -t[0])[:6]:
        print("   %5d verts, size x %.2f y %.2f z %.2f" % (n, s.x, s.y, s.z))

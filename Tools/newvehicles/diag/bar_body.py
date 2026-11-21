"""Is the missing right half of the auto's / e-rickshaw's handlebar inside the Body mesh (item 6)? Counts the Body's
vertices in the handlebar's upper (grip) region on the left and in its mirror on the right, in 2 cm slices of x.
blender -b NewVehicles_v06.blend -P diag/bar_body.py -- tuktuk_auto tirri_erickshaw"""
import bpy, sys
from mathutils import Vector

names = sys.argv[sys.argv.index("--") + 1:]
for col in bpy.data.collections:
    if len(col.name) < 4 or col.name[3:] not in names:
        continue
    objs = [o for o in col.all_objects if o.type == "MESH"]
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
    cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
    bar = next(o for o in objs if o.name.startswith("Handle"))
    body = next(o for o in objs if o.name.startswith("Body"))
    U = lambda w: (-(w.x - cx), w.z, -(w.y - cy))
    bv = [U(bar.matrix_world @ v.co) for v in bar.data.vertices]
    top = [p for p in bv if p[1] > 0.9]
    y0, y1 = min(p[1] for p in top) - 0.03, max(p[1] for p in top) + 0.03
    z0, z1 = min(p[2] for p in top) - 0.03, max(p[2] for p in top) + 0.03
    dv = [U(body.matrix_world @ v.co) for v in body.data.vertices]
    print("==", col.name, "bar top region y %.2f..%.2f z %.2f..%.2f, bar verts there %d" % (y0, y1, z0, z1, len(top)))
    for k in range(-22, 22):
        a, b = k * 0.02, k * 0.02 + 0.02
        nb = sum(1 for p in top if a <= p[0] < b)
        nd = sum(1 for p in dv if a <= p[0] < b and y0 <= p[1] <= y1 and z0 <= p[2] <= z1)
        if nb or nd:
            print("  x %+.2f..%+.2f  bar %4d  body %4d" % (a, b, nb, nd))

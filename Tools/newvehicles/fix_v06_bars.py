"""The auto's and the e-rickshaw's steering sat LEFT of the centre (docs/VEHICLES.md section 0, item 6, 21 Nov 2026).
Tripo gave both a small round steering wheel on a column, wholly on the left (Unity x -0.40..-0.10, y 0.93-1.15),
while the driver sits on the centre line and the Handlebar object turns about the centre (its origin on the steering
axis, over Wheel_F). Renders from the driver's seat (diag/bar_render.py) show it is one wheel, not half a T-bar, so it
is moved, not mirrored: every loose piece of the Handlebar that lies wholly on the left and reaches the bar's height
(top > 0.85 m) - the wheel, its spokes, hub and column - slides right by its own centre, so it stands on the centre
line in front of the driver. The fork, mudguard and headlamp (already centred) stay. Saves the blend in place.
Then it prints where the driver's wrists go on the rim (Unity vehicle space, for Tools/vehicles/fit_vehicle_seats.py):
the rim's outermost points at each side, 2 cm in, 4 cm up (ten-to-two, like a car's wheel in the Vehicle Setup tool).
  blender -b NewVehicles_v06.blend --python-exit-code 1 -P fix_v06_bars.py -- tuktuk_auto tirri_erickshaw
Running it again finds the wheel already centred (nothing wholly on the left) and only prints the wrists."""
import bpy, bmesh, sys
from mathutils import Vector

names = sys.argv[sys.argv.index("--") + 1:]
changed = False
for col in bpy.data.collections:
    if len(col.name) < 4 or col.name[3:] not in names:
        continue
    objs = [o for o in col.all_objects if o.type == "MESH"]
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
    cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
    bar = next(o for o in objs if o.name.split(".")[0].startswith("Handle"))
    mw, mi = bar.matrix_world.copy(), bar.matrix_world.inverted()
    ux = lambda w: -(w.x - cx)          # Unity vehicle x (right = +); Blender's +X is the vehicle's left
    me = bar.data
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seen, pieces = set(), []
    for v in bm.verts:
        if v.index in seen: continue
        stack, idx = [v], []
        seen.add(v.index)
        while stack:
            a = stack.pop(); idx.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen: seen.add(b.index); stack.append(b)
        pieces.append(idx)
    def box(vs):
        ws = [mw @ v.co for v in vs]
        return min(ux(w) for w in ws), max(ux(w) for w in ws), min(w.z for w in ws), max(w.z for w in ws)
    wheel = [p for p in pieces if box(p)[1] < -0.05 and box(p)[3] > 0.85]
    print("==", col.name, "pieces", len(pieces), "wholly left at bar height", len(wheel))
    if wheel:
        vs = [v for p in wheel for v in p]
        xs = [ux(mw @ v.co) for v in vs]
        shift = -(min(xs) + max(xs)) / 2          # Unity x: the wheel's centre onto 0
        for v in vs:
            w = mw @ v.co
            w.x -= shift                          # Unity x = -(X - cx): +shift in Unity is -shift in Blender X
            v.co = mi @ w
        bm.to_mesh(me); me.update()
        changed = True
        print("  moved %d verts (%d pieces) %.3f m right" % (len(vs), len(wheel), shift))
    bm.free()
    # the wrists on the rim: the steering parts are the ones up at the wheel's height (the column's top and the rim)
    P = [(ux(mw @ v.co), (mw @ v.co).z, -((mw @ v.co).y - cy)) for v in me.vertices]
    top = max(p[1] for p in P)
    rim = [p for p in P if p[1] > top - 0.25 and abs(p[0]) < 0.35]
    def side(sign):
        s = [p for p in rim if p[0] * sign > 0.0]
        w = max(p[0] * sign for p in s)
        end = [p for p in s if p[0] * sign > w - 0.02]
        m = [sum(p[k] for p in end) / len(end) for k in range(3)]
        return (round(m[0] - sign * 0.02, 3), round(m[1] + 0.04, 3), round(m[2], 3))
    print("  wrists L %s R %s (wheel top %.3f, rim x %.3f..%.3f)" % (side(-1), side(1), top, min(p[0] for p in rim), max(p[0] for p in rim)))
if changed:
    bpy.ops.wm.save_mainfile()
    print("SAVED", bpy.data.filepath)

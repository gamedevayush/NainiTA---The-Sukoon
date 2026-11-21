"""The handlebar of the auto and the e-rickshaw (docs/VEHICLES.md section 0, item 6: modelled on the left half only):
where its mesh lies about the vehicle's centre line, its origin and axes, its parts (loose pieces) and the body's own
mesh near the bar.  blender -b NewVehicles_v06.blend -P diag/bar_check.py -- tuktuk_auto tirri_erickshaw"""
import bpy, sys, bmesh
from mathutils import Vector

names = sys.argv[sys.argv.index("--") + 1:]
for col in bpy.data.collections:
    if len(col.name) < 4 or col.name[3:] not in names:
        continue
    objs = [o for o in col.all_objects if o.type == "MESH"]
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
    cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
    print("==", col.name, "centre x %.3f y %.3f" % (cx, cy))
    for o in objs:
        n = o.name.split(".")[0]
        if not (n.startswith("Handle") or n.startswith("Wheel_F")):
            continue
        mw = o.matrix_world
        vs = [mw @ v.co for v in o.data.vertices]
        # Unity vehicle space: x = -(X - cx), y = Z, z = -(Y - cy)
        ux = [-(v.x - cx) for v in vs]; uy = [v.z for v in vs]; uz = [-(v.y - cy) for v in vs]
        print("  %s parent %s verts %d  unity x %.3f..%.3f  y %.3f..%.3f  z %.3f..%.3f" % (
            o.name, o.parent.name if o.parent else "-", len(vs), min(ux), max(ux), min(uy), max(uy), min(uz), max(uz)))
        org = mw.translation
        print("    origin unity (%.3f, %.3f, %.3f)  local Z axis (world) %s  scale %s" % (
            -(org.x - cx), org.z, -(org.y - cy), tuple(round(a, 3) for a in mw.to_3x3().col[2].normalized()), tuple(round(s, 3) for s in o.scale)))
        # loose parts
        bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
        seen = set(); parts = []
        for v in bm.verts:
            if v.index in seen: continue
            stack = [v]; idx = []
            seen.add(v.index)
            while stack:
                a = stack.pop(); idx.append(a.index)
                for e in a.link_edges:
                    b = e.other_vert(a)
                    if b.index not in seen: seen.add(b.index); stack.append(b)
            pv = [mw @ bm.verts[i].co for i in idx]
            parts.append((len(idx), min(-(p.x - cx) for p in pv), max(-(p.x - cx) for p in pv), min(p.z for p in pv), max(p.z for p in pv),
                          min(-(p.y - cy) for p in pv), max(-(p.y - cy) for p in pv)))
        bm.free()
        parts.sort(reverse=True)
        for p in parts[:12]:
            print("    part %5d verts  x %.3f..%.3f  y %.3f..%.3f  z %.3f..%.3f" % p)

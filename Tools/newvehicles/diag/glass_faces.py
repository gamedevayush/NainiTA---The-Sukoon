"""Find a vehicle's windscreen in its Body (owner, 21 Nov 2026: "auto me seesha alag krna pdega, andar ka dikh nahi raha"
- the auto's windscreen is painted opaque, the driver cannot be seen). Faces whose centre lies in a box (Unity vehicle
space: x right, y up, z forward) and whose normal faces along the vehicle (|n.z| > min_nz, the glass leans back) are the
glass. Prints how many, their box and the z / normal spread, and renders the vehicle with those faces taken out of the
body and shown as their own red object (Workbench, object colours), from the front, the front three-quarter and the
driver's side, to <out>/glass_<name>_*.png. Read-only (the blend is not saved).
  blender -b NewVehicles_v06.blend --python-exit-code 1 -P diag/glass_faces.py -- <out> tuktuk_auto xmin xmax ymin ymax zmin zmax min_nz
"""
import bpy, bmesh, sys, os
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
out, name = a[0], a[1]
x0, x1, y0, y1, z0, z1, nz = [float(v) for v in a[2:9]]
os.makedirs(out, exist_ok=True)
col = next(c for c in bpy.data.collections if c.name[3:] == name)
objs = [o for o in col.all_objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
U = lambda w: Vector((-(w.x - cx), w.z, -(w.y - cy)))            # Blender world -> Unity vehicle space
Un = lambda n: Vector((-n.x, n.z, -n.y))
body = next(o for o in objs if o.name.split(".")[0] in ("Body", col.name))
mw = body.matrix_world
rot = mw.to_3x3().normalized()
me = body.data
sel = []
for f in me.polygons:
    c = U(mw @ f.center)
    n = Un(rot @ f.normal).normalized()
    if x0 <= c.x <= x1 and y0 <= c.y <= y1 and z0 <= c.z <= z1 and abs(n.z) >= nz:
        sel.append((f.index, c, n))
print("== %s: %d faces of %d selected" % (name, len(sel), len(me.polygons)))
if sel:
    cs = [s[1] for s in sel]
    print("   box x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f" % (min(c.x for c in cs), max(c.x for c in cs), min(c.y for c in cs), max(c.y for c in cs), min(c.z for c in cs), max(c.z for c in cs)))
    fw = sum(1 for s in sel if s[2].z > 0); print("   facing forward %d, backward %d" % (fw, len(sel) - fw))

# the glass faces as their own red object, the body without them (grey)
idx = set(i for i, _, _ in sel)
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
gbm = bmesh.new()
vmap = {}
for f in [bm.faces[i] for i in idx]:
    vs = []
    for v in f.verts:
        if v.index not in vmap: vmap[v.index] = gbm.verts.new(v.co)
        vs.append(vmap[v.index])
    try: gbm.faces.new(vs)
    except ValueError: pass
bmesh.ops.delete(bm, geom=[bm.faces[i] for i in idx], context="FACES_ONLY")
cut = bpy.data.meshes.new("cut"); bm.to_mesh(cut); bm.free()
gm = bpy.data.meshes.new("glass"); gbm.to_mesh(gm); gbm.free()
body.data = cut
gobj = bpy.data.objects.new("GlassPreview", gm); gobj.matrix_world = mw; col.objects.link(gobj)
for o in objs: o.color = (0.75, 0.75, 0.75, 1)
gobj.color = (1, 0, 0, 1)
objs.append(gobj)

sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading; sh.light = "STUDIO"; sh.color_type = "OBJECT"
sc.render.resolution_x, sc.render.resolution_y = 1200, 1000
for o in sc.objects: o.hide_render = o not in objs
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
c = (lo + hi) / 2
cam = bpy.data.objects.new("GlassCam", bpy.data.cameras.new("GlassCam")); sc.collection.objects.link(cam); sc.camera = cam
views = (("front", Vector((c.x, lo.y - 5.0, c.z + 0.5))), ("quarter", Vector((c.x - 3.5, lo.y - 3.5, c.z + 1.2))),
         ("side", Vector((lo.x - 4.5, c.y - 0.6, c.z + 0.4))))
for tag, loc in views:
    cam.location = loc
    cam.rotation_euler = (c - loc).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 50
    for show_glass in (True, False):
        gobj.hide_render = not show_glass
        sc.render.filepath = os.path.join(out, "glass_%s_%s_%s.png" % (name, tag, "red" if show_glass else "open"))
        bpy.ops.render.render(write_still=True)
print("RENDERED")

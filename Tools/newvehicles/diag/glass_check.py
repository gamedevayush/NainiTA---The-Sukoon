"""After fix_v07_glass.py: the vehicle from the front three-quarter with its Glass hidden (red when shown), Workbench,
to <out>/glasscheck_<name>_{with,without}.png. Read-only.
  blender -b NewVehicles_v07.blend --python-exit-code 1 -P diag/glass_check.py -- <out> tuktuk_auto
"""
import bpy, sys, os
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
out, name = a[0], a[1]
os.makedirs(out, exist_ok=True)
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    try: bpy.ops.object.mode_set(mode="OBJECT")
    except RuntimeError: pass
col = next(c for c in bpy.data.collections if c.name[3:] == name)
objs = [o for o in col.all_objects if o.type == "MESH"]
glass = next(o for o in objs if o.name.split(".")[0] == "Glass")
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading; sh.light = "STUDIO"; sh.color_type = "OBJECT"
sc.render.resolution_x, sc.render.resolution_y = 1000, 900
for o in sc.objects: o.hide_render = o not in objs
for o in objs: o.color = (0.75, 0.75, 0.75, 1)
glass.color = (1, 0.1, 0.1, 1)
pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
c = (lo + hi) / 2
cam = bpy.data.objects.new("CheckCam", bpy.data.cameras.new("CheckCam")); sc.collection.objects.link(cam); sc.camera = cam
cam.location = Vector((c.x - 3.0, lo.y - 3.2, c.z + 1.0))
cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
for tag, hide in (("with", False), ("without", True)):
    glass.hide_render = hide
    sc.render.filepath = os.path.join(out, "glasscheck_%s_%s.png" % (name, tag))
    bpy.ops.render.render(write_still=True)
print("RENDERED")

"""Workbench renders of a vehicle's handlebar from the driver's seat and from the front (Unity vehicle space points),
to check fix_v06_bars.py.  blender -b NewVehicles_v06.blend -P diag/bar_render.py -- <out dir> tuktuk_auto tirri_erickshaw"""
import bpy, sys, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out, names = argv[0], argv[1:]
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 900, 700
cam_data = bpy.data.cameras.new("chk"); cam_data.lens = 30
cam = bpy.data.objects.new("chk", cam_data); sc.collection.objects.link(cam); sc.camera = cam
VIEWS = {"driver": ((0.0, 1.55, 0.15), (0.0, 1.0, 0.85)), "front": ((0.6, 1.5, 2.4), (0.0, 1.0, 0.8)), "top": ((0.0, 2.4, 0.75), (0.0, 1.0, 0.76))}
for col in bpy.data.collections:
    if len(col.name) < 4 or not col.name[:2].isdigit(): continue
    objs = [o for o in col.all_objects]
    for o in objs: o.hide_render = col.name[3:] not in names
for col in bpy.data.collections:
    if len(col.name) < 4 or col.name[3:] not in names: continue
    for c2 in bpy.data.collections:
        if len(c2.name) >= 4 and c2.name[:2].isdigit():
            for o in c2.all_objects: o.hide_render = c2 != col
    meshes = [o for o in col.all_objects if o.type == "MESH"]
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
    cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
    B = lambda u: Vector((cx - u[0], cy - u[2], u[1]))
    for vn, (eye, at) in VIEWS.items():
        cam.location = B(eye)
        d = B(at) - B(eye)
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out, "%s_%s.png" % (col.name[3:], vn))
        bpy.ops.render.render(write_still=True)
        print("WROTE", sc.render.filepath)

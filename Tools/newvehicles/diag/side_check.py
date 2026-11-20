"""Side renders (both sides) of chosen vehicles, for checking wheels, handlebars and doors.
blender -b <blend> -P side_check.py -- <out_dir> 13 10 00 01"""
import bpy, os, sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
out, want = args[0], set(args[1:])
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 900, 500
cam = bpy.data.objects.new("scam", bpy.data.cameras.new("scam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"
for c in bpy.data.collections:
    if c.name[:2] not in want:
        continue
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = c not in o.users_collection
    pts = [o.matrix_world @ v.co for o in c.all_objects if o.type == "MESH" for v in o.data.vertices]
    x0, x1 = min(p.x for p in pts), max(p.x for p in pts)
    y0, y1 = min(p.y for p in pts), max(p.y for p in pts)
    z1 = max(p.z for p in pts)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    cam.data.ortho_scale = max(y1 - y0, z1) * 1.15
    for name, eye in (("right", Vector((x0 - 8, cy, z1 / 2))), ("left", Vector((x1 + 8, cy, z1 / 2)))):
        cam.location = eye
        cam.rotation_euler = (Vector((cx, cy, z1 / 2)) - eye).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out, "%s_%s.png" % (c.name, name))
        bpy.ops.render.render(write_still=True)
    print("RENDERED", c.name)

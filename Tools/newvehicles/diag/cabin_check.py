"""Close-ups of the driver's place for chosen vehicles (right-hand drive check): a camera just over the bonnet looking
in and down through the windscreen, and one above each front side window looking across.
  blender -b <file.blend> -P cabin_check.py -- <out_dir> 03 06 01"""
import bpy, os, sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
out, want = args[0], set(args[1:])
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 1000, 700
cam = bpy.data.objects.new("ccam", bpy.data.cameras.new("ccam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 24
cam.data.clip_start = 0.02
for c in bpy.data.collections:
    if c.name[:2] not in want:
        continue
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = c not in o.users_collection
    pts = [o.matrix_world @ Vector(b) for o in c.all_objects if o.type == "MESH" for b in o.bound_box]
    x0, x1 = min(p.x for p in pts), max(p.x for p in pts)
    y0, y1 = min(p.y for p in pts), max(p.y for p in pts)
    z1 = max(p.z for p in pts)
    cx, L = (x0 + x1) / 2, y1 - y0
    shots = {
        "in": (Vector((cx, y0 + L * 0.06, z1 * 0.82)), Vector((cx, y0 + L * 0.3, z1 * 0.45))),
        "R": (Vector((x0 - 1.2, y0 + L * 0.2, z1 * 0.8)), Vector((cx, y0 + L * 0.2, z1 * 0.55))),   # from the vehicle's right (-X)
        "L": (Vector((x1 + 1.2, y0 + L * 0.2, z1 * 0.8)), Vector((cx, y0 + L * 0.2, z1 * 0.55))),   # from its left (+X)
    }
    for k, (eye, look) in shots.items():
        cam.location = eye
        cam.rotation_euler = (look - eye).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out, "%s_%s.png" % (c.name, k))
        bpy.ops.render.render(write_still=True)
    print("RENDERED", c.name)

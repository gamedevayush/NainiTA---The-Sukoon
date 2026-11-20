"""Right-hand drive check (docs/VEHICLES.md 9.2 step 6): for every car and bus (collections 00-11) render the view through
the windscreen - the camera in front (-Y), at windscreen height, looking +Y. From there the vehicle's RIGHT (-X) is on the
LEFT of the picture: a right-hand-drive vehicle shows its steering wheel on the left half.
  blender -b <file.blend> --python-exit-code 1 -P windscreen_check.py -- <out_dir>
Writes <out_dir>/NN_name.png and a close-up from above the bonnet (NN_name_top.png)."""
import bpy, os, sys
from mathutils import Vector

out = sys.argv[sys.argv.index("--") + 1]
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.display.shading.show_xray = False
sc.render.resolution_x, sc.render.resolution_y = 900, 600
cam = bpy.data.objects.new("wcam", bpy.data.cameras.new("wcam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 35
cols = [c for c in bpy.data.collections if len(c.name) > 3 and c.name[:2].isdigit() and int(c.name[:2]) <= 11]
for c in cols:
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = c not in o.users_collection
    pts = [o.matrix_world @ Vector(b) for o in c.all_objects if o.type == "MESH" for b in o.bound_box]
    x0, x1 = min(p.x for p in pts), max(p.x for p in pts)
    y0, y1 = min(p.y for p in pts), max(p.y for p in pts)
    z1 = max(p.z for p in pts)
    cx = (x0 + x1) / 2
    # through the windscreen: in front, at about 72 % of the height, a little down
    eye = Vector((cx, y0 - 1.6, z1 * 0.72))
    look = Vector((cx, y0 + (y1 - y0) * 0.35, z1 * 0.55))
    cam.location = eye
    cam.rotation_euler = (look - eye).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, c.name + ".png")
    bpy.ops.render.render(write_still=True)
    # from above and in front, looking down into the cabin
    eye = Vector((cx, y0 - 0.6, z1 + 1.4))
    look = Vector((cx, y0 + (y1 - y0) * 0.3, z1 * 0.5))
    cam.location = eye
    cam.rotation_euler = (look - eye).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, c.name + "_top.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", c.name)

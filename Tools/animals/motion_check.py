"""Film an animal's clips in motion (side view, 2 loops each), Workbench, PNG frames -> outdir/_motion/.
  blender -b <kind>.blend -P motion_check.py -- <outdir>
Then ffmpeg -framerate 30 -i _motion/%04d.png motion.mp4"""
import bpy, sys, os
from mathutils import Vector
out = sys.argv[sys.argv.index("--") + 1:][0]
sc = bpy.context.scene
rig = next(o for o in sc.objects if o.type == "ARMATURE")
ob = next(o for o in sc.objects if o.type == "MESH")
cam = sc.camera
if cam is None:   # the .blend is saved before the check camera exists
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
w = bpy.data.worlds.new("w"); sc.world = w; w.color = (1, 0, 1)
pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
L = max(p.y for p in pts) - min(p.y for p in pts); H = max(p.z for p in pts)
cam.data.type = "ORTHO"; cam.data.ortho_scale = max(L, H) * 1.4
sc.render.resolution_x, sc.render.resolution_y = 480, 360
d = os.path.join(out, "_motion"); os.makedirs(d, exist_ok=True)
i = 0
for act in bpy.data.actions:
    rig.animation_data.action = act
    n = int(act.frame_end)
    for vn, pos in (("side", Vector((-4 * L, 0, H * 0.5))), ("q34", Vector((-3 * L, -3 * L, H * 1.1)))):
        cam.location = pos
        cam.rotation_euler = (Vector((0, 0, H * 0.45)) - pos).to_track_quat("-Z", "Y").to_euler()
        for loop in range(2):
            for f in range(n):
                sc.frame_set(f)
                sc.render.filepath = os.path.join(d, "%04d.png" % i); i += 1
                bpy.ops.render.render(write_still=True)
print("MOTION frames", i)

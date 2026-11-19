import bpy, math, sys
from mathutils import Vector
out = sys.argv[sys.argv.index("--") + 1]
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"; sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 1600, 500
xs = [o.location.x for o in sc.objects if o.type == "MESH"]
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = (max(xs) - min(xs)) + 6
t = Vector(((max(xs) + min(xs)) / 2, 0, 1.0))
cam.location = t + Vector((-6, -30, 12)); cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.render.filepath = out; bpy.ops.render.render(write_still=True); print("RENDERED", out)

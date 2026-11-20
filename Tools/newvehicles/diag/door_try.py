"""Render a bus door from outside for several trial shifts of its mesh (world metres).
blender -b <blend> -P door_try.py -- <door> <frame> <out_dir> "x,y,z" ["x,y,z" ...]"""
import bpy, os, sys
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
door = bpy.data.objects[args[0]]
frame, out = int(args[1]), args[2]
shifts = [Vector(tuple(float(v) for v in s.split(","))) for s in args[3:]]
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
sc.frame_set(frame)
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 700, 700
col = door.users_collection[0]
for o in bpy.data.objects:
    if o.type == "MESH":
        o.hide_render = col not in o.users_collection
cam = bpy.data.objects.new("dcam", bpy.data.cameras.new("dcam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 30
pts = [door.matrix_world @ Vector(b) for b in door.bound_box]
c = sum(pts, Vector()) / 8
side = -1.0 if c.x < 0 else 1.0
orig = [v.co.copy() for v in door.data.vertices]
for k, t in enumerate(shifts):
    for v, o in zip(door.data.vertices, orig):
        v.co = o
    dl = door.matrix_world.inverted().to_3x3() @ t
    door.data.transform(Matrix.Translation(dl))
    door.data.update()
    for name, eye in (("out", Vector((c.x + side * 3.2, c.y - 0.6, 1.5))), ("in", Vector((c.x - side * 1.0, c.y + 2.0, 1.9)))):
        look = Vector((c.x + t.x, c.y + t.y, 1.3))
        cam.location = eye
        cam.rotation_euler = (look - eye).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out, "%s_%d_%s.png" % (door.name, k, name))
        bpy.ops.render.render(write_still=True)
    print("RENDERED", k, tuple(round(x, 3) for x in t))

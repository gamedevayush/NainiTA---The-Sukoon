"""
Render a small iso thumbnail + bounding size of each FBX (to review a building kit before placing it).
Usage: blender -b --factory-startup -P render_fbx_sheet.py -- <out_dir> <fbx> [<fbx> ...]
Prints: SIZE <name> <x> <y> <z>   (metres, Blender axes after import: x width, y depth, z height)
"""
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out = argv[0]
os.makedirs(out, exist_ok=True)
for f in argv[1:]:
    name = os.path.splitext(os.path.basename(f))[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=f)
    except Exception as e:
        print("FAIL", name, e)
        continue
    mins, maxs = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            mins = Vector(map(min, mins, w))
            maxs = Vector(map(max, maxs, w))
    size = maxs - mins
    print(f"SIZE {name} {size.x:.2f} {size.y:.2f} {size.z:.2f}")
    ctr = (mins + maxs) / 2
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x, sc.render.resolution_y = 360, 300
    sc.world = bpy.data.worlds.new("W")
    cam_d = bpy.data.cameras.new("C")
    cam_d.type = "ORTHO"
    cam_d.ortho_scale = max(size.x, size.y, size.z) * 1.6
    cam = bpy.data.objects.new("C", cam_d)
    sc.collection.objects.link(cam)
    sc.camera = cam
    d = max(size) * 3
    cam.location = ctr + Vector((d * 0.7, -d * 0.7, d * 0.55))
    cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)

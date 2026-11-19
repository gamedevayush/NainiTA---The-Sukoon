"""
Check one Tripo vehicle .glb before it goes anywhere (19 Nov 2026): triangle count, texture size, size in metres, and a
4-view sheet (front 3/4, side, rear 3/4, front-wheel close-up) rendered with Workbench + texture colours (light, no GPU load).
  blender -b --factory-startup --python-exit-code 1 -P check_model.py -- <model.glb> <out_sheet.png>
"""
import bpy, sys, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
GLB, OUT = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
imgs = [(i.name, tuple(i.size)) for i in bpy.data.images if i.size[0] > 0]
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
size = hi - lo
print("CHECK meshes %d tris %d textures %s size %.2f x %.2f x %.2f (raw units)" % (len(meshes), tris, imgs, size.x, size.y, size.z))

sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.display.shading.show_cavity = True
sc.render.resolution_x, sc.render.resolution_y = 640, 400
sc.render.film_transparent = False
w = bpy.data.worlds.new("w"); sc.world = w
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
centre = (lo + hi) / 2
R = max(size) * 1.25
long_axis = 0 if size.x >= size.y else 1   # glTF import: up is Z; the vehicle's length is the longer ground axis


def view(name, az_deg, el_deg, dist, target):
    a, e = math.radians(az_deg), math.radians(el_deg)
    d = Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)))
    cam.location = target + d * dist
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = OUT.replace(".png", "_%s.png" % name)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


base = 0 if long_axis == 0 else 90
shots = [view("front34", base + 35, 12, R, centre), view("side", base + 90, 5, R, centre),
         view("rear34", base + 215, 12, R, centre)]
# wheel close-up: lowest front corner on the camera side
wheel = Vector((centre.x + (size.x * 0.33 if long_axis == 0 else size.x * 0.5),
                centre.y + (size.y * 0.5 if long_axis == 0 else size.y * 0.33), lo.z + size.z * 0.18))
shots.append(view("wheel", base + 70, 8, max(size) * 0.45, wheel))
print("SHOTS", shots)

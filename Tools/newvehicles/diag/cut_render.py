"""A side cut through one vehicle (Workbench, orthographic, from its right): everything right of Unity x = <cut> is
clipped away, so a cab or a seat row shows from inside. A 10 cm grid of lines (Unity y / z) is drawn over it.
blender -b NewVehicles_v06.blend -P diag/cut_render.py -- <out.png> <model> <cut x> <centre z> <centre y> <half height>"""
import bpy, sys
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
out, model, cut, cz, cyy, half = a[0], a[1], float(a[2]), float(a[3]), float(a[4]), float(a[5])
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.display.shading.show_backface_culling = False
sc.render.resolution_x = sc.render.resolution_y = 1000
col = next(c for c in bpy.data.collections if c.name[3:] == model)
for c2 in bpy.data.collections:
    if len(c2.name) >= 4 and c2.name[:2].isdigit():
        for o in c2.all_objects:
            if o: o.hide_render = c2 != col
meshes = [o for o in col.all_objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
B = lambda u: Vector((cx - u[0], cy - u[2], u[1]))
cd = bpy.data.cameras.new("cut"); cd.type = "ORTHO"; cd.ortho_scale = half * 2
cam = bpy.data.objects.new("cut", cd); sc.collection.objects.link(cam); sc.camera = cam
cam.location = B((cut + 5.0, cyy, cz))
cam.rotation_euler = (B((cut, cyy, cz)) - cam.location).to_track_quat("-Z", "Y").to_euler()
cd.clip_start = 5.0; cd.clip_end = 20.0
# grid: thin boxes every 10 cm (thicker every 50 cm) in the plane just left of the cut
gm = bpy.data.materials.new("grid"); gm.diffuse_color = (1, 0, 0, 1)
cube = bpy.data.meshes.new("gridcube")
cube.from_pydata([(x, y, z) for x in (-.5, .5) for y in (-.5, .5) for z in (-.5, .5)], [],
                 [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])
cube.materials.append(gm)
def bar(p, s, thick):
    o = bpy.data.objects.new("grid", cube); sc.collection.objects.link(o)
    o.location = p; o.scale = s
for k in range(-int(half * 10), int(half * 10) + 1):
    y = round(cyy + k * 0.1, 2)
    t = 0.006 if abs(y * 10 - round(y * 10)) < 1e-6 and round(y * 100) % 50 == 0 else 0.002
    bar(B((cut - 0.01, y, cz)), (0.001, half * 2, t), t)
    z = round(cz + k * 0.1, 2)
    t = 0.006 if round(z * 100) % 50 == 0 else 0.002
    bar(B((cut - 0.01, cyy, z)), (0.001, t, half * 2), t)
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print("WROTE", out)

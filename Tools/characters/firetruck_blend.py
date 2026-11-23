"""Fire truck (24 Nov 2026): decoded Tripo GLB -> FireTruck_v01.blend at real size for the owner's tyre / door cuts.
Same conventions as Tools/newvehicles/build_blend.py: front -Y, ground z 0, uniform scale, textures packed.
  blender -b --factory-startup -P firetruck_blend.py -- <decoded.glb> <out.blend> [length_m]"""
import bpy, sys, math
from mathutils import Vector, Matrix
a = sys.argv[sys.argv.index("--") + 1:]
GLB, OUT = a[0], a[1]
LENGTH = float(a[2]) if len(a) > 2 else 7.1      # a 2-axle Indian fire tender (Tata LPT 1613 class) is ~7.2-7.6 m
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
ms = [o for o in bpy.context.scene.objects if o.type == "MESH"]
for o in bpy.context.scene.objects: o.select_set(o in ms)
bpy.context.view_layer.objects.active = ms[0]
if len(ms) > 1: bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
for o in list(bpy.context.scene.objects):
    if o != ob: bpy.data.objects.remove(o)
ob.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = ob.data
me.transform(Matrix.Rotation(math.pi / 2, 4, "Z"))      # Tripo cars face +X -> -Y
xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
# median of the length / width / height ratios against a real tender (7.1 x 2.5 x 3.2 m incl. ladder) - Tripo trucks are chunky
REAL = (2.5, LENGTH, 3.2)
s = sorted([REAL[0] / (max(xs) - min(xs)), REAL[1] / (max(ys) - min(ys)), REAL[2] / (max(zs) - min(zs))])[1]
me.transform(Matrix.Translation(Vector((-(max(xs) + min(xs)) / 2 * s, -(max(ys) + min(ys)) / 2 * s, -min(zs) * s))) @ Matrix.Scale(s, 4))
ob.name = "Body"
for m in me.materials:
    if m: m.use_backface_culling = False
col = bpy.data.collections.new("24_firetruck"); bpy.context.scene.collection.children.link(col)
bpy.context.scene.collection.objects.unlink(ob); col.objects.link(ob)
xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
print("TRUCK size %.2f x %.2f x %.2f m, tris %d" % (max(xs) - min(xs), max(ys) - min(ys), max(zs), sum(len(p.vertices) - 2 for p in me.polygons)))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=OUT)

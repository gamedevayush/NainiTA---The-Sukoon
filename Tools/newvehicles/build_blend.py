"""
All new (unbranded) Tripo vehicles in one .blend for hand work (tyre cuts, damage) - 19 Nov 2026.
Every models\\NN_name_dec.glb (meshopt already decoded) is imported, named after the vehicle, turned to the game's
convention (front -Y, x across, z up, ground at z = 0, origin at the ground centre), scaled UNIFORMLY to its real length
and put in its own collection, in a row along X. Textures are packed into the file.
  blender -b --factory-startup --python-exit-code 1 -P build_blend.py -- <out.blend>
Front direction as Tripo gives it (checked on the 4-view sheets in checks\\): cars +X, two-wheelers +Y. A vehicle that
comes out another way goes into FRONT_OVERRIDE.
"""
import bpy, sys, os, glob, math
from mathutils import Vector, Matrix

OUT = sys.argv[sys.argv.index("--") + 1]
ROOT = os.path.dirname(os.path.abspath(__file__))
# Real size in metres: length, width WITH mirrors, height (with roof racks). Tripo squeezes long vehicles (the buses came
# out 3.6 m wide when scaled by length), so the one uniform scale is the MEDIAN of the three ratios: a single bad
# dimension can't blow the whole vehicle up.
REAL = {
    "bus_roadways": (9.6, 2.9, 3.25), "bus_tourist": (11.0, 2.95, 3.45), "car_hatch_small": (3.7, 1.85, 1.55),
    "car_tallboy": (3.65, 1.85, 1.70), "car_sedan_compact": (4.0, 1.95, 1.50), "car_mpv_compact": (4.4, 2.0, 1.72),
    "car_mpv_large": (4.75, 2.1, 1.85), "car_suv_boxy": (4.0, 2.0, 1.95), "car_suv_muscular": (4.6, 2.1, 1.85),
    "car_pickup": (4.9, 2.0, 1.95), "car_van": (4.5, 2.1, 2.3), "truck_light": (6.0, 2.3, 2.5),
    "bike_commuter100": (2.0, 0.75, 1.1), "bike_commuter125": (2.05, 0.75, 1.1), "bike_street160": (2.05, 0.8, 1.1),
    "bike_retro350": (2.1, 0.8, 1.1), "bike_dualsport": (2.2, 0.85, 1.25), "scooty_family": (1.83, 0.72, 1.15),
    "scooty_neoretro": (1.85, 0.72, 1.15), "scooty_electric": (1.85, 0.72, 1.15), "tirri_erickshaw": (2.8, 1.0, 1.85),
    "tuktuk_auto": (2.65, 1.3, 1.72),
}
FRONT_OVERRIDE = {   # name -> "+X" / "-X" / "+Y" / "-Y" (seen on the checks\ side views: image right = -Y there)
    "tirri_erickshaw": "-Y", "bus_roadways": "-Y", "bus_tourist": "-Y", "bike_commuter125": "-Y", "scooty_neoretro": "-Y", "scooty_electric": "-Y",
}
TURN = {"+X": -90, "-X": 90, "+Y": 180, "-Y": 0}   # degrees about Z that bring that side to -Y

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
x_cursor = 0.0
report = []
for path in sorted(glob.glob(os.path.join(ROOT, "models", "*_dec.glb"))):
    base = os.path.basename(path)[:-len("_dec.glb")]          # e.g. 02_car_hatch_small
    name = base[3:]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    # bake every transform into the mesh and drop the importer's empties
    for o in meshes:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.name = ob.data.name = base

    pts = [v.co for v in ob.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    size = hi - lo
    front = FRONT_OVERRIDE.get(name, "+X" if size.x >= size.y else "+Y")
    length, width = max(size.x, size.y), min(size.x, size.y)
    rl, rw, rh = REAL.get(name, (4.0, 1.9, 1.6))
    s = sorted([rl / length, rw / width, rh / size.z])[1]
    centre = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    m = Matrix.Rotation(math.radians(TURN[front]), 4, "Z") @ Matrix.Scale(s, 4) @ Matrix.Translation(-centre)
    ob.data.transform(m)
    ob.data.update()
    pts = [v.co for v in ob.data.vertices]
    w = max(p.x for p in pts) - min(p.x for p in pts)
    l = max(p.y for p in pts) - min(p.y for p in pts)
    h = max(p.z for p in pts)
    ob.location = (x_cursor + w / 2, 0, 0)
    x_cursor += w + 2.0

    col = bpy.data.collections.new(base)
    scene.collection.children.link(col)
    for c in ob.users_collection:
        c.objects.unlink(ob)
    col.objects.link(ob)
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    report.append("%-24s front was %s  scale x%.3f  -> %.2f long, %.2f wide, %.2f high, %d tris" % (base, front, s, l, w, h, tris))

bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=OUT, compress=True)
print("\n".join(report))
print("SAVED", OUT, len(report), "vehicles")

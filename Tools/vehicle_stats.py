"""Blender: for each vehicle model in a folder, report triangles, size and materials, and render a small 3/4 view.

blender -b -P vehicle_stats.py -- <model dir> <out dir>
Writes <out dir>/<name>.png and <out dir>/stats.json.
"""
import bpy, sys, os, json, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[0], argv[1]
os.makedirs(out, exist_ok=True)
stats = {}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load(path):
    ext = os.path.splitext(path)[1].lower()
    if ext == ".glb" or ext == ".gltf":
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext == ".dae":
        bpy.ops.wm.collada_import(filepath=path)


for fn in sorted(os.listdir(src)):
    if not fn.lower().endswith((".glb", ".fbx", ".dae")):
        continue
    name = os.path.splitext(fn)[0]
    clear()
    try:
        load(os.path.join(src, fn))
    except Exception as e:
        stats[name] = {"error": str(e)[:200]}
        continue
    for o in list(bpy.context.scene.objects):   # LOD files: look at the full-detail mesh only
        if o.name.startswith(("Mesh_L1", "Mesh_L2")):
            bpy.data.objects.remove(o, do_unlink=True)
    for o in bpy.context.scene.objects:   # wheels pulled out sideways, to see what was cut
        if o.name.startswith("Wheel_") and os.environ.get("EXPLODE"):
            o.location.x += 0.9 if o.location.x > 0 else -0.9
    dg = bpy.context.evaluated_depsgraph_get()
    tris, mats, imgs = 0, set(), set()
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in meshes:
        ev = o.evaluated_get(dg)
        m = ev.to_mesh()
        tris += sum(len(p.vertices) - 2 for p in m.polygons)
        for v in m.vertices:
            w = o.matrix_world @ v.co
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
        ev.to_mesh_clear()
        for s in o.material_slots:
            if s.material:
                mats.add(s.material.name)
                if s.material.use_nodes:
                    for n in s.material.node_tree.nodes:
                        if n.type == "TEX_IMAGE" and n.image:
                            imgs.add((n.image.name, tuple(n.image.size)))
    size = hi - lo
    stats[name] = {"tris": tris, "meshes": len(meshes), "size": [round(size.x, 2), round(size.y, 2), round(size.z, 2)],
                   "materials": len(mats), "images": sorted(f"{a} {b[0]}x{b[1]}" for a, b in imgs),
                   "armature": any(o.type == "ARMATURE" for o in bpy.context.scene.objects)}
    # 3/4 render
    c = (lo + hi) * 0.5
    r = max(size.x, size.y, size.z)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    bpy.context.scene.collection.objects.link(cam)
    cam.location = c + Vector((r * 1.1, -r * 1.3, r * 0.7))
    cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 40
    bpy.context.scene.camera = cam
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    bpy.context.scene.collection.objects.link(sun)
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.66, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    bpy.context.scene.world = w
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE_NEXT"
    sc.render.resolution_x, sc.render.resolution_y = 360, 240
    sc.render.filepath = os.path.join(out, name + ".png")
    try:
        bpy.ops.render.render(write_still=True)
    except Exception as e:
        stats[name]["render_error"] = str(e)[:120]
    print("[stats]", name, stats[name], flush=True)

json.dump(stats, open(os.path.join(out, "stats.json"), "w"), indent=1)

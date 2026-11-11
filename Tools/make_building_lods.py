"""
Make a game-ready, 3-LOD version of a heavy building FBX (G.O.A.N. kit) in Blender (headless).

Usage:
  blender -b --factory-startup --python-exit-code 1 -P make_building_lods.py -- \
      <in.fbx> <out.fbx> <lod0_tris> <lod1_tris> <lod2_tris> [height_m]

- Joins all meshes, puts the pivot at the bottom centre (so it sits on the ground at y=0).
- height_m (optional) rescales the model to that height (Tripo exports are 1 m tall).
- LODs are collapse-decimated copies of the full-detail mesh, named Mesh_L0/L1/L2 (not "_LOD0", so
  Unity's importer does not build its own LOD group; our prefab builder does).
- Textures are embedded so Unity rebuilds the same materials.
Prints: LODS <name> <w> <d> <h> <tris0> <tris1> <tris2>
"""
import bpy, sys, os
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
IN, OUT = argv[0], argv[1]
TARGETS = [int(argv[2]), int(argv[3]), int(argv[4])]
HEIGHT = float(argv[5]) if len(argv) > 5 else 0.0
NAME = os.path.splitext(os.path.basename(OUT))[0]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=IN)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
assert meshes, "no mesh in " + IN
for o in bpy.context.scene.objects:
    o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(bpy.context.scene.objects):
    if o != obj:
        bpy.data.objects.remove(o, do_unlink=True)

me = obj.data
xs = [v.co for v in me.vertices]
mn = Vector((min(c.x for c in xs), min(c.y for c in xs), min(c.z for c in xs)))
mx = Vector((max(c.x for c in xs), max(c.y for c in xs), max(c.z for c in xs)))
s = HEIGHT / (mx.z - mn.z) if HEIGHT > 0 else 1.0
me.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z)))
me.update()
size = (mx - mn) * s


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


full = tris(obj)
lods = []
for i, target in enumerate(TARGETS):
    o = obj.copy()
    o.data = obj.data.copy()
    o.name = f"Mesh_L{i}"
    o.data.name = f"{NAME}_L{i}"
    bpy.context.scene.collection.objects.link(o)
    ratio = min(1.0, target / max(full, 1))
    if ratio < 0.999:
        m = o.modifiers.new("Decimate", "DECIMATE")
        m.ratio = ratio
        m.use_collapse_triangulate = True
        for x in bpy.context.scene.objects:
            x.select_set(x == o)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier="Decimate")
    lods.append(o)
bpy.data.objects.remove(obj, do_unlink=True)

for x in bpy.context.scene.objects:
    x.select_set(x in lods)
out_dir = os.path.dirname(OUT)
os.makedirs(out_dir, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=OUT, use_selection=True, object_types={"MESH"}, mesh_smooth_type="FACE",
    path_mode="STRIP", embed_textures=False, apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z", axis_up="Y")

# textures per material slot (same order as Unity's sub-meshes): copied next to the FBX
import json, shutil


def linked_image(node_tree, socket_name):
    for n in node_tree.nodes:
        if n.type != "BSDF_PRINCIPLED":
            continue
        sock = n.inputs.get(socket_name)
        if not sock or not sock.is_linked:
            return None
        src = sock.links[0].from_node
        if src.type == "NORMAL_MAP" and src.inputs["Color"].is_linked:
            src = src.inputs["Color"].links[0].from_node
        return src.image if src.type == "TEX_IMAGE" else None
    return None


tex_dir = os.path.join(out_dir, "Textures")
os.makedirs(tex_dir, exist_ok=True)
mats = []
for i, slot in enumerate(lods[0].material_slots):
    m = slot.material
    entry = {"name": m.name if m else "", "color": "", "normal": ""}
    if m and m.use_nodes:
        for key, sock in (("color", "Base Color"), ("normal", "Normal")):
            img = linked_image(m.node_tree, sock)
            if not img:
                continue
            if img.packed_file:                      # texture embedded in the source FBX
                data = bytes(img.packed_file.data)
                ext = ".png" if data[:4] == b"\x89PNG" else ".jpg"
                dst = os.path.join(tex_dir, f"{NAME}_m{i}_{key}{ext}")
                with open(dst, "wb") as fh:
                    fh.write(data)
                entry[key] = os.path.basename(dst)
            elif img.filepath:
                src = bpy.path.abspath(img.filepath)
                if os.path.exists(src):
                    dst = os.path.join(tex_dir, f"{NAME}_m{i}_{key}{os.path.splitext(src)[1].lower()}")
                    shutil.copyfile(src, dst)
                    entry[key] = os.path.basename(dst)
    mats.append(entry)
with open(os.path.join(out_dir, NAME + ".json"), "w") as f:
    json.dump({"name": NAME, "size": [round(size.x, 3), round(size.y, 3), round(size.z, 3)],
               "tris": [tris(o) for o in lods], "materials": mats}, f, indent=1)
print(f"LODS {NAME} {size.x:.2f} {size.y:.2f} {size.z:.2f} {full} {tris(lods[0])} {tris(lods[1])} {tris(lods[2])} mats={len(mats)} tex={sum(1 for m in mats if m['color'])}")

"""
A rigged (Mixamo) Tripo character made phone-sized for the game, in Blender (headless). Police officers, 18 Nov 2026
(owner: police and ambulance system; the kit's PoliceGuys are 206k triangles each).

Usage:
  blender -b --factory-startup --python-exit-code 1 -P decimate_rigged_fbx.py -- <in.fbx> <out.fbx> <out.jpg> <lod0_tris> <lod1_tris>

- Keeps the armature and its Mixamo bone names, the skin weights and the UVs; drops the animations.
- The mesh is decimated (collapse, vertex groups kept) to lod0_tris as "Body", and a copy to lod1_tris as "Body_LOD1".
- The base-colour texture (embedded in the FBX) is written to out.jpg (at most 1024 px).
- Size and pivot stay as they are (1 m tall, feet at 0): CharacterLibraryBuilder scales the prefab to the height.
"""
import bpy, os, sys

argv = sys.argv[sys.argv.index("--") + 1:]
IN, OUT, JPG, T0, T1 = argv[0], argv[1], argv[2], int(argv[3]), int(argv[4])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=IN, automatic_bone_orientation=False)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
arm.animation_data_clear()
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if len(meshes) > 1:
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
body = next(o for o in bpy.context.scene.objects if o.type == "MESH")
body.name = "Body"


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def decimate(o, target):
    n = tris(o)
    if n <= target:
        return
    mod = o.modifiers.new("dec", "DECIMATE")
    mod.ratio = target / n
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_move_to_index(modifier="dec", index=0)   # before the armature modifier
    bpy.ops.object.modifier_apply(modifier="dec")


full = tris(body)
decimate(body, T0)
lod1 = body.copy()
lod1.data = body.data.copy()
lod1.name = "Body_LOD1"
bpy.context.scene.collection.objects.link(lod1)
decimate(lod1, T1)

# the texture: the first image the material uses
img = None
for slot in body.material_slots:
    if slot.material and slot.material.use_nodes:
        for n in slot.material.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                img = n.image
                break
    if img:
        break
if img:
    if max(img.size) > 1024:
        k = 1024 / max(img.size)
        img.scale(max(1, int(img.size[0] * k)), max(1, int(img.size[1] * k)))
    img.filepath_raw = JPG
    img.file_format = "JPEG"
    img.save()

bpy.ops.object.select_all(action="DESELECT")
for o in (arm, body, lod1):
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, add_leaf_bones=False, bake_anim=False,
                         path_mode="STRIP", embed_textures=False, mesh_smooth_type="FACE")
print(f"RIGGED {os.path.basename(OUT)}: {full} -> {tris(body)} / {tris(lod1)} tris, bones {len(arm.data.bones)}, texture {bool(img)}")

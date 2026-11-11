"""
Report, for each rigged FBX, how many vertices each bone dominates and the bone lengths.
Usage: blender -b --factory-startup -P check_rig_weights.py -- <fbx> [<fbx> ...]
A healthy humanoid has every limb bone owning a few hundred vertices; a bone with ~0 vertices
(or one bone owning nearly everything) means the skinning failed for that part.
"""
import bpy, sys, os
argv = sys.argv[sys.argv.index("--") + 1:]
for f in argv:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f)
    body = next(o for o in bpy.context.scene.objects if o.type == "MESH" and o.name.startswith("Body") and "LOD" not in o.name)
    arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    names = {g.index: g.name for g in body.vertex_groups}
    dom = {}
    for v in body.data.vertices:
        if not v.groups:
            dom["<none>"] = dom.get("<none>", 0) + 1
            continue
        g = max(v.groups, key=lambda g: g.weight)
        dom[names[g.group]] = dom.get(names[g.group], 0) + 1
    lens = {b.name: round(b.length, 3) for b in arm.data.bones}
    print("WEIGHTS", os.path.basename(f), len(body.data.vertices))
    for b in arm.data.bones:
        print(f"   {b.name:14s} verts={dom.get(b.name, 0):5d} len={lens[b.name]}")
    if "<none>" in dom:
        print("   <none>", dom["<none>"])

"""
Every vehicle of a NewVehicles_vNN.blend -> its own FBX in Assets/Nainital/Vehicles/New/<name>.fbx for the Vehicle
Setup tool (Nainital > Vehicle Setup) - 19 Nov 2026.
One collection per vehicle (named NN_name); everything in it is exported: Body, Wheel_FL/FR/RL/RR (bikes Wheel_F/R),
Door_FL/FR/RL/RR/Back (origin on the hinge), shape keys Dmg_Front/Rear/Left/Right/Top on the Body. The collection is
moved to the origin for the export only (the blend keeps its row). Same axes as make_vehicle_lods.py (front -Y in
Blender = +Z in Unity), textures embedded, modifiers NOT applied (that keeps the shape keys).
  blender -b <NewVehicles_vNN.blend> --python-exit-code 1 -P export_unity.py -- <Assets/Nainital/Vehicles/New> [name ...]
A lone mesh still called NN_name is renamed Body in the FBX.
"""
import bpy, sys, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
OUT, ONLY = argv[0], set(argv[1:])
os.makedirs(OUT, exist_ok=True)
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    try:
        bpy.ops.object.mode_set(mode="OBJECT")
    except RuntimeError:
        pass
done = []
for col in bpy.data.collections:
    if len(col.name) < 4 or not col.name[:2].isdigit():
        continue
    name = col.name[3:]
    if ONLY and name not in ONLY and col.name not in ONLY:
        continue
    objs = [o for o in col.all_objects if o.type in ("MESH", "EMPTY")]
    if not objs:
        continue
    tops = [o for o in objs if o.parent is None or o.parent not in objs]
    # the vehicle's ground centre: the middle of everything in the collection (not the Body's origin, which may have
    # been moved while cutting wheels and doors)
    body = next((o for o in objs if o.name.split(".")[0] in ("Body", col.name)), objs[0])
    pts = [o.matrix_world @ Vector(c) for o in objs if o.type == "MESH" for c in o.bound_box]
    off = Vector(((min(p.x for p in pts) + max(p.x for p in pts)) / 2, (min(p.y for p in pts) + max(p.y for p in pts)) / 2, 0.0))
    renamed = []
    if body.name == col.name:
        renamed.append((body, body.name)); body.name = "Body"
    # objects that keep an animation (the bus doors' fold / slide, 21 Nov): their location keys must move with the
    # collection too, and the FBX gets the animation baked over their key range
    animated = [o for o in objs if o.animation_data and o.animation_data.action]
    shifted = []
    for o in tops:
        o.location -= off
        if o in animated:
            for fc in o.animation_data.action.fcurves:
                if fc.data_path == "location":
                    d = off[fc.array_index]
                    for k in fc.keyframe_points:
                        k.co.y -= d; k.handle_left.y -= d; k.handle_right.y -= d
                    shifted.append((fc, d))
    sc = bpy.context.scene
    frames = sorted({k.co.x for o in animated for fc in o.animation_data.action.fcurves for k in fc.keyframe_points})
    if frames:
        sc.frame_start, sc.frame_end = int(frames[0]), int(frames[-1])
    sc.frame_set(sc.frame_start)
    # the collection may be excluded / hidden in the saved file (he edits one vehicle at a time): an object that is not
    # in the view layer cannot be selected, and the FBX came out empty (20 Nov). Only this background copy changes.
    def show(lc):
        if lc.collection == col:
            lc.exclude = False; lc.hide_viewport = False; return True
        return any(show(ch) for ch in lc.children)
    show(bpy.context.view_layer.layer_collection)
    col.hide_viewport = False; col.hide_select = False
    for ob in bpy.context.view_layer.objects:   # no operator: its poll fails when the file was saved in edit mode
        ob.select_set(False)
    for o in objs:
        o.hide_viewport = False; o.hide_select = False; o.hide_set(False); o.select_set(True)
    bpy.context.view_layer.objects.active = body
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, mesh_smooth_type="FACE",
        use_mesh_modifiers=False, path_mode="COPY", embed_textures=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, add_leaf_bones=False, bake_anim=bool(animated),
        bake_anim_use_all_bones=False, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
    for o in tops:
        o.location += off
    for fc, d in shifted:
        for k in fc.keyframe_points:
            k.co.y += d; k.handle_left.y += d; k.handle_right.y += d
    for o, n in renamed:
        o.name = n
    # the textures as plain PNGs too: Unity left the embedded ones off the URP materials (19 Nov), so
    # VehicleSetupWindow.FixMaterials builds the materials from these (Textures/<name>_color|normal|orm.png)
    tex_dir = os.path.join(OUT, "Textures")
    os.makedirs(tex_dir, exist_ok=True)
    seen = set()
    for o in objs:
        if o.type != "MESH":
            continue
        for slot in o.material_slots:
            m = slot.material
            if not m or not m.use_nodes:
                continue
            for nd in m.node_tree.nodes:
                img = getattr(nd, "image", None)
                if nd.type != "TEX_IMAGE" or not img or img.name in seen:
                    continue
                seen.add(img.name)
                kind = "normal" if img.name.lower().startswith("normal") else "orm" if img.name.lower().startswith("orm") else "color"
                img.file_format = "PNG"
                img.save(filepath=os.path.join(tex_dir, "%s_%s.png" % (name, kind)))
    done.append("%s (%d objects, %d selected, %.2f MB, textures %s)" % (name, len(objs), sum(o.select_get() for o in objs), os.path.getsize(path) / 1e6, len(seen)))
print("EXPORTED", len(done)); print("\n".join(done))

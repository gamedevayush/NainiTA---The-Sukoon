import bpy, json
out = {}
for o in bpy.data.objects:
    ad = o.animation_data
    if ad and ad.action:
        act = ad.action
        curves = []
        fcs = []
        try:
            fcs = list(act.fcurves)
        except Exception:
            for layer in getattr(act, 'layers', []):
                for strip in layer.strips:
                    for bag in getattr(strip, 'channelbags', []):
                        fcs += list(bag.fcurves)
        for fc in fcs:
            curves.append([fc.data_path, fc.array_index, [(round(k.co[0], 1), round(k.co[1], 3)) for k in fc.keyframe_points]])
        out[o.name] = {'action': act.name, 'collection': [c.name for c in o.users_collection], 'curves': curves}
print("ANIM", json.dumps(out))
print("FRAMES", bpy.context.scene.frame_start, bpy.context.scene.frame_end, bpy.context.scene.frame_current)

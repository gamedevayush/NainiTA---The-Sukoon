"""Are the door seams in the geometry? (owner, 21 Nov 2026: cut the car doors only "agar perfect kat sko to").
Renders each named vehicle's Body from its right side, untextured (Workbench, flat grey, cavity on so grooves and panel
gaps show as dark lines), 1600 px, to <out>/seams_<name>.png. A door that can be cut cleanly shows its outline as a
groove; a Tripo body whose seams are only painted shows a smooth side.
  blender -b NewVehicles_v06.blend --python-exit-code 1 -P diag/door_seams.py -- <out_dir> car_hatch_small car_sedan_compact
"""
import bpy, sys, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out, names = argv[0], argv[1:]
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading
sh.light = "STUDIO"; sh.color_type = "SINGLE"; sh.single_color = (0.75, 0.75, 0.75)
sh.show_cavity = True; sh.cavity_type = "BOTH"; sh.curvature_ridge_factor = 2.0; sh.curvature_valley_factor = 2.0
sh.cavity_ridge_factor = 2.0; sh.cavity_valley_factor = 2.0
sc.render.resolution_x, sc.render.resolution_y = 1600, 900
for col in bpy.data.collections:
    if len(col.name) < 4 or col.name[3:] not in names:
        continue
    objs = [o for o in col.all_objects if o.type == "MESH"]
    body = next(o for o in objs if o.name.split(".")[0] in ("Body", col.name))
    # only this vehicle shows
    for o in sc.objects:
        o.hide_render = o not in objs
    pts = [body.matrix_world @ Vector(c) for c in body.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (lo + hi) / 2
    cam = bpy.data.objects.get("SeamCam") or bpy.data.objects.new("SeamCam", bpy.data.cameras.new("SeamCam"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = (hi.y - lo.y) * 1.08
    # the vehicle's right side is Blender -X (front is -Y, left is +X); look along +X from the right
    cam.location = (lo.x - 6.0, c.y, c.z)
    cam.rotation_euler = (1.5708, 0.0, -1.5708)
    sc.camera = cam
    sc.render.filepath = os.path.join(out, "seams_%s.png" % col.name[3:])
    bpy.ops.render.render(write_still=True)
    print("RENDERED", sc.render.filepath, "verts", len(body.data.vertices))

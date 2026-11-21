"""
A check sheet of the player in every outfit (front, back, side), rendered with Workbench in Blender, headless:
  blender -b --factory-startup --python-exit-code 1 -P Tools/outfits/render_outfits.py -- <model.fbx> <textures dir> <out dir> <ids...>
An id "base" renders the model's own texture (Assets/Nainital/Characters/Textures/<model>.jpg).
"""
import bpy, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
FBX, TEXDIR, OUTDIR, IDS = argv[0], argv[1], argv[2], argv[3:]
BASE = os.path.join(os.path.dirname(os.path.dirname(FBX)), "Textures", os.path.splitext(os.path.basename(FBX))[0] + ".jpg")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
for o in meshes:
    o.hide_render = o.name != "Body"
body = next(o for o in meshes if o.name == "Body")

mat = bpy.data.materials.new("Outfit")
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes.get("Principled BSDF")
img_node = nt.nodes.new("ShaderNodeTexImage")
nt.links.new(img_node.outputs["Color"], bsdf.inputs["Base Color"])
body.data.materials.clear()
body.data.materials.append(mat)

sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 300, 520
sc.render.film_transparent = False
sc.world = bpy.data.worlds.new("W")
sc.world.color = (0.55, 0.6, 0.65)

bb = [body.matrix_world @ Vector(c) for c in body.bound_box]
zmin, zmax = min(v.z for v in bb), max(v.z for v in bb)
cx = sum(v.x for v in bb) / 8
cy = sum(v.y for v in bb) / 8
hgt = zmax - zmin
cam_data = bpy.data.cameras.new("Cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = hgt * 1.08
cam = bpy.data.objects.new("Cam", cam_data)
sc.collection.objects.link(cam)
sc.camera = cam

views = {"front": 0.0, "side": 90.0, "back": 180.0}
for oid in IDS:
    path = BASE if oid == "base" else os.path.join(TEXDIR, oid + ".jpg")
    img_node.image = bpy.data.images.load(path, check_existing=True)
    for vname, ang in views.items():
        a = math.radians(ang)
        # Tripo / our rig: the character faces -Y in Blender
        d = Vector((math.sin(a), -math.cos(a), 0.0))
        cam.location = Vector((cx, cy, zmin + hgt * 0.5)) + d * 6.0
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(OUTDIR, f"{oid}_{vname}.png")
        bpy.ops.render.render(write_still=True)
print("[render_outfits] done")

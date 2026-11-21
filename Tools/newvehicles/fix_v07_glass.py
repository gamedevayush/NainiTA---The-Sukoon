"""The auto's windscreen out of its Body as its own see-through part (owner, 21 Nov 2026: "auto me seesha alag krna
pdega..andar ka dikh nahi raha"). Tripo painted the windscreen opaque on the Body: a thin slab (outer face leaning back,
normal up-and-forward; inner face 1-2 cm behind it) at Unity z 0.93-1.08, y 1.1-1.55 (diag/glass_rays.py). The faces
of the Body whose centre lies in the box and whose normal runs along the vehicle (|n.z| >= MIN_NZ) are split into a new
object "Glass" with its own material "Glass" (Unity: VehicleSetupWindow.FixMaterials gives it the transparent
M_VehicleGlass). Then rays from the front must meet Glass first. Saves as NewVehicles_v07.blend (new file per rebuild).
  blender -b NewVehicles_v06.blend --python-exit-code 1 -P fix_v07_glass.py
"""
import bpy, bmesh, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree

# the blend may have been saved in edit mode (the tuktuk's mesh was): mesh edits need object mode
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    try:
        bpy.ops.object.mode_set(mode="OBJECT")
    except RuntimeError:
        pass

# vehicle: Unity-space box of the windscreen's face centres, and the least |normal.z|
# (first try took the roof lip and the cab's curved front corners too: kept inside the pillars and under the lip, 21 Nov)
JOBS = {"tuktuk_auto": ((-0.50, 0.50), (1.08, 1.52), (0.86, 1.12), 0.6)}   # 1.58 bit teeth into the roof lip
MAX_NX = 0.45   # a face turned this much sideways is the cab's corner, not the glass

for name, (bx, by, bz, min_nz) in JOBS.items():
    col = next(c for c in bpy.data.collections if c.name[3:] == name)
    objs = [o for o in col.all_objects if o.type == "MESH"]
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
    cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
    U = lambda w: Vector((-(w.x - cx), w.z, -(w.y - cy)))
    B = lambda u: Vector((cx - u.x, cy - u.z, u.y))
    Un = lambda n: Vector((-n.x, n.z, -n.y))
    if any(o.name.split(".")[0] == "Glass" for o in objs):
        print("==", name, "already has Glass"); continue
    body = next(o for o in objs if o.name.split(".")[0] in ("Body", col.name))
    mw = body.matrix_world; rot = mw.to_3x3().normalized()
    me = body.data
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    pick = []
    for f in bm.faces:
        c = U(mw @ f.calc_center_median()); n = Un(rot @ f.normal).normalized()
        if bx[0] <= c.x <= bx[1] and by[0] <= c.y <= by[1] and bz[0] <= c.z <= bz[1] and abs(n.z) >= min_nz and abs(n.x) < MAX_NX:
            pick.append(f)
    print("== %s: %d faces of %d to Glass" % (name, len(pick), len(bm.faces)))
    # the glass: a copy of those faces (same UVs) in a new mesh, then gone from the body
    gm = me.copy(); gm.name = "Glass"
    gbm = bmesh.new(); gbm.from_mesh(gm); gbm.faces.ensure_lookup_table()
    keep = set(f.index for f in pick)
    bmesh.ops.delete(gbm, geom=[f for f in gbm.faces if f.index not in keep], context="FACES")
    gbm.to_mesh(gm); gbm.free()
    bmesh.ops.delete(bm, geom=pick, context="FACES_ONLY")
    bm.to_mesh(me); bm.free(); me.update()
    gm.materials.clear()
    mat = bpy.data.materials.get("Glass") or bpy.data.materials.new("Glass")
    mat.diffuse_color = (0.55, 0.65, 0.72, 0.3)
    gm.materials.append(mat)
    glass = bpy.data.objects.new("Glass", gm)
    glass.matrix_world = mw
    col.objects.link(glass)
    if body.parent: glass.parent = body.parent; glass.matrix_world = mw
    # check: rays from the front meet Glass first
    trees = []
    for o in [body, glass]:
        t = bmesh.new(); t.from_mesh(o.data); t.transform(o.matrix_world)
        trees.append((o.name.split(".")[0], BVHTree.FromBMesh(t)))
    first = {}
    for y in (1.15, 1.25, 1.35, 1.45, 1.55):
        for x in (-0.35, 0.0, 0.35):
            o = B(Vector((x, y, 3.0))); best = None
            for nm, t in trees:
                h = t.ray_cast(o, Vector((0, 1, 0)), 6.0)
                if h[0] is not None and (best is None or (h[0] - o).length < best[0]): best = ((h[0] - o).length, nm)
            first[best[1] if best else "-"] = first.get(best[1] if best else "-", 0) + 1
    print("   rays from the front, first hit:", first, "| Glass verts", len(gm.vertices), "Body verts", len(me.vertices))

out = os.path.join(os.path.dirname(bpy.data.filepath), "NewVehicles_v07.blend")
bpy.ops.wm.save_as_mainfile(filepath=out)
print("SAVED", out)

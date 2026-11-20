"""
NewVehicles_v05.blend -> NewVehicles_v06.blend: the fixes of docs/VEHICLES.md section 9.2 (20 Nov hand-off), done on
21 Nov 2026 without v04 (old blends are not kept in git):
  1. Auto Keying off.
  2. The two bus doors back in their openings. On 20 Nov their origins were moved onto a "hinge" and the meshes the other
     way, but the doors' animation keys hold their location, so the meshes ended up shifted. The door was cut out of the
     body, so the door's open edges must lie on the body's: the shift is found by matching them (grid search, then ICP)
     and taken back out of the door mesh. The tourist coach's door also had 2 faces from the far side of the coach: they
     go into its Body.
  3. Bike 13's handlebar: its one key pinned the rotation to identity, which undid the raked frame (the mesh was
     counter-rotated for it). The key goes and the raked rotation comes back, so the mesh is where the owner made it;
     the front wheel (its child) goes back on the wheel centre below the steering axis.
  4. Every other action with keys at one frame only: the transform at that frame is kept, the action removed.
  5. Negative scales (the owner's right-hand-drive mirrors) baked into the meshes: world shape unchanged, scale positive.
  6. Right-hand drive: checked by renders (diag/windscreen_check.py, diag/cabin_check.py) - every car and bus whose
     cabin can be seen (00, 01, 02, 03, 06, 07, 09, 11) has its steering wheel on the right; 04, 05, 08, 10 have opaque
     glass. Nothing more to mirror. Both buses have their passenger door on the right (-X) too: Door_FR is the right name.
  blender -b NewVehicles_v05.blend --python-exit-code 1 -P fix_v06.py -- NewVehicles_v06.blend
"""
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree

out = sys.argv[sys.argv.index("--") + 1]
sc = bpy.context.scene
log = []


def say(*a):
    s = " ".join(str(x) for x in a)
    log.append(s)
    print("FIX", s)


def col_name(o):
    return o.users_collection[0].name if o.users_collection else ""


# ---------------------------------------------------------------- 1. Auto Keying off
sc.tool_settings.use_keyframe_insert_auto = False
say("auto keying off")


def key_frames(o):
    ad = o.animation_data
    if not ad or not ad.action:
        return set()
    return {round(k.co.x, 3) for fc in ad.action.fcurves for k in fc.keyframe_points}


def world_verts(o, frame=None):
    if frame is not None:
        sc.frame_set(int(frame))
    M = o.matrix_world
    return [M @ v.co for v in o.data.vertices]


# ---------------------------------------------------------------- 2. the bus doors
DOORS = {"Door_FR": ("00_bus_roadways", 1), "Door_FR.001": ("01_bus_tourist", 0)}


def body_of(cname):
    col = bpy.data.collections[cname]
    return next(o for o in col.all_objects if o.type == "MESH" and o.name.split(".")[0] == "Body")


for dname, (cname, closed) in DOORS.items():
    door = bpy.data.objects[dname]
    body = body_of(cname)
    sc.frame_set(closed)
    Md, Mb = door.matrix_world.copy(), body.matrix_world.copy()
    bw = [Mb @ v.co for v in body.data.vertices]
    kd = KDTree(len(bw))
    for i, p in enumerate(bw):
        kd.insert(p, i)
    kd.balance()
    bm = bmesh.new()
    bm.from_mesh(door.data)
    side = None
    edge = []
    for v in bm.verts:
        if any(e.is_boundary for e in v.link_edges):
            w = Md @ v.co
            edge.append(w)
    # the door's own side of the bus (drop the stray faces from the far side when matching)
    xs = sorted(p.x for p in edge)
    near_side = xs[len(xs) // 2]
    edge = [p for p in edge if (p.x < 0) == (near_side < 0)]
    bm.free()

    sample = edge[::max(1, len(edge) // 80)]

    def score(t, cap=0.15):
        return sum(min(kd.find(p + t)[2], cap) for p in sample) / len(sample)

    # a wide 3D grid at 5 cm, then 1 cm around the best (diag/door_fit.py; renders in diag/door_try.py confirmed it:
    # roadways (-0.28, -0.41, 0.07), coach (0.12, -0.45, 0.16))
    best = (score(Vector()), Vector())
    for ix in range(-30, 31):
        for iy in range(-30, 31):
            for iz in range(-4, 5):
                t = Vector((ix * 0.05, iy * 0.05, iz * 0.05))
                s = score(t)
                if s < best[0]:
                    best = (s, t)
    t0 = best[1].copy()
    for ix in range(-6, 7):
        for iy in range(-6, 7):
            for iz in range(-6, 7):
                t = t0 + Vector((ix, iy, iz)) * 0.01
                s = score(t)
                if s < best[0]:
                    best = (s, t)
    t = best[1]
    before, after = score(Vector()), score(t)
    matched = sum(1 for p in edge if kd.find(p + t)[2] < 0.01) / len(edge)
    # take the shift out of the mesh, in the door's local space at the closed frame
    dl = Md.inverted().to_3x3() @ t
    door.data.transform(Matrix.Translation(dl))
    door.data.update()
    say("%s: moved %.3f %.3f %.3f m (edge gap %.3f -> %.4f m, %.0f%% of its edge on the body's within 1 cm)"
        % (dname, t.x, t.y, t.z, before, after, matched * 100))

    if dname == "Door_FR.001":
        # stray faces from the coach's far side: into the Body
        sc.frame_set(closed)
        Md = door.matrix_world.copy()
        bm = bmesh.new()
        bm.from_mesh(door.data)
        bm.faces.ensure_lookup_table()
        stray = [f for f in bm.faces if ((Md @ f.calc_center_median()).x < 0) != (near_side < 0)]
        say("%s: %d stray faces on the far side" % (dname, len(stray)))
        if stray:
            # copy them into the body (world -> body local), then delete them from the door
            bb = bmesh.new()
            bb.from_mesh(body.data)
            uv_d = bm.loops.layers.uv.active
            uv_b = bb.loops.layers.uv.active
            Mbi = body.matrix_world.inverted() @ Md
            for f in stray:
                vs = [bb.verts.new(Mbi @ v.co) for v in f.verts]
                try:
                    nf = bb.faces.new(vs)
                except ValueError:
                    continue
                nf.material_index = min(f.material_index, max(len(body.data.materials) - 1, 0))
                if uv_d and uv_b:
                    for lo, ln in zip(f.loops, nf.loops):
                        ln[uv_b].uv = lo[uv_d].uv
                if Mbi.determinant() < 0:
                    nf.normal_flip()
            bb.to_mesh(body.data)
            bb.free()
            bmesh.ops.delete(bm, geom=stray, context="FACES")
            bm.to_mesh(door.data)
        bm.free()
        door.data.update()

# ---------------------------------------------------------------- 3. bike 13's handlebar
hb = bpy.data.objects.get("Handlebar.001")
if hb:
    wheel = next((c for c in hb.children if c.name.startswith("Wheel_F")), None)
    sc.frame_set(0)
    # where the front wheel's centre must be: the handlebar's origin is the wheel centre + d * 0.44
    a = math.radians(24.0)
    d = Vector((0.0, math.sin(a), math.cos(a)))
    hb_origin = hb.matrix_world.translation.copy()
    target_centre = hb_origin - d * 0.44
    wheel_before = wheel.matrix_world.copy() if wheel else None
    wheel_centre_before = None
    if wheel:
        pts = [wheel.matrix_world @ Vector(b) for b in wheel.bound_box]
        wheel_centre_before = sum(pts, Vector()) / 8
    hb.animation_data_clear()
    X = Vector((1.0, 0.0, 0.0))
    Z = d.normalized()
    Y = Z.cross(X)
    R = Matrix((X, Y, Z)).transposed()
    hb.rotation_mode = "QUATERNION"
    hb.rotation_quaternion = R.to_quaternion()
    bpy.context.view_layer.update()
    if wheel:
        wheel.animation_data_clear()
        new = Matrix.Translation(target_centre - wheel_centre_before) @ wheel_before
        wheel.matrix_world = new
        bpy.context.view_layer.update()
        pts = [wheel.matrix_world @ v.co for v in wheel.data.vertices]
        c = sum(pts, Vector()) / len(pts)
        say("bike 13: handlebar raked again; front wheel centre moved %.3f m to (%.3f, %.3f, %.3f), lowest point z %.3f"
            % ((target_centre - wheel_centre_before).length, c.x, c.y, c.z, min(p.z for p in pts)))

# ---------------------------------------------------------------- 4. one-frame actions
cleared = []
for o in bpy.data.objects:
    if o.name in DOORS:
        continue
    fr = key_frames(o)
    if len(fr) == 1:
        sc.frame_set(int(list(fr)[0]))
        mb = o.matrix_basis.copy()
        o.animation_data_clear()
        o.matrix_basis = mb
        cleared.append(o.name)
    elif len(fr) > 1:
        say("KEPT animation on", o.name, sorted(fr))
say("one-frame actions removed:", len(cleared), ", ".join(cleared))
sc.frame_set(0)
bpy.context.view_layer.update()

# ---------------------------------------------------------------- 5. negative scales into the meshes
for o in bpy.data.objects:
    if o.type != "MESH":
        continue
    # the object's own scale (decompose() would turn one negative axis into three plus a 180 deg turn)
    neg = [i for i in range(3) if o.scale[i] < 0]
    if not neg:
        continue
    if o.data.users > 1:
        o.data = o.data.copy()
    kids = {c: c.matrix_world.copy() for c in o.children}
    D = Matrix.Identity(4)
    for i in neg:
        D[i][i] = -1.0
    o.data.transform(D)
    if len(neg) % 2 == 1:
        o.data.flip_normals()
    o.data.update()
    ad = o.animation_data
    keyed = False
    if ad and ad.action:   # the tourist door: its scale keys carry the mirror
        for fc in ad.action.fcurves:
            if fc.data_path == "scale" and fc.array_index in neg:
                for k in fc.keyframe_points:
                    k.co.y = abs(k.co.y)
                    k.handle_left.y = abs(k.handle_left.y)
                    k.handle_right.y = abs(k.handle_right.y)
                keyed = True
    s = list(o.scale)
    for i in neg:
        s[i] = abs(s[i])
    o.scale = s
    bpy.context.view_layer.update()
    for c, m in kids.items():
        c.matrix_world = m
    say("mirror baked: %s (%s) axes %s%s" % (o.name, col_name(o), neg, ", scale keys made positive" if keyed else ""))

# ---------------------------------------------------------------- the van's rear wheels
# (found on 21 Nov: 10_car_van's Wheel_R - both rear wheels in one mesh - sat 0.75 m to the left: its one-frame key held
# location x 0.754, so the right arch showed a black disc and the left wheel stood out of the body). Slide it back so
# the axle is centred under the front wheels'.
van = bpy.data.collections.get("10_car_van")
if van:
    wr = next((o for o in van.all_objects if o.name.split(".")[0] == "Wheel_R"), None)
    fronts = [o for o in van.all_objects if o.name.split(".")[0] in ("Wheel_FL", "Wheel_FR")]
    if wr and fronts:
        def centre_x(o):
            xs = [(o.matrix_world @ v.co).x for v in o.data.vertices]
            return (min(xs) + max(xs)) / 2
        mid = sum(centre_x(o) for o in fronts) / len(fronts)
        dx = mid - centre_x(wr)
        if abs(dx) > 0.05:
            wr.matrix_world = Matrix.Translation(Vector((dx, 0, 0))) @ wr.matrix_world
            bpy.context.view_layer.update()
            xs = [(wr.matrix_world @ v.co).x for v in wr.data.vertices]
            say("van: Wheel_R slid %.3f m in x; it spans %.2f..%.2f now (front track centre %.3f)" % (dx, min(xs), max(xs), mid))

# ---------------------------------------------------------------- checks
sc.frame_set(0)
bpy.context.view_layer.update()
bad = [o.name for o in bpy.data.objects if o.type == "MESH" and o.matrix_world.determinant() < 0]
say("negative determinants left:", bad)
for c in bpy.data.collections:
    if not (len(c.name) > 3 and c.name[:2].isdigit()):
        continue
    wheels = [o for o in c.all_objects if o.name.startswith("Wheel")]
    if not wheels:
        continue
    info = []
    for w in wheels:
        pts = [w.matrix_world @ v.co for v in w.data.vertices]
        lo = [min(p[i] for p in pts) for i in range(3)]
        hi = [max(p[i] for p in pts) for i in range(3)]
        info.append("%s(%.2f,%.2f,%.2f low %.3f)" % (w.name.split(".")[0], (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2, lo[2]))
    say("wheels", c.name, " ".join(info))
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(out), compress=True)
say("saved", out)

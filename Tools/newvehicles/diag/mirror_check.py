import bpy, json
import numpy as np
def bb(o):
    co = np.array([o.matrix_world @ v.co for v in o.data.vertices]); return co.min(0), co.max(0)
res = {}
for col in sorted([c for c in bpy.data.collections if c.name[:2].isdigit()], key=lambda c: c.name):
    ms = [o for o in col.all_objects if o.type == 'MESH']
    wheels = [o for o in ms if o.name.startswith('Wheel')]
    body = [o for o in ms if o.name.startswith('Body')][0]
    wx = [((bb(w)[0] + bb(w)[1]) / 2)[0] for w in wheels]
    cx = float(np.mean([x for x in wx])) if wx else 0.0
    lo, hi = bb(body)
    r = {'wheels_cx': round(cx, 3), 'body_x': [round(float(lo[0]), 2), round(float(hi[0]), 2)], 'body_cx': round(float((lo[0] + hi[0]) / 2), 3),
         'body_det': round(body.matrix_world.determinant(), 3)}
    for d in [o for o in ms if o.name.startswith('Door')]:
        dl, dh = bb(d); r[d.name] = {'x': [round(float(dl[0]), 2), round(float(dh[0]), 2)], 'y': [round(float(dl[1]), 2), round(float(dh[1]), 2)], 'det': round(d.matrix_world.determinant(), 3)}
    res[col.name] = r
print("MIR", json.dumps(res))

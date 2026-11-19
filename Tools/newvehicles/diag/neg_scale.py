import bpy, json
res = {}
for col in sorted([c for c in bpy.data.collections if c.name[:2].isdigit()], key=lambda c: c.name):
    rows = []
    for o in col.all_objects:
        d = o.matrix_world.determinant()
        if d < 0 or any(v < 0 for v in o.scale):
            rows.append((o.name, [round(v, 3) for v in o.scale], round(d, 3), o.parent.name if o.parent else ""))
    res[col.name] = rows
print("NEG", json.dumps(res))

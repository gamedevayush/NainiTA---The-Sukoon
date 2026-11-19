import bpy, json
import numpy as np
out = {}
for cn in ('00_bus_roadways', '01_bus_tourist'):
    col = bpy.data.collections[cn]
    for o in col.all_objects:
        if o.type == 'MESH' and o.name.lower().startswith('door'):
            co = np.array([o.matrix_world @ v.co for v in o.data.vertices])
            out[cn + '/' + o.name] = {'x': [round(float(co[:, 0].min()), 3), round(float(co[:, 0].max()), 3)],
                                     'y': [round(float(co[:, 1].min()), 3), round(float(co[:, 1].max()), 3)],
                                     'origin': [round(v, 3) for v in o.matrix_world.translation], 'scale': [round(v, 3) for v in o.scale],
                                     'loc': [round(v, 3) for v in o.location], 'parent': o.parent.name if o.parent else '',
                                     'delta': [round(v, 3) for v in o.delta_location], 'constraints': len(o.constraints), 'mods': [m.type for m in o.modifiers]}
print("DOOR", json.dumps(out))

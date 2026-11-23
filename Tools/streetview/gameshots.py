"""route.json + sv/sv_meta.json -> Bridge/batch.txt: one game camera per Street View pano, left and right, at the
game road's centre line nearest the pano (2.5 m up, level), plus views.json with the offsets."""
import json, math, os, sys

d = sys.argv[1]
route = json.load(open(os.path.join(d, 'route.json')))
meta = json.load(open(os.path.join(d, 'sv', 'sv_meta.json')))
CLAT, CLON = 29.3905, 79.4550
M_LAT = 110_860.0
M_LON = 111_320.0 * math.cos(math.radians(CLAT))
line = route['line']


def snap(x, z):
    best = (1e9, x, z)
    for (ax, az), (bx, bz) in zip(line, line[1:]):
        dx, dz = bx - ax, bz - az
        L2 = dx * dx + dz * dz or 1e-9
        t = max(0, min(1, ((x - ax) * dx + (z - az) * dz) / L2))
        px, pz = ax + t * dx, az + t * dz
        dd = math.hypot(x - px, z - pz)
        if dd < best[0]:
            best = (dd, px, pz)
    return best


specs, views = [], []
pick = set(map(int, sys.argv[2].split(','))) if len(sys.argv) > 2 else None
for i, s in enumerate(route['samples']):
    m = meta.get(str(i))
    if pick is not None and i not in pick:
        continue
    if not m or m.get('none'):
        x, z, off = s['x'], s['z'], None
    else:
        rx = (m['lng'] - CLON) * M_LON
        rz = (m['lat'] - CLAT) * M_LAT
        off, x, z = snap(rx, rz)
        if off > 25:          # far from the game's road: keep the game road's own sample point
            x, z = s['x'], s['z']
    for tag, dh in (('L', -90), ('R', 90)):
        h = math.radians((s['yaw'] + dh) % 360)
        lx, lz = x + math.sin(h), z + math.cos(h)
        specs.append(f"kd_{i:03d}_{tag};{x:.2f},2.5,{z:.2f};{lx:.2f},2.5,{lz:.2f};11;g")
    views.append({'i': i, 'm': s['m'], 'x': round(x, 1), 'z': round(z, 1), 'yaw': s['yaw'], 'off': None if off is None else round(off, 1),
                  'near': s['near'], 'date': (m or {}).get('date', ''), 'sv': bool(m and not m.get('none'))})
bridge = r'D:\NainiTA---The-Sukoon\Bridge'
os.makedirs(bridge, exist_ok=True)
open(os.path.join(bridge, 'batch.txt'), 'w').write('shot ' + '|'.join(specs) + '\n')
json.dump(views, open(os.path.join(d, 'views.json'), 'w'), indent=0)
offs = [v['off'] for v in views if v['off'] is not None]
print(f'{len(specs)} shots; Street View panos {len(offs)}/{len(views)}; game road off the car track: '
      f'median {sorted(offs)[len(offs) // 2]:.1f} m, max {max(offs):.1f} m')
for v in views:
    if v['off'] is not None and v['off'] > 8:
        print(f"  #{v['i']:03d} {v['m']} m: game road {v['off']} m from the Street View car  {v['near']}")

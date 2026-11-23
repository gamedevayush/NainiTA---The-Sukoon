"""Comparison report for the owner: D:\\NainiTA---The-Sukoon\\Shots\\kd_compare\\ (gitignored)
  locations.csv  every shot: game camera x,y,z + facing, Street View link (same pano, same facing)
  index.html     Street View | game, left and right, per point, with the links and the notes
usage: python -I report.py <kd dir> <unity log> [notes.json]"""
import csv, html, json, math, os, re, shutil, sys
from PIL import Image

d, log = sys.argv[1], sys.argv[2]
notes = json.load(open(sys.argv[3], encoding='utf-8')) if len(sys.argv) > 3 and os.path.exists(sys.argv[3]) else {}
out = r'D:\NainiTA---The-Sukoon\Shots\kd_compare'
img = os.path.join(out, 'img')
os.makedirs(img, exist_ok=True)
route = json.load(open(os.path.join(d, 'route.json')))
views = {v['i']: v for v in json.load(open(os.path.join(d, 'views.json')))}
meta = json.load(open(os.path.join(d, 'sv', 'sv_meta.json')))
pos = {}
for line in open(log, encoding='utf-8', errors='ignore'):
    m = re.search(r'\[Nainital\] shot (kd_\d+_[LR]) pos=([-\d.]+),([-\d.]+),([-\d.]+) look=([-\d.]+),([-\d.]+),([-\d.]+)', line)
    if m:
        pos[m.group(1)] = tuple(float(v) for v in m.groups()[1:])

rows = []
for i, s in enumerate(route['samples']):
    v, m = views.get(i), meta.get(str(i), {})
    for tag in 'LR':
        name = f'kd_{i:03d}_{tag}'
        h = ((s['yaw'] + (-90 if tag == 'L' else 90)) % 360 + 360) % 360
        p = pos.get(name)
        sv = (f"https://www.google.com/maps/@?api=1&map_action=pano&pano={m['pano']}&heading={h:.0f}&pitch=0&fov=90"
              if m.get('pano') else '')
        rows.append({'point': i, 'side': 'left' if tag == 'L' else 'right', 'm_from_dsa': s['m'], 'near': s['near'],
                     'game_x': f'{p[0]:.1f}' if p else '', 'game_y': f'{p[1]:.1f}' if p else '', 'game_z': f'{p[2]:.1f}' if p else '',
                     'facing_deg': f'{h:.0f}', 'sv_lat': m.get('lat', ''), 'sv_lng': m.get('lng', ''), 'sv_date': m.get('date', '').replace('Image capture: ', ''),
                     'game_road_off_m': '' if not v or v['off'] is None else v['off'], 'streetview': sv,
                     'note': notes.get(f'{i}{tag}', notes.get(str(i), ''))})
        for src, dst in ((os.path.join(d, 'sv', f'real_{i:03d}_{tag}.jpg'), f'sv_{i:03d}_{tag}.jpg'),
                         (os.path.join(r'D:\NainiTA---The-Sukoon\Shots', f'{name}.png'), f'game_{i:03d}_{tag}.jpg'),
                         (os.path.join(r'D:\NainiTA---The-Sukoon\Shots\kd_before', f'{name}.png'), f'before_{i:03d}_{tag}.jpg')):
            if os.path.exists(src):
                Image.open(src).convert('RGB').resize((640, 360)).save(os.path.join(img, dst), quality=84)

with open(os.path.join(out, 'locations.csv'), 'w', newline='', encoding='utf-8-sig') as f:
    w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
    w.writeheader()
    w.writerows(rows)

E = html.escape
parts = ['<!doctype html><meta charset="utf-8"><title>DSA ground to Khurpatal</title><style>'
         'body{background:#111;color:#ddd;font:14px system-ui;margin:16px}h1{font-size:20px}'
         '.pt{margin:18px 0 30px;border-top:1px solid #333;padding-top:10px}.hd{color:#ffd36b;font-weight:600}'
         '.g{display:grid;grid-template-columns:1fr 1fr;gap:6px;margin-top:6px}.g img{width:100%;display:block}'
         '.cap{font-size:12px;color:#aaa;margin:2px 0 6px}a{color:#7cc7ff}.note{background:#2a2410;color:#ffe9a8;padding:6px 8px;margin-top:6px;border-radius:4px}'
         'code{color:#9fe}</style>',
         f'<h1>DSA ground (Flats) to Khurpatal: Street View vs game, every {route["samples"][1]["m"]} m ({route["length"]:.0f} m)</h1>'
         '<p>Each point, per side: Street View (Aug 2022 car), the game now, and the game before this pass, all from the same spot. Left / right of the direction '
         'of travel (towards Khurpatal). Game position = the camera, 2.5 m above the road; facing in compass degrees (0 = north = +z, 90 = east = +x).</p>']
by = {}
for r in rows:
    by.setdefault(r['point'], []).append(r)
for i, rs in by.items():
    r0 = rs[0]
    note = notes.get(str(i), '')
    parts.append(f'<div class="pt" id="p{i}"><div class="hd">#{i:03d} &nbsp; {r0["m_from_dsa"]} m from DSA ground &nbsp; {E(r0["near"])}'
                 f' &nbsp; <span style="color:#aaa;font-weight:400">game road {r0["game_road_off_m"]} m from the SV car</span></div><div class="g">')
    for r in rs:
        t = 'L' if r['side'] == 'left' else 'R'
        parts.append(f'<div><div class="cap">{r["side"].upper()} - Street View: '
                     + (f'<a href="{r["streetview"]}" target="_blank">open (facing {r["facing_deg"]}°)</a> {r["sv_lat"]},{r["sv_lng"]}' if r['streetview'] else 'none here')
                     + f'<br>game: <code>{r["game_x"]}, {r["game_y"]}, {r["game_z"]}</code> facing {r["facing_deg"]}°</div>'
                     f'<img src="img/sv_{i:03d}_{t}.jpg" loading="lazy"><div class="cap">game now (after the Street View pass)</div>'
                     f'<img src="img/game_{i:03d}_{t}.jpg" loading="lazy"><div class="cap">game before (23 Nov, morning)</div>'
                     f'<img src="img/before_{i:03d}_{t}.jpg" loading="lazy" style="opacity:.85">'
                     + (f'<div class="note">{E(notes.get(f"{i}{t}", ""))}</div>' if notes.get(f'{i}{t}') else '') + '</div>')
    parts.append('</div>' + (f'<div class="note">{E(note)}</div>' if note else '') + '</div>')
open(os.path.join(out, 'index.html'), 'w', encoding='utf-8').write(''.join(parts))
print(len(rows), 'rows ->', out)

"""Compact review sheets: one row per point: SV left | game left || SV right | game right."""
import json, os, sys
from PIL import Image, ImageDraw

d = sys.argv[1]
per = int(sys.argv[2]) if len(sys.argv) > 2 else 6
shots = r'D:\NainiTA---The-Sukoon\Shots'
out = os.path.join(d, 'sheets2')
os.makedirs(out, exist_ok=True)
views = json.load(open(os.path.join(d, 'views.json')))
VW, VH, LB, GAP = 400, 225, 20, 8
for s in range(0, len(views), per):
    g = views[s:s + per]
    sh = Image.new('RGB', (VW * 4 + GAP, len(g) * (VH + LB)), (12, 12, 12))
    dr = ImageDraw.Draw(sh)
    for k, v in enumerate(g):
        i, y = v['i'], k * (VH + LB)
        dr.text((4, y + 4), f"#{i:03d} {v['m']} m  {v['near']}  off {v['off']} m      [SV L | game L]      [SV R | game R]", fill=(255, 220, 110))
        for c, (p) in enumerate((os.path.join(d, 'sv', f'real_{i:03d}_L.jpg'), os.path.join(shots, f'kd_{i:03d}_L.png'),
                                 os.path.join(d, 'sv', f'real_{i:03d}_R.jpg'), os.path.join(shots, f'kd_{i:03d}_R.png'))):
            if os.path.exists(p):
                sh.paste(Image.open(p).convert('RGB').resize((VW, VH)), (c * VW + (GAP if c >= 2 else 0), y + LB))
    sh.save(os.path.join(out, f's_{s // per + 1:02d}_{g[0]["i"]:03d}-{g[-1]["i"]:03d}.jpg'), quality=80)
print('sheets ->', out)

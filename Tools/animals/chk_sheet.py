"""Contact sheet of rig_animal.py's check frames: python chk_sheet.py <outdir> <sheet.jpg>"""
import sys, os
from PIL import Image, ImageDraw
d, out = sys.argv[1], sys.argv[2]
rows = [l.split("|") for l in open(os.path.join(d, "_chk_list.txt")).read().splitlines()]
ims = []
for p, lab in rows:
    im = Image.open(p).convert("RGB"); ImageDraw.Draw(im).text((4, 4), lab, fill=(255, 255, 0)); ims.append(im)
w, h = ims[0].size; cols = 4; r = (len(ims) + cols - 1) // cols
sheet = Image.new("RGB", (w * cols, h * r))
for i, im in enumerate(ims): sheet.paste(im, ((i % cols) * w, (i // cols) * h))
sheet.save(out, quality=85)

import sys, os
from PIL import Image, ImageDraw
TV = os.path.join(os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")), "Shots", "topview")
x0, z0, x1, z1, mpp, tile, nx, nz = open(os.path.join(TV, "topview.txt")).read().split()
x0, z0, mpp, tile, nx, nz = float(x0), float(z0), float(mpp), float(tile), int(nx), int(nz)
tp = int(round(tile / mpp)); W, H = nx * tp, nz * tp; Z1 = z0 + nz * tile
ours = Image.new("RGB", (W, H))
for iz in range(nz):
    for ix in range(nx):
        ours.paste(Image.open(os.path.join(TV, f"tile_{ix}_{iz}.png")).convert("RGB"), (ix * tp, (nz - 1 - iz) * tp))
cx, cz, half = float(sys.argv[1]), float(sys.argv[2]), float(sys.argv[3])
box = (int((cx - half - x0) / mpp), int((Z1 - (cz + half)) / mpp), int((cx + half - x0) / mpp), int((Z1 - (cz - half)) / mpp))
im = ours.crop(box).resize((1000, 1000), Image.LANCZOS)
d = ImageDraw.Draw(im)
sc = 1000 / (2 * half)
for gx in range(int(cx-half)//10*10, int(cx+half), 10):
    x = (gx - (cx - half)) * sc; d.line([(x, 0), (x, 6)], fill=(255,255,255))
    if gx % 50 == 0: d.text((x+2, 8), str(gx), fill=(255,255,255))
for gz in range(int(cz-half)//10*10, int(cz+half), 10):
    y = ((cz + half) - gz) * sc; d.line([(0, y), (6, y)], fill=(255,255,255))
    if gz % 50 == 0: d.text((8, y+2), str(gz), fill=(255,255,255))
im.save(sys.argv[4], quality=92)

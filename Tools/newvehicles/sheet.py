"""Contact sheet of vehicle images: python sheet.py <out.jpg> <folder> [first] [last]  (3 columns, names on top)."""
import os, sys, glob
from PIL import Image, ImageDraw
out, folder = sys.argv[1], sys.argv[2]
files = sorted(glob.glob(os.path.join(folder, "*.*")))
a = int(sys.argv[3]) if len(sys.argv) > 3 else 0
b = int(sys.argv[4]) if len(sys.argv) > 4 else len(files) - 1
files = [f for f in files if a <= int(os.path.basename(f)[:2]) <= b]
w, h, cols = 620, 346, 3
rows = (len(files) + cols - 1) // cols
sheet = Image.new("RGB", (w * cols, h * rows), (40, 40, 40))
for i, f in enumerate(files):
    im = Image.open(f).convert("RGB").resize((w, h))
    d = ImageDraw.Draw(im); n = os.path.splitext(os.path.basename(f))[0]
    d.rectangle((0, 0, 8 + 7 * len(n), 16), fill=(0, 0, 0)); d.text((4, 2), n, fill=(255, 255, 0))
    sheet.paste(im, ((i % cols) * w, (i // cols) * h))
sheet.save(out, quality=90)
print(out, len(files))

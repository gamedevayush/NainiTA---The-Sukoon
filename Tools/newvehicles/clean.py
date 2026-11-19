"""
Flow vehicle images -> Tripo-ready PNGs (19 Nov 2026).
raw\\NN_name.webp (1376x768) -> clean\\NN_name.png: Flow's sparkle watermark (bottom-right, fixed box measured on these
16:9 1K images: x 1256, y 648, 50 px) and any badge Flow still put on a vehicle (EXTRA, boxes found by eye on the image)
are inpainted with LaMa; everything else is left as it is. Already cleaned files are skipped (delete one to redo it).
Run with the google-flow-automation skill's venv (it has simple-lama-inpainting):
  "C:\\Users\\Ayush Singth\\.claude\\skills\\google-flow-automation\\venv\\Scripts\\python.exe" -I clean.py
"""
import os, glob
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
RAW, OUT = os.path.join(ROOT, "raw"), os.path.join(ROOT, "clean")
os.makedirs(OUT, exist_ok=True)
WATERMARK = (1256, 648, 50, 50)   # x, y, w, h on 1376x768
EXTRA = {
    "05_car_mpv_compact": [(214, 388, 44, 38)],   # chrome badge on the nose
}
PAD, CTX = 8, 160

lama = None


def inpaint(im, box, pad):
    global lama
    x0, y0 = box[0] - pad, box[1] - pad
    x1, y1 = box[0] + box[2] + pad, box[1] + box[3] + pad
    cx0, cy0 = max(0, x0 - CTX), max(0, y0 - CTX)
    cx1, cy1 = min(im.width, x1 + CTX), min(im.height, y1 + CTX)
    crop = im.crop((cx0, cy0, cx1, cy1))
    mask = np.zeros((crop.height, crop.width), np.uint8)
    mask[max(0, y0 - cy0):y1 - cy0, max(0, x0 - cx0):x1 - cx0] = 255
    if lama is None:
        from simple_lama_inpainting import SimpleLama
        lama = SimpleLama()
    fixed = lama(crop, Image.fromarray(mask)).convert("RGB").resize(crop.size)
    # paste only the masked area with a soft 6 px edge (pasting the whole window left a visible seam at its border)
    from PIL import ImageFilter
    soft = Image.fromarray(mask).filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(3))
    im.paste(fixed, (cx0, cy0), soft)


for src in sorted(glob.glob(os.path.join(RAW, "*.webp"))):
    name = os.path.splitext(os.path.basename(src))[0]
    dst = os.path.join(OUT, name + ".png")
    if os.path.exists(dst):
        continue
    im = Image.open(src).convert("RGB")
    assert im.size == (1376, 768), (name, im.size)
    inpaint(im, WATERMARK, PAD)
    for b in EXTRA.get(name, []):
        inpaint(im, b, 4)
    im.save(dst)
    print("clean", name, "+%d badge" % len(EXTRA.get(name, [])) if name in EXTRA else "")
print("done", len(glob.glob(os.path.join(OUT, "*.png"))), "in clean")

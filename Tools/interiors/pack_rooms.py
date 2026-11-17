"""Pack the Flow room photos into the room atlas the shaders read (NainitalRooms.hlsl).

raw/NN_<name>.png (1024 x 1024 from Google Flow, prompts.json order) -> the Flow sparkle in the lower-right corner
painted out with LaMa (loaded once) -> clean/NN_<name>.png -> 512 x 512 tiles in an 8 x 4 grid, room r at column r % 8,
row r // 8 counted from the BOTTOM (Unity's v runs up) -> Assets/Nainital/Data/Interiors/RoomPhotos.png, plus a
labelled sheet for review (rooms_sheet.jpg).

Run with the Flow skill's venv (it has simple-lama-inpainting):
  "C:/Users/Ayush Singth/.claude/skills/google-flow-automation/venv/Scripts/python.exe" Tools/interiors/pack_rooms.py
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
COLS, ROWS, TILE = 8, 4, 512
SPARKLE = (900, 900, 44, 44)   # x, y, w, h of Flow's sparkle on a 1024 x 1024 image (checked on 04 and 17)
OUT = os.path.join(ROOT, "Assets", "Nainital", "Data", "Interiors", "RoomPhotos.png")

cfg = json.load(open(os.path.join(HERE, "prompts.json"), encoding="utf-8"))
rooms = cfg["rooms"]
os.makedirs(os.path.join(HERE, "clean"), exist_ok=True)
lama = None


def heal(src, dst):
    global lama
    if lama is None:
        from simple_lama_inpainting import SimpleLama
        lama = SimpleLama()
    im = Image.open(src).convert("RGB")
    if im.size != (1024, 1024):
        im = im.resize((1024, 1024), Image.LANCZOS)
    x, y, w, h = SPARKLE
    pad, m = 6, 160
    x0, y0, x1, y1 = x - pad, y - pad, x + w + pad, y + h + pad
    cx0, cy0, cx1, cy1 = max(0, x0 - m), max(0, y0 - m), min(1024, x1 + m), min(1024, y1 + m)
    crop = im.crop((cx0, cy0, cx1, cy1))
    mask = np.zeros((crop.height, crop.width), np.uint8)
    mask[y0 - cy0:y1 - cy0, x0 - cx0:x1 - cx0] = 255
    fixed = lama(crop, Image.fromarray(mask)).convert("RGB").resize(crop.size)
    im.paste(fixed.crop((x0 - cx0, y0 - cy0, x1 - cx0, y1 - cy0)), (x0, y0))
    im.save(dst)


atlas = Image.new("RGB", (COLS * TILE, ROWS * TILE), (12, 11, 10))
sheet = Image.new("RGB", (COLS * 256, ROWS * 256), (0, 0, 0))
d = ImageDraw.Draw(sheet)
missing = []
for r, room in enumerate(rooms):
    name = "%02d_%s.png" % (r, room["name"])
    raw, clean = os.path.join(HERE, "raw", name), os.path.join(HERE, "clean", name)
    if not os.path.exists(raw):
        missing.append(name)
        continue
    if not os.path.exists(clean) or os.path.getmtime(clean) < os.path.getmtime(raw):
        heal(raw, clean)
    tile = Image.open(clean).convert("RGB").resize((TILE, TILE), Image.LANCZOS)
    col, row = r % COLS, r // COLS
    atlas.paste(tile, (col * TILE, (ROWS - 1 - row) * TILE))
    small = tile.resize((256, 256))
    sx, sy = col * 256, (ROWS - 1 - row) * 256
    sheet.paste(small, (sx, sy))
    d.rectangle((sx, sy, sx + 150, sy + 14), fill=(0, 0, 0))
    d.text((sx + 3, sy + 2), "%d %s" % (r, room["name"]), fill=(255, 255, 0))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
atlas.save(OUT, optimize=True)
sheet.save(os.path.join(HERE, "rooms_sheet.jpg"), quality=85)
print("atlas", OUT, atlas.size, "rooms", len(rooms) - len(missing), "missing", missing)
sys.exit(1 if missing else 0)

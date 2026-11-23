"""
The player's outfits (shops, 22 Nov 2026; owner: "clothes / jackets / caps ... 4-6 outfits"): recoloured copies of the
player's own Tripo texture, so his face, hair and hands stay his and only the clothes change.

  python Tools/outfits/make_outfits.py            (numpy + Pillow)

Needs Tools/outfits/regions_<model>.npz from outfit_regions.py (Blender, the skin weights -> body part per texel).
A texel is recoloured when its body part is clothes (torso + arms, hips, legs) AND its colour is the garment's: the black
puffer = dark and grey (low saturation), the jeans = blue. The new colour is a gradient map of the old brightness, so the
quilting, folds and shine stay. Writes Assets/Resources/Outfits/<id>.jpg (OutfitLook in Runtime/Core/Inventory.cs loads
them by id) and Tools/outfits/regions_preview.jpg (the parts over the texture, to check).
"""
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
# the player's model (24 Nov 2026: the new Tripo player m_player; was m_young_puffer) - `python make_outfits.py [model]`
MODEL = sys.argv[1] if len(sys.argv) > 1 else "m_player"
SRC = os.path.join(ROOT, "Assets", "Nainital", "Characters", "Textures", MODEL + ".jpg")
REG = os.path.join(HERE, "regions_" + MODEL + ".npz")
OUT = os.path.join(ROOT, "Assets", "Resources", "Outfits")

HEAD, HANDS, FEET, LEGS, HIPS, TORSO = 1, 2, 3, 4, 5, 6

# id: jacket (dark, light), trousers (dark, light) or None = the jeans stay
OUTFITS = {
    "outfit_maroon":  (((0.20, 0.035, 0.05), (0.72, 0.17, 0.22)), None),
    "outfit_mustard": (((0.30, 0.19, 0.03), (0.96, 0.71, 0.18)), None),
    "outfit_olive":   (((0.11, 0.13, 0.06), (0.50, 0.55, 0.30)), ((0.30, 0.25, 0.15), (0.80, 0.70, 0.50))),
    "outfit_pahadi":  (((0.035, 0.06, 0.16), (0.26, 0.40, 0.72)), None),
    "outfit_winter":  (((0.09, 0.13, 0.15), (0.50, 0.62, 0.66)), ((0.07, 0.07, 0.08), (0.42, 0.42, 0.44))),
}


def hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = mx - mn
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    h = np.zeros_like(mx)
    dd = np.maximum(d, 1e-6)
    h = np.where(mx == r, ((g - b) / dd) % 6, h)
    h = np.where(mx == g, (b - r) / dd + 2, h)
    h = np.where(mx == b, (r - g) / dd + 4, h)
    h = np.where(d < 1e-6, 0, h) * 60.0
    return h, s, mx


def fill_parts(part, z, steps):
    """Texels with no surface take the part (and height) of a neighbour (the texture's padding around each island)."""
    p, z = part.copy(), z.copy()
    for _ in range(steps):
        empty = p == 0
        if not empty.any():
            break
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sh = np.roll(np.roll(p, dy, 0), dx, 1)
            shz = np.roll(np.roll(z, dy, 0), dx, 1)
            take = empty & (sh > 0)
            p[take] = sh[take]
            z[take] = shz[take]
            empty = p == 0
    return p, z


def ramp(x, a, b):
    return np.clip((x - a) / (b - a), 0, 1)


def gradient(L, mask, dark, light):
    sel = L[mask > 0.5]
    lo, hi = (np.percentile(sel, 3), np.percentile(sel, 97)) if sel.size > 100 else (0.0, 1.0)
    t = np.clip((L - lo) / max(hi - lo, 1e-3), 0, 1)
    col = np.array(dark)[None, None, :] + (np.array(light) - np.array(dark))[None, None, :] * (t ** 0.85)[..., None]
    return np.clip(col + (t ** 6)[..., None] * 0.22, 0, 1)   # the puffer's shine stays a little white


def main():
    src = np.asarray(Image.open(SRC).convert("RGB"), np.float32) / 255.0
    H, W = src.shape[:2]
    reg = np.load(REG)
    part, zz = fill_parts(reg["part"], reg["z"].astype(np.float32), 10)
    k = H // part.shape[0]
    part = np.repeat(np.repeat(part, k, 0), k, 1)[:H, :W]
    zz = np.repeat(np.repeat(zz, k, 0), k, 1)[:H, :W]

    h, s, v = hsv(src)
    L = src @ np.array([0.299, 0.587, 0.114], np.float32)
    clothes = np.isin(part, (TORSO, HIPS, LEGS))
    # the jacket's high collar and the shoulders round it are skinned to the neck, the cuffs to the hands: those count
    # below the chin (0.835-0.85 of the height; the beard starts at ~0.86), the cuffs only when clearly grey
    upper = np.isin(part, (TORSO, HIPS)) + (part == HEAD) * ramp(-zz, -0.85, -0.835)
    # the black puffer: grey-black (low saturation), not the white shirt / shoes
    dark_grey = ramp(-s, -0.34, -0.20) * ramp(-v, -0.88, -0.70)
    jacket = dark_grey * np.clip(upper, 0, 1) + (part == HANDS) * ramp(-s, -0.2, -0.12) * ramp(-v, -0.6, -0.45)
    # the jeans: blue
    blue = ramp(h, 180, 195) * ramp(-h, -240, -228) * ramp(s, 0.10, 0.20)
    jeans = blue * np.isin(part, (LEGS, HIPS, TORSO))

    os.makedirs(OUT, exist_ok=True)
    for oid, (jk, tr) in OUTFITS.items():
        out = src.copy()
        newj = gradient(L, jacket, *jk)
        out = out * (1 - jacket[..., None]) + newj * jacket[..., None]
        if tr:
            newt = gradient(L, jeans, *tr)
            out = out * (1 - jeans[..., None]) + newt * jeans[..., None]
        Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, oid + ".jpg"), quality=90)
        print(f"[outfits] {oid}: jacket {float((jacket > 0.5).mean()) * 100:.1f}% of the texture, trousers {float((jeans > 0.5).mean()) * 100 if tr else 0:.1f}%")

    # check sheet: the parts tinted over the texture
    tint = {HEAD: (1, 0.2, 0.2), HANDS: (1, 0.8, 0.1), FEET: (0.6, 0.2, 1), LEGS: (0.1, 0.5, 1), HIPS: (0.1, 1, 1), TORSO: (0.2, 1, 0.2)}
    prev = src * 0.55
    for pid, c in tint.items():
        prev[part == pid] += np.array(c, np.float32) * 0.45
    prev = np.concatenate([prev, np.repeat((jacket + jeans * 0.5)[..., None], 3, 2)], 1)
    Image.fromarray((np.clip(prev, 0, 1) * 255).astype(np.uint8)).resize((W // 2, H // 4)).save(os.path.join(HERE, "regions_preview.jpg"), quality=85)


if __name__ == "__main__":
    main()

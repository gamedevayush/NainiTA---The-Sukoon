"""The phone's art (owner, 22 Nov 2026: "phone UI tgda bnaao .. curved sides .. iphone ki tarah but not iphone .. crazy
glassmorphism apps with smooth animations .. keypad .. phone me nainital ka wallpaper").

Writes Assets/Resources/Phone/:
  wall_<name>.jpg + wall_<name>_blur.jpg   the game's own Nainital renders (Shots/wall_*.png, PhoneWallpaper.Render) and a
                                           small blurred copy - the frosted glass shows it (Shaders/UIGlass.shader)
  frame.png, screen.png, glass.png, glass_edge.png, shadow.png, circle.png, ring.png, pill.png   shapes (9-sliced in code)
  ic_<name>.png   app glyphs: white, or filled with the app's two-colour gradient (ic_<name>_g.png)
Every pixel keeps its colour under a zero alpha (no dark fringes when Unity filters the edges).

Run: python Tools/phone/make_phone_art.py   (Pillow)
"""
import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Phone")
SHOTS = os.path.join(ROOT, "Shots")
FONT = os.path.join(ROOT, "Tools", "loc", "fonts_src", "Rajdhani-Bold.ttf")
SS = 4   # supersampling


def save_mask(mask, path, rgb=(255, 255, 255)):
    """An alpha mask as a sprite: colour everywhere, alpha = the mask."""
    img = Image.new("RGBA", mask.size, rgb + (0,))
    img.putalpha(mask)
    img.save(path)


def rrect_mask(w, h, r, inset=0):
    m = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle([inset * SS, inset * SS, (w - inset) * SS - 1, (h - inset) * SS - 1], radius=r * SS, fill=255)
    return m.resize((w, h), Image.LANCZOS)


def shapes():
    # the phone's body: a titanium rim (light on the top-left edge, darker below) round a black bezel; the screen sits in it
    W = H = 320
    outer = rrect_mask(W, H, 118)
    inner = rrect_mask(W, H, 100, inset=14)
    rim = ImageChops.subtract(outer, inner)
    grad = Image.new("RGB", (W, H))
    gp = grad.load()
    for y in range(H):
        for x in range(W):
            t = (x * 0.35 + y) / (W * 0.35 + H)
            v = int(205 - 120 * t + 30 * math.sin(t * 9.0))
            gp[x, y] = (v, v + 4, v + 10)
    body = Image.new("RGBA", (W, H), (20, 22, 26, 0))
    body.paste(grad, (0, 0))
    body.putalpha(rim)
    # a thin dark bezel just inside the rim
    bez = ImageChops.subtract(inner, rrect_mask(W, H, 94, inset=20))
    bez_img = Image.new("RGBA", (W, H), (6, 7, 9, 0))
    bez_img.putalpha(bez)
    body = Image.alpha_composite(body, bez_img)
    body.save(os.path.join(OUT, "frame.png"))
    save_mask(rrect_mask(W, H, 94, inset=20), os.path.join(OUT, "screen.png"))
    # glass tile (alpha shape) and its edge light
    G = 160
    save_mask(rrect_mask(G, G, 46), os.path.join(OUT, "glass.png"))
    edge = ImageChops.subtract(rrect_mask(G, G, 46), rrect_mask(G, G, 44, inset=2))
    # brighter on the top edge
    fade = Image.new("L", (G, G))
    fp = fade.load()
    for y in range(G):
        for x in range(G):
            fp[x, y] = int(255 * (1.0 - 0.75 * y / G))
    save_mask(ImageChops.multiply(edge, fade), os.path.join(OUT, "glass_edge.png"))
    # soft shadow
    S = 200
    sh = rrect_mask(S, S, 60, inset=40).filter(ImageFilter.GaussianBlur(16))
    save_mask(sh, os.path.join(OUT, "shadow.png"), (0, 0, 0))
    # circle, ring, pill
    C = 256
    m = Image.new("L", (C * SS, C * SS), 0)
    ImageDraw.Draw(m).ellipse([0, 0, C * SS - 1, C * SS - 1], fill=255)
    circ = m.resize((C, C), Image.LANCZOS)
    save_mask(circ, os.path.join(OUT, "circle.png"))
    m = Image.new("L", (C * SS, C * SS), 0)
    ImageDraw.Draw(m).ellipse([0, 0, C * SS - 1, C * SS - 1], outline=255, width=6 * SS)
    save_mask(m.resize((C, C), Image.LANCZOS), os.path.join(OUT, "ring.png"))
    save_mask(rrect_mask(120, 40, 20), os.path.join(OUT, "pill.png"))


# ---------------------------------------------------------------- glyphs (drawn at 4x on a 0..1 square)
N = 192


class Pen:
    def __init__(self):
        self.m = Image.new("L", (N * SS, N * SS), 0)
        self.d = ImageDraw.Draw(self.m)

    def P(self, x, y):
        return (x * N * SS, y * N * SS)

    def circle(self, cx, cy, r, fill=255, width=0):
        a, b = self.P(cx - r, cy - r), self.P(cx + r, cy + r)
        if width:
            self.d.ellipse([a, b], outline=fill, width=int(width * N * SS))
        else:
            self.d.ellipse([a, b], fill=fill)

    def rrect(self, x0, y0, x1, y1, r, fill=255, width=0):
        a, b = self.P(x0, y0), self.P(x1, y1)
        if width:
            self.d.rounded_rectangle([a, b], radius=r * N * SS, outline=fill, width=int(width * N * SS))
        else:
            self.d.rounded_rectangle([a, b], radius=r * N * SS, fill=fill)

    def poly(self, pts, fill=255):
        self.d.polygon([self.P(x, y) for x, y in pts], fill=fill)

    def line(self, pts, w, fill=255):
        self.d.line([self.P(x, y) for x, y in pts], fill=fill, width=int(w * N * SS), joint="curve")
        for x, y in (pts[0], pts[-1]):
            self.circle(x, y, w / 2, fill)

    def arc(self, cx, cy, r, a0, a1, w, fill=255):
        a, b = self.P(cx - r, cy - r), self.P(cx + r, cy + r)
        self.d.arc([a, b], a0, a1, fill=fill, width=int(w * N * SS))

    def text(self, s, size, cx, cy):
        f = ImageFont.truetype(FONT, int(size * N * SS))
        bb = self.d.textbbox((0, 0), s, font=f)
        x = cx * N * SS - (bb[0] + bb[2]) / 2
        y = cy * N * SS - (bb[1] + bb[3]) / 2
        self.d.text((x, y), s, font=f, fill=255)

    def done(self):
        return self.m.resize((N, N), Image.LANCZOS)


def handset(p, angle=0.0):
    """The classic receiver: two rounded pads joined by a curved grip, turned by angle (degrees)."""
    q = Pen()
    # a ⊂ shape: the grip curves round on the left, the ear and mouth pieces reach in at the ends
    q.arc(0.64, 0.5, 0.34, 118, 242, 0.13)
    q.rrect(0.40, 0.12, 0.68, 0.31, 0.07)
    q.rrect(0.40, 0.69, 0.68, 0.88, 0.07)
    m = q.m.rotate(-45 + angle, resample=Image.BICUBIC, center=(N * SS / 2, N * SS / 2))
    p.m = ImageChops.lighter(p.m, m)
    p.d = ImageDraw.Draw(p.m)


def g_phone():
    p = Pen(); handset(p); return p.done()


def g_endcall():
    p = Pen(); handset(p, 90); return p.done()


def g_camera():
    p = Pen()
    p.rrect(0.14, 0.30, 0.86, 0.78, 0.10)
    p.rrect(0.36, 0.22, 0.58, 0.34, 0.04)
    p.circle(0.5, 0.54, 0.16, 0)
    p.circle(0.5, 0.54, 0.10)
    p.circle(0.75, 0.40, 0.03, 0)
    return p.done()


def g_selfie():
    p = Pen()
    p.rrect(0.26, 0.10, 0.74, 0.90, 0.10, width=0.06)
    p.circle(0.5, 0.40, 0.09)
    p.d.pieslice([p.P(0.33, 0.55), p.P(0.67, 0.89)], 180, 360, fill=255)
    p.circle(0.5, 0.17, 0.025)
    return p.done()


def g_gallery():
    p = Pen()
    p.rrect(0.12, 0.18, 0.88, 0.82, 0.10, width=0.06)
    p.poly([(0.18, 0.76), (0.42, 0.44), (0.58, 0.64), (0.66, 0.54), (0.82, 0.76)])
    p.circle(0.66, 0.36, 0.07)
    return p.done()


def g_map():
    p = Pen()
    p.circle(0.5, 0.40, 0.24)
    p.poly([(0.30, 0.52), (0.70, 0.52), (0.5, 0.88)])
    p.circle(0.5, 0.40, 0.10, 0)
    return p.done()


def g_weather():
    p = Pen()
    p.circle(0.40, 0.38, 0.15)
    for i in range(8):
        a = i * math.pi / 4
        p.line([(0.40 + math.cos(a) * 0.21, 0.38 + math.sin(a) * 0.21), (0.40 + math.cos(a) * 0.28, 0.38 + math.sin(a) * 0.28)], 0.045)
    # the cloud in front, cut out from the sun with a gap
    c = Pen()
    for (x, y, r) in ((0.48, 0.62, 0.16), (0.66, 0.56, 0.18), (0.80, 0.68, 0.12)):
        c.circle(x, y, r)
    c.rrect(0.32, 0.62, 0.92, 0.80, 0.09)
    gap = c.done().filter(ImageFilter.MaxFilter(9))
    sun = p.done()
    return ImageChops.lighter(ImageChops.subtract(sun, gap), c.done())


def g_wallet():
    p = Pen()
    p.text("₹", 0.78, 0.5, 0.52)
    return p.done()


def g_settings():
    p = Pen()
    p.circle(0.5, 0.5, 0.27)
    for i in range(8):
        a = i * math.pi / 4
        cx, cy = 0.5 + math.cos(a) * 0.32, 0.5 + math.sin(a) * 0.32
        q = Pen()
        q.rrect(cx - 0.07, cy - 0.07, cx + 0.07, cy + 0.07, 0.02)
        rot = q.m.rotate(-math.degrees(a), resample=Image.BICUBIC, center=q.P(cx, cy))
        p.m = ImageChops.lighter(p.m, rot)
        p.d = ImageDraw.Draw(p.m)
    p.circle(0.5, 0.5, 0.11, 0)
    return p.done()


def g_sos():
    p = Pen()
    p.text("SOS", 0.42, 0.5, 0.52)
    return p.done()


def g_stats():
    p = Pen()
    for i, h in enumerate((0.30, 0.48, 0.66)):
        x = 0.20 + i * 0.22
        p.rrect(x, 0.84 - h, x + 0.15, 0.84, 0.04)
    p.line([(0.16, 0.46), (0.36, 0.34), (0.56, 0.40), (0.84, 0.16)], 0.05)
    return p.done()


def g_radio():
    p = Pen()
    p.rrect(0.12, 0.34, 0.88, 0.84, 0.08)
    p.line([(0.30, 0.34), (0.72, 0.14)], 0.05)
    p.circle(0.66, 0.59, 0.15, 0)
    p.circle(0.66, 0.59, 0.07)
    for i in range(3):
        p.rrect(0.20, 0.46 + i * 0.1, 0.44, 0.50 + i * 0.1, 0.02, fill=0)
    return p.done()


def g_back():
    p = Pen(); p.line([(0.62, 0.20), (0.34, 0.50), (0.62, 0.80)], 0.10); return p.done()


def g_backspace():
    p = Pen()
    p.poly([(0.10, 0.5), (0.32, 0.24), (0.88, 0.24), (0.88, 0.76), (0.32, 0.76)])
    q = Pen()
    q.line([(0.46, 0.38), (0.70, 0.62)], 0.06)
    q.line([(0.70, 0.38), (0.46, 0.62)], 0.06)
    return ImageChops.subtract(p.done(), q.done())


def g_close():
    p = Pen(); p.line([(0.25, 0.25), (0.75, 0.75)], 0.10); p.line([(0.75, 0.25), (0.25, 0.75)], 0.10); return p.done()


def g_trash():
    p = Pen()
    p.rrect(0.24, 0.30, 0.76, 0.88, 0.06, width=0.07)
    p.rrect(0.16, 0.20, 0.84, 0.28, 0.03)
    p.rrect(0.40, 0.12, 0.60, 0.22, 0.03)
    p.line([(0.42, 0.42), (0.42, 0.76)], 0.05)
    p.line([(0.58, 0.42), (0.58, 0.76)], 0.05)
    return p.done()


def g_signal():
    p = Pen()
    for i in range(4):
        h = 0.22 + i * 0.18
        p.rrect(0.10 + i * 0.21, 0.86 - h, 0.26 + i * 0.21, 0.86, 0.03)
    return p.done()


def g_battery():
    p = Pen()
    p.rrect(0.08, 0.30, 0.82, 0.70, 0.08, width=0.05)
    p.rrect(0.85, 0.42, 0.93, 0.58, 0.02)
    p.rrect(0.15, 0.37, 0.66, 0.63, 0.04)
    return p.done()


def g_wifi():
    p = Pen()
    for r in (0.36, 0.24):
        p.arc(0.5, 0.78, r, 225, 315, 0.07)
    p.circle(0.5, 0.74, 0.06)
    return p.done()


def g_user():
    p = Pen()
    p.circle(0.5, 0.36, 0.16)
    p.d.pieslice([p.P(0.20, 0.56), p.P(0.80, 1.16)], 180, 360, fill=255)
    return p.done()


GLYPHS = {
    "phone": (g_phone, ((90, 255, 170), (0, 190, 130))),
    "camera": (g_camera, ((255, 190, 90), (255, 80, 110))),
    "selfie": (g_selfie, ((255, 140, 240), (140, 110, 255))),
    "gallery": (g_gallery, ((255, 230, 90), (255, 140, 60))),
    "map": (g_map, ((80, 200, 255), (60, 255, 230))),
    "weather": (g_weather, ((150, 210, 255), (255, 240, 180))),
    "wallet": (g_wallet, ((255, 215, 90), (255, 120, 60))),
    "settings": (g_settings, ((220, 230, 240), (150, 170, 200))),
    "sos": (g_sos, ((255, 110, 110), (255, 50, 90))),
    "stats": (g_stats, ((120, 255, 140), (40, 200, 255))),
    "radio": (g_radio, ((255, 160, 80), (255, 90, 200))),
    "endcall": (g_endcall, None), "call": (g_phone, None), "back": (g_back, None), "backspace": (g_backspace, None),
    "close": (g_close, None), "trash": (g_trash, None), "signal": (g_signal, None), "battery": (g_battery, None),
    "wifi": (g_wifi, None), "user": (g_user, None),
}


def gradient(c0, c1):
    g = Image.new("RGB", (N, N))
    gp = g.load()
    for y in range(N):
        for x in range(N):
            t = min(1.0, max(0.0, (x + y) / (2.0 * N)))
            gp[x, y] = tuple(int(c0[i] + (c1[i] - c0[i]) * t) for i in range(3))
    return g


def glyphs():
    for name, (fn, cols) in GLYPHS.items():
        m = fn()
        save_mask(m, os.path.join(OUT, f"ic_{name}.png"))
        if cols:
            img = gradient(*cols).convert("RGBA")
            img.putalpha(m)
            img.save(os.path.join(OUT, f"ic_{name}_g.png"))


def wallpapers():
    names = []
    for f in sorted(os.listdir(SHOTS)):
        if not (f.startswith("wall_") and f.endswith(".png")):
            continue
        name = f[5:-4]
        im = Image.open(os.path.join(SHOTS, f)).convert("RGB")
        im.resize((720, 1560), Image.LANCZOS).save(os.path.join(OUT, f"wall_{name}.jpg"), quality=88)
        small = im.resize((180, 390), Image.LANCZOS).filter(ImageFilter.GaussianBlur(7))
        small.save(os.path.join(OUT, f"wall_{name}_blur.jpg"), quality=90)
        names.append(name)
    return names


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    shapes()
    glyphs()
    print("wallpapers:", wallpapers())
    print("ok ->", OUT)

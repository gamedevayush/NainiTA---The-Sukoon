"""Extruded 3D lettering data from OFL display fonts (user, 2026-11-18: "saare jahan text hai, bahut basic hai - usko
tagda 3D name kardo, different stylish font based on their category").

For every printable ASCII character of each font below: the outline flattened to polylines (cap height = 1, y up,
outer contours counter-clockwise, holes clockwise), the face triangulated (each hole bridged into its outer contour,
then ear clipping), and the advance width. Written to Assets/Nainital/Data/Fonts3D/<key>.json, which
Editor/Glyph3D.cs extrudes into letters. Only fontTools is needed (no triangulation package).

  py Tools/fonts3d/make_fonts3d.py
Fonts: Tools/fonts3d/ttf/ (Google Fonts, SIL Open Font License, OFL.txt there).
"""
import json
import math
import os

from fontTools.pens.basePen import BasePen
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "Nainital", "Data", "Fonts3D"))
FONTS = {   # key: (file, variable-font axes)
    "script": ("Pacifico-Regular.ttf", None),       # restaurants, cafes, fast food
    "round": ("LilitaOne-Regular.ttf", None),        # sweets, bakery, ice cream (static: variable fonts overlap their contours)
    "royal": ("CinzelDecorative-Bold.ttf", None),   # hotels, gates
    "desi": ("YatraOne-Regular.ttf", None),         # gifts, Tibetan crafts, candles, souvenirs, the welcome gate
    "serif": ("RozhaOne-Regular.ttf", None),        # shawls, woollens
    "tall": ("BebasNeue-Regular.ttf", None),        # clothes, footwear, bags
    "block": ("Anton-Regular.ttf", None),           # banks, opticians, chemists, travel, mobiles, police, road signs
    "deco": ("Limelight-Regular.ttf", None),        # the Capitol cinema
}
CHARS = [chr(c) for c in range(33, 127)]


class FlatPen(BasePen):
    """Outlines as polylines: a few straight pieces per curve (phone budget: ~25-60 points a letter)."""

    def __init__(self, glyphSet, step):
        super().__init__(glyphSet)
        self.contours, self.cur, self.step = [], [], step

    def _pieces(self, *pts):
        length = sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))
        return max(2, min(6, int(math.ceil(length / self.step))))

    def _moveTo(self, p):
        self.cur = [p]

    def _lineTo(self, p):
        self.cur.append(p)

    def _curveToOne(self, p1, p2, p3):
        p0 = self.cur[-1]
        n = self._pieces(p0, p1, p2, p3)
        for i in range(1, n + 1):
            t = i / n
            u = 1 - t
            self.cur.append((u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                             u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]))

    def _qCurveToOne(self, p1, p2):
        p0 = self.cur[-1]
        n = self._pieces(p0, p1, p2)
        for i in range(1, n + 1):
            t = i / n
            u = 1 - t
            self.cur.append((u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0], u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1]))

    def _closePath(self):
        c = self.cur
        if len(c) > 1 and math.dist(c[0], c[-1]) < 1e-6:
            c = c[:-1]
        clean = []
        for p in c:
            if not clean or math.dist(clean[-1], p) > 1e-6:
                clean.append(p)
        if len(clean) >= 3 and abs(area(clean)) > 1e-6:
            self.contours.append(clean)
        self.cur = []

    _endPath = _closePath


def area(c):
    return 0.5 * sum(c[i][0] * c[(i + 1) % len(c)][1] - c[(i + 1) % len(c)][0] * c[i][1] for i in range(len(c)))


def inside(p, c):
    x, y, hit = p[0], p[1], False
    for i in range(len(c)):
        a, b = c[i], c[(i + 1) % len(c)]
        if (a[1] > y) != (b[1] > y) and x < a[0] + (y - a[1]) * (b[0] - a[0]) / (b[1] - a[1]):
            hit = not hit
    return hit


def cross(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


def in_tri(p, a, b, c):
    d1, d2, d3 = cross(a, b, p), cross(b, c, p), cross(c, a, p)
    return not ((d1 < -1e-12 or d2 < -1e-12 or d3 < -1e-12) and (d1 > 1e-12 or d2 > 1e-12 or d3 > 1e-12))


def bridge(outer, holes):
    """One simple polygon: each hole cut into the outer contour along a seam from its rightmost point (Eberly)."""
    poly = list(outer)
    for h in sorted(holes, key=lambda h: -max(p[0] for p in h)):
        mi = max(range(len(h)), key=lambda i: (h[i][0], -h[i][1]))
        m = h[mi]
        best, edge = float("inf"), -1
        for i in range(len(poly)):
            a, b = poly[i], poly[(i + 1) % len(poly)]
            if (a[1] > m[1]) != (b[1] > m[1]):
                x = a[0] + (m[1] - a[1]) * (b[0] - a[0]) / (b[1] - a[1])
                if m[0] <= x < best:
                    best, edge = x, i
        if edge < 0:
            continue
        a, b = poly[edge], poly[(edge + 1) % len(poly)]
        pi = edge if a[0] > b[0] else (edge + 1) % len(poly)
        ip = (best, m[1])
        # a reflex vertex inside the triangle (m, hit point, p) would block the seam: take the one nearest the ray
        cand = []
        for k in range(len(poly)):
            if k == pi:
                continue
            q = poly[k]
            prv, nxt = poly[k - 1], poly[(k + 1) % len(poly)]
            if cross(prv, q, nxt) < 0 and in_tri(q, m, ip, poly[pi]) and q[0] >= m[0]:
                ang = abs(math.atan2(q[1] - m[1], q[0] - m[0]))
                cand.append((ang, math.dist(q, m), k))
        if cand:
            pi = min(cand)[2]
        p = poly[pi]
        ring = h[mi:] + h[:mi + 1]
        poly = poly[:pi + 1] + ring + [p] + poly[pi + 1:]
    return poly


def earclip(poly):
    pts = poly
    idx = list(range(len(pts)))
    tris = []
    guard = 0
    while len(idx) > 3 and guard < 20000:
        guard += 1
        n = len(idx)
        reflex = [j for k, j in enumerate(idx) if cross(pts[idx[k - 1]], pts[j], pts[idx[(k + 1) % n]]) <= 1e-12]
        clipped = False
        for k in range(n):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % n]
            a, b, c = pts[i0], pts[i1], pts[i2]
            if cross(a, b, c) <= 1e-12:
                continue
            ok = True
            for j in reflex:
                if j in (i0, i1, i2):
                    continue
                q = pts[j]
                if q == a or q == b or q == c:
                    continue
                if in_tri(q, a, b, c):
                    ok = False
                    break
            if ok:
                tris += [i0, i1, i2]
                idx.pop(k)
                clipped = True
                break
        if not clipped:   # only degenerate pieces left: drop the flattest vertex
            k = min(range(n), key=lambda k: abs(cross(pts[idx[k - 1]], pts[idx[k]], pts[idx[(k + 1) % n]])))
            idx.pop(k)
    if len(idx) == 3 and cross(pts[idx[0]], pts[idx[1]], pts[idx[2]]) > 1e-12:
        tris += idx
    return tris


def glyph_data(contours, scale):
    contours = [[(x * scale, y * scale) for x, y in c] for c in contours]
    depth = [sum(1 for j, o in enumerate(contours) if j != i and inside(c[0], o)) for i, c in enumerate(contours)]
    outers = [i for i in range(len(contours)) if depth[i] % 2 == 0]
    groups = {i: [] for i in outers}
    for i in range(len(contours)):
        if depth[i] % 2 == 1:
            par = [o for o in outers if depth[o] == depth[i] - 1 and inside(contours[i][0], contours[o])]
            if par:
                groups[min(par, key=lambda o: abs(area(contours[o])))].append(i)
    pts, tris, sides = [], [], []
    for o, hs in groups.items():
        outer = contours[o] if area(contours[o]) > 0 else contours[o][::-1]
        holes = [contours[h] if area(contours[h]) < 0 else contours[h][::-1] for h in hs]
        merged = bridge(outer, holes)
        base = len(pts)
        pts += merged
        tris += [base + t for t in earclip(merged)]
        sides.append(outer)
        sides += holes
    r = lambda v: round(v, 4)
    return ([r(v) for p in pts for v in p], tris, [{"p": [r(v) for p in c for v in p]} for c in sides])


def main():
    os.makedirs(OUT, exist_ok=True)
    for key, (file, axes) in FONTS.items():
        font = TTFont(os.path.join(HERE, "ttf", file))
        if axes:
            font = instancer.instantiateVariableFont(font, axes)
        gs = font.getGlyphSet()
        cmap = font.getBestCmap()
        upm = font["head"].unitsPerEm
        cap = getattr(font["OS/2"], "sCapHeight", 0) or 0
        if cap <= 0 and "H" in [chr(c) for c in cmap]:
            bp = FlatPen(gs, upm)
            gs[cmap[ord("H")]].draw(bp)
            cap = max(p[1] for c in bp.contours for p in c)
        cap = cap or upm * 0.7
        scale = 1.0 / cap
        glyphs, points = [], 0
        for ch in CHARS:
            name = cmap.get(ord(ch))
            if not name:
                continue
            pen = FlatPen(gs, upm * 0.06)
            gs[name].draw(pen)
            adv = gs[name].width * scale
            p, t, s = glyph_data(pen.contours, scale) if pen.contours else ([], [], [])
            glyphs.append({"c": ord(ch), "adv": round(adv, 4), "pts": p, "tris": t, "contours": s})
            points += len(p) // 2
        space = gs[cmap[32]].width * scale if 32 in cmap else 0.3
        data = {"key": key, "file": file, "space": round(space, 4), "glyphs": glyphs}
        path = os.path.join(OUT, key + ".json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f, separators=(",", ":"))
        print(f"{key:7s} {file:24s} {len(glyphs)} glyphs, {points / max(1, len(glyphs)):.0f} points a glyph, {os.path.getsize(path) // 1024} KB")


if __name__ == "__main__":
    main()

"""Builds the game's text tables and the Hindi-capable fonts (owner, 22 Nov 2026: "default will be english ... can be
changable to hindi .. with hindi font .. and everywhere").

Unity's uGUI Text cannot shape Devanagari (the i-matra would sit after its consonant, conjuncts and reph would fall
apart). So every Hindi string is shaped HERE with HarfBuzz against the game's own font (Rajdhani, Indian Type Foundry,
OFL - it has Devanagari), and the shaped glyphs are given Private Use Area code points (U+E000...) added to the fonts'
cmap. A mark that HarfBuzz moves (x/y offset, e.g. the ri-matra under a wide letter) gets its own shifted copy of the
glyph. The game then shows a Hindi string as a plain run of PUA characters: already in visual order, already joined.

Input : Tools/loc/strings.tsv   (key <TAB> english <TAB> hindi; '#' comment lines; '\\n' = new line; '#' inside a field
        is a number placeholder; {0} {1} are format arguments)
Output: Assets/Resources/Loc/strings.json  (keys, en, hi - hi already shaped into PUA)
        Assets/Resources/Fonts/Rajdhani-*.ttf  (the originals + the PUA glyphs; originals kept in Tools/loc/fonts_src)

Run (any PC): python Tools/loc/build_loc.py      (needs fontTools + uharfbuzz: pip install --user fonttools uharfbuzz)
"""
import json, os, re, shutil, sys
import uharfbuzz as hb
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TSV_DIR = os.path.join(ROOT, "Tools", "loc")   # every strings*.tsv in it (strings, strings_world, strings_game, ...)
SRC = os.path.join(ROOT, "Tools", "loc", "fonts_src")
DST = os.path.join(ROOT, "Assets", "Resources", "Fonts")
OUT = os.path.join(ROOT, "Assets", "Resources", "Loc", "strings.json")
FONTS = ["Rajdhani-SemiBold", "Rajdhani-Bold", "Rajdhani-Medium"]
SHAPER = "Rajdhani-SemiBold"   # glyph names are the same in the family; offsets are taken from this weight
PUA0 = 0xE000

DEV = re.compile(r"[ऀ-ॿ‌‍]+")
TAG = re.compile(r"<[^>]+>|\{\d+\}")


def read_tsv():
    rows, seen = [], set()
    import glob
    for path in sorted(glob.glob(os.path.join(TSV_DIR, "strings*.tsv"))):
        rows_from(path, rows, seen)
    return rows


def rows_from(TSV, rows, seen):
    name = os.path.basename(TSV)
    for n, line in enumerate(open(TSV, encoding="utf-8"), 1):
        line = line.rstrip("\n\r")
        if not line.strip() or (line.lstrip().startswith("#") and "	" not in line):   # ("# minute pehle" is a key)
            continue
        parts = line.split("\t")
        if len(parts) < 2:
            print(f"{name}:{n}: needs key<TAB>english<TAB>hindi", file=sys.stderr)
            continue
        key, en = parts[0], parts[1]
        hi = parts[2] if len(parts) > 2 else ""
        un = lambda s: s.replace("\\n", "\n")
        key, en, hi = un(key), un(en), un(hi)
        if key in seen:
            continue   # the first file wins (strings.tsv, then strings_game.tsv, then strings_world.tsv)
        seen.add(key)
        rows.append((key, en or key, hi))


def main():
    os.makedirs(SRC, exist_ok=True)
    for f in FONTS:   # keep the untouched originals once
        s = os.path.join(SRC, f + ".ttf")
        if not os.path.exists(s):
            shutil.copy(os.path.join(DST, f + ".ttf"), s)
    rows = read_tsv()

    blob = hb.Blob.from_file_path(os.path.join(SRC, SHAPER + ".ttf"))
    hbfont = hb.Font(hb.Face(blob))
    upem = hbfont.face.upem

    glyph_cp = {}    # (glyph name, dx, dy) -> PUA code point
    def shape_run(text):
        buf = hb.Buffer()
        buf.add_str(text)
        buf.direction, buf.script, buf.language = "ltr", "deva", "hi"
        hb.shape(hbfont, buf, {})
        out = []
        for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
            name = hbfont.glyph_to_string(info.codepoint)
            k = (name, pos.x_offset, pos.y_offset)
            if k not in glyph_cp:
                glyph_cp[k] = PUA0 + len(glyph_cp)
            out.append(chr(glyph_cp[k]))
        return "".join(out)

    def shape(s):
        # tags and {n} untouched; Devanagari runs shaped (shaping never crosses a space), the rest as it is
        res, i = [], 0
        for m in TAG.finditer(s):
            res.append(shape_plain(s[i:m.start()]))
            res.append(m.group(0))
            i = m.end()
        res.append(shape_plain(s[i:]))
        return "".join(res)

    def shape_plain(s):
        return DEV.sub(lambda m: shape_run(m.group(0)), s)

    keys, ens, his, missing = [], [], [], 0
    for key, en, hi in rows:
        keys.append(key)
        ens.append(shape(en))   # an English text may hold Devanagari too (the language button's own name)
        if not hi:
            missing += 1
        his.append(shape(hi) if hi else "")

    # the fonts: a PUA code point for every glyph the shaped strings use; moved marks get a shifted copy
    for f in FONTS:
        tt = TTFont(os.path.join(SRC, f + ".ttf"))
        order = list(tt.getGlyphOrder())
        gs = tt.getGlyphSet()
        glyf, hmtx = tt["glyf"], tt["hmtx"]
        added = []
        names = {}
        for (name, dx, dy), cp in glyph_cp.items():
            if name not in gs:
                continue
            if dx == 0 and dy == 0:
                names[cp] = name
                continue
            nn = f"{name}.o{dx}_{dy}".replace("-", "m")
            if nn not in names.values() and nn not in order and nn not in added:
                pen = TTGlyphPen(gs)
                gs[name].draw(TransformPen(pen, (1, 0, 0, 1, dx, dy)))
                g = pen.glyph()
                glyf[nn] = g
                adv, lsb = hmtx[name]
                g.recalcBounds(glyf)
                hmtx[nn] = (adv, getattr(g, "xMin", 0))
                added.append(nn)
            names[cp] = nn
        if added:
            tt.setGlyphOrder(order + added)
        for t in tt["cmap"].tables:
            if t.isUnicode():
                for cp, n in names.items():
                    t.cmap[cp] = n
        if "post" in tt and tt["post"].formatType == 2.0:
            tt["post"].extraNames = []
            tt["post"].mapping = {}
        tt.save(os.path.join(DST, f + ".ttf"))
        print(f"{f}: {len(names)} PUA code points, {len(added)} shifted glyphs")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    json.dump({"keys": keys, "en": ens, "hi": his}, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    print(f"{len(keys)} strings ({missing} without Hindi) -> {os.path.relpath(OUT, ROOT)}; glyph keys {len(glyph_cp)}")


if __name__ == "__main__":
    main()

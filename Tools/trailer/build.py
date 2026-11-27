"""NainiVerse - First Glimpse: the trailer and the day / night films, from the Unity renders
(D:/NainiTA---The-Sukoon/Cinematics, made with CinemaReel.Timeline "shots"; see CLAUDE.md "Cinematics").

  python build.py analyze     measure every clip (brightness, spread) for the grade
  python build.py preview     key moments as stills with grade + graphics (work/preview/*.jpg)
  python build.py graphics    the title animations as PNG frames (work/el/<id>/)
  python build.py audio       the sound beds + effects (work/*.wav)
  python build.py trailer     out/NainiVerse_First_Glimpse_1080p60.mp4
  python build.py films       out/NainiVerse_Day_1080p60.mp4, out/NainiVerse_Night_1080p60.mp4
  python build.py verify      frame counts, 60 fps, durations
  python build.py all         everything in order

Everything tunable is in config.json. Frames are counted in whole frames at 60 fps from the start of each piece, so the
day and night renders (made from the same timeline frame by frame) line up exactly: each shot plays its day render for
the first half, strobes, then its night render.
"""
import json
import math
import os
import re
import shutil
import subprocess
import sys
import wave
from multiprocessing import Pool

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

PROJ = os.path.dirname(os.path.abspath(__file__))
C = json.load(open(os.path.join(PROJ, 'config.json'), encoding='utf-8'))
FPS = C['fps']
W, H = C['width'], C['height']
S = W / 3840.0                      # config sizes are in 4K pixels
SRC = C['source_dir']
WORK = os.path.join(PROJ, 'work')
OUT = os.path.join(PROJ, 'out')
SR = 48000
P = C['strobe']['pattern']
STROBE = sum(P)

FONTS = {k: os.path.join(PROJ, 'fonts', v) for k, v in
         {'cinzel': 'Cinzel.ttf', 'corm': 'Cormorant.ttf', 'cormi': 'CormorantItalic.ttf', 'mont': 'Montserrat.ttf'}.items()}
# the fonts (SIL Open Font License, Google Fonts) are not in git: fetched from github.com/google/fonts when missing
FONT_URLS = {'Cinzel.ttf': 'ofl/cinzel/Cinzel%5Bwght%5D.ttf',
             'Cormorant.ttf': 'ofl/cormorantgaramond/CormorantGaramond%5Bwght%5D.ttf',
             'CormorantItalic.ttf': 'ofl/cormorantgaramond/CormorantGaramond-Italic%5Bwght%5D.ttf',
             'Montserrat.ttf': 'ofl/montserrat/Montserrat%5Bwght%5D.ttf'}
for _f, _u in FONT_URLS.items():
    _p = os.path.join(PROJ, 'fonts', _f)
    if not os.path.exists(_p):
        import urllib.request
        os.makedirs(os.path.dirname(_p), exist_ok=True)
        urllib.request.urlretrieve('https://raw.githubusercontent.com/google/fonts/main/' + _u, _p)


def ffmpeg_bin(name):
    f = shutil.which(name)
    if f:
        return f
    pk = os.path.join(os.environ.get('LOCALAPPDATA', ''), 'Microsoft', 'WinGet', 'Packages')
    for d in os.listdir(pk):
        if d.startswith('Gyan.FFmpeg'):
            for r, _, files in os.walk(os.path.join(pk, d)):
                if name + '.exe' in files:
                    return os.path.join(r, name + '.exe')
    raise SystemExit('no ' + name)


FFMPEG, FFPROBE = ffmpeg_bin('ffmpeg'), ffmpeg_bin('ffprobe')


def run(cmd, **kw):
    return subprocess.run(cmd, capture_output=True, text=True, **kw)


def clamp(x, a, b):
    return a if x < a else b if x > b else x


# ---------------------------------------------------------------------------------------------------- the timeline
def clip(kind, n):
    return os.path.join(SRC, f'CinematicAnimation_{kind}_shot{n:02d}_4k60.mp4')


def nb_frames(path):
    r = run([FFPROBE, '-v', 'error', '-select_streams', 'v:0', '-show_entries', 'stream=nb_frames', '-of', 'csv=p=0', path])
    return int(r.stdout.strip())


def timeline():
    cache = os.path.join(WORK, 'frames.json')
    fr = json.load(open(cache)) if os.path.exists(cache) else {}
    segs = []
    sr = C['sunrise']
    n0 = round(sr['seconds'] * FPS)
    segs.append(dict(key='sunrise', start=0, n=n0, mid=None, title=sr['title'], suffix=sr['suffix'], window='sun'))
    pos = n0
    for sh in C['shots']:
        k = str(sh['n'])
        if k not in fr:
            nd, nn = nb_frames(clip('day', sh['n'])), nb_frames(clip('night', sh['n']))
            assert nd == nn, f'shot {k}: day {nd} frames, night {nn}'
            fr[k] = nd
        n = fr[k]
        segs.append(dict(key=k, shot=sh['n'], start=pos, n=n, mid=n // 2, title=sh['title'], window=sh['window']))
        pos += n
    os.makedirs(WORK, exist_ok=True)
    json.dump(fr, open(cache, 'w'))
    return segs, pos, round(C['end_card_seconds'] * FPS)


def is_day(f, mid):
    """Local frame f of a shot whose strobe starts at `mid`: the day render showing?"""
    if mid is None or f < mid:
        return True
    k, acc = f - mid, 0
    for i, r in enumerate(P):
        if k < acc + r:
            return i % 2 == 1
        acc += r
    return False


def day_runs(mid):
    runs, acc = [], mid
    for i, r in enumerate(P):
        if i % 2 == 1:
            runs.append((acc, acc + r))
        acc += r
    return runs


# ---------------------------------------------------------------------------------------------------- the grade
def measure(path, kind):
    cache = os.path.join(WORK, 'stats.json')
    st = json.load(open(cache)) if os.path.exists(cache) else {}
    key = os.path.basename(path) + (f'@{C["sunrise"]["from"]}' if kind == 'morning' else '')
    if key not in st:
        r = run([FFPROBE, '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', path])
        dur = float(r.stdout.strip())
        a, b = (C['sunrise']['from'], C['sunrise']['from'] + C['sunrise']['seconds']) if kind == 'morning' else (0, dur)
        vals = {'YAVG': [], 'YLOW': [], 'YHIGH': [], 'SATAVG': []}
        for frac in (0.2, 0.5, 0.8):
            t = a + (b - a) * frac
            r = run([FFMPEG, '-v', 'error', '-ss', f'{t:.3f}', '-i', path, '-frames:v', '1', '-vf',
                     'scale=480:270,signalstats,metadata=print:file=-', '-f', 'null', '-'])
            for k in vals:
                m = re.search(k + r'=([\d.]+)', r.stdout)
                if m:
                    vals[k].append(float(m.group(1)))
        st[key] = {k: sum(v) / len(v) for k, v in vals.items() if v}
        json.dump(st, open(cache, 'w'), indent=1)
    return st[key]


def grade(path, kind):
    """scale to the output size; the black point down to where the footage's darks really are (the renders' blacks sit
    at Y 40-55, milky); exposure (gamma towards the kind's target), contrast, saturation; a soft S curve and a small
    tint - all in YUV (no RGB round trip)."""
    g = C['grade'][kind]
    m = measure(path, kind)
    blk = g.get('black', 0.65)
    if isinstance(blk, str) and blk.startswith('low-'):
        B = m['YLOW'] - float(blk[4:])
    else:
        B = 16 + (m['YLOW'] - 16) * float(blk)
    B = clamp(B, 16, 70)
    n = clamp((m['YAVG'] - B) / (235 - B), 0.02, 0.98)
    gamma = clamp(math.log(n) / math.log(g['target']), *g['gamma'])
    spread = clamp((m['YHIGH'] - m['YLOW']) / 219, 0.08, 1.0)
    contrast = clamp(g['contrast'] * (0.7 / spread) ** 0.2, 0.98, 1.14)
    a = g['curve']
    x = 'clip((val-16)/219,0,1)'
    y = f"clip(16+219*({x}-{a}*sin(2*PI*{x})/(2*PI)),16,235)"
    return (f"scale={W}:{H}:flags=lanczos,lutyuv=y='clip(16+219*(val-{B:.2f})/{235 - B:.2f},16,235)',"
            f"eq=contrast={contrast:.3f}:saturation={g['saturation']:.3f}:gamma={gamma:.3f},"
            f"lutyuv=y='{y}':u='clip(val+({g['du']}),16,240)':v='clip(val+({g['dv']}),16,240)'")


# ---------------------------------------------------------------------------------------------------- graphics
_fonts = {}


def font(name, size4k, wght):
    size = max(8, int(round(size4k * S)))
    key = (name, size, wght)
    if key not in _fonts:
        f = ImageFont.truetype(FONTS[name], size)
        try:
            f.set_variation_by_axes([wght])
        except Exception:
            pass
        _fonts[key] = f
    return _fonts[key]


def text_mask(text, fnt, tracking4k=0.0):
    tr = tracking4k * S
    asc, desc = fnt.getmetrics()
    w = fnt.getlength(text) if abs(tr) < 0.01 else sum(fnt.getlength(c) for c in text) + tr * (len(text) - 1)
    pad = int(fnt.size * 0.3) + 6
    m = Image.new('L', (int(math.ceil(w)) + 2 * pad, asc + desc + 2 * pad), 0)
    d = ImageDraw.Draw(m)
    if abs(tr) < 0.01:
        d.text((pad, pad), text, font=fnt, fill=255)
    else:
        x = pad
        for c in text:
            d.text((x, pad), c, font=fnt, fill=255)
            x += fnt.getlength(c) + tr
    return m


def colorize(mask, color, gradient=None):
    if gradient:
        top, bot = gradient
        h = mask.size[1]
        col = np.zeros((h, 1, 3), np.float32)
        t = np.linspace(0, 1, h)[:, None]
        col[:, 0, :] = np.array(top) * (1 - t) + np.array(bot) * t
        img = Image.fromarray(np.repeat(col, mask.size[0], axis=1).astype(np.uint8), 'RGB').convert('RGBA')
    else:
        img = Image.new('RGBA', mask.size, tuple(color) + (255,))
    img.putalpha(mask)
    return img


def with_alpha(img, a):
    if a >= 0.999:
        return img
    img = img.copy()
    img.putalpha(img.getchannel('A').point(lambda v: int(v * a)))
    return img


def blurred(img, r4k):
    r = r4k * S
    return img.filter(ImageFilter.GaussianBlur(r)) if r > 0.3 else img


def shadow_of(img, r4k=12, strength=0.6):
    sh = Image.new('RGBA', img.size, (0, 0, 0, 255))
    sh.putalpha(img.getchannel('A').point(lambda v: int(v * strength)))
    return blurred(sh, r4k)


def glow_of(img, color, r4k, strength):
    g = Image.new('RGBA', img.size, tuple(color) + (255,))
    g.putalpha(img.getchannel('A').point(lambda v: int(min(255, v * strength))))
    return blurred(g, r4k)


_scrims = {}


def scrim(w, h, strength):
    """A soft dark oval behind text, so white type reads over a bright sky: full strength in the middle, falling smoothly
    to nothing at the oval's edge (made once per size)."""
    w, h = max(8, int(w)), max(8, int(h))
    if (w, h) not in _scrims:
        yy, xx = np.mgrid[0:h, 0:w]
        r = np.sqrt(((xx + 0.5 - w / 2) / (w / 2)) ** 2 + ((yy + 0.5 - h / 2) / (h / 2)) ** 2)
        t = np.clip((r - 0.3) / 0.7, 0, 1)
        _scrims[(w, h)] = 1 - t * t * (3 - 2 * t)
    a = (_scrims[(w, h)] * 255 * strength).astype(np.uint8)
    img = Image.new('RGBA', (w, h), (0, 0, 0, 255))
    img.putalpha(Image.fromarray(a, 'L'))
    return img


def put(canvas, img, cx, cy):
    """img centred at (cx, cy) on the canvas, clipped to it."""
    x, y = int(round(cx - img.size[0] / 2)), int(round(cy - img.size[1] / 2))
    sx, sy = max(0, -x), max(0, -y)
    ex, ey = min(img.size[0], canvas.size[0] - x), min(img.size[1], canvas.size[1] - y)
    if ex <= sx or ey <= sy:
        return
    canvas.alpha_composite(img.crop((sx, sy, ex, ey)), (x + sx, y + sy))


def ease_out(x):
    x = clamp(x, 0.0, 1.0)
    return 1 - (1 - x) ** 3


def ease_in(x):
    x = clamp(x, 0.0, 1.0)
    return x ** 3


def env(i, n, fin, fout, delay=0):
    """(in, out) progress for frame i of n: in eases 0->1 over fin seconds after delay, out 0->1 over the last fout."""
    pin = ease_out((i - delay * FPS) / max(1, fin * FPS))
    pout = ease_in((i - (n - fout * FPS)) / max(1, fout * FPS))
    return pin, pout


def line_layer(spec, i, n):
    """One animated line: rises, un-blurs, tracks in. spec: text, font (name, size, wght), color | gradient, track
    (from, to), delay, fin, blur, rise, glow (color, r, strength), shadow."""
    pin, pout = env(i, n, spec.get('fin', 0.7), spec.get('fout', 0.5), spec.get('delay', 0))
    if pin <= 0.001:
        return None, 0
    t0, t1 = spec.get('track', (0, 0))
    fnt = font(*spec['font'])
    mask = text_mask(spec['text'], fnt, t1 + (t0 - t1) * (1 - pin))
    img = colorize(mask, spec.get('color', C['colors']['white']), spec.get('gradient'))
    layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
    if spec.get('shadow', True):
        layer.alpha_composite(shadow_of(img, 14, 0.55))
    if spec.get('glow'):
        gc, gr, gs = spec['glow']
        layer.alpha_composite(glow_of(img, gc, gr, gs))
    layer.alpha_composite(img)
    blur = spec.get('blur', 8) * (1 - pin) + 6 * pout
    layer = blurred(layer, blur)
    alpha = pin * (1 - pout)
    rise = spec.get('rise', 24) * (1 - pin) * S - 10 * pout * S
    return with_alpha(layer, alpha), rise


# ---- elements: each a box on the frame and a function of its own frame index
def el_place(el, i):
    """The place name, bottom centre: 'Mall Road (Day)'; on a strobe the suffix flips with the picture."""
    cw, ch = el['w'], el['h']
    canvas = Image.new('RGBA', (cw, ch), (0, 0, 0, 0))
    n = el['n']
    pin, pout = env(i, n, 0.65, 0.5)
    if pin <= 0.001:
        return canvas
    lf = el['local0'] + i                            # the frame within its shot
    day = is_day(lf, el.get('mid'))
    suffix = el['suffix'] if el.get('suffix') else ('(Day)' if day else '(Night)')
    scol = C['colors'][el.get('suffix_color') or ('day' if day else 'night')]
    flip = el.get('mid') is not None and lf > 0 and is_day(lf, el['mid']) != is_day(lf - 1, el['mid'])
    flash = el.get('mid') is not None and lf in (el['mid'], el['mid'] + 1)
    track = 10 * (1 - pin)
    name = colorize(text_mask(el['title'], font('corm', 112, 600), track), C['colors']['white'])
    suf = colorize(text_mask(suffix, font('cormi', 92, 500), track), scol)
    gap = int(26 * S)
    w = name.size[0] + suf.size[0] - int(2 * (int(font('corm', 112, 600).size * 0.3) + 6)) + gap
    tw = name.size[0] + suf.size[0] + gap
    row = Image.new('RGBA', (tw, max(name.size[1], suf.size[1])), (0, 0, 0, 0))
    row.alpha_composite(name, (0, 0))
    row.alpha_composite(suf, (name.size[0] + gap - int(2 * (int(font('cormi', 92, 500).size * 0.3) + 6)) // 2,
                              int((name.size[1] - suf.size[1]) * 0.62)))
    vis = pin * (1 - pout)
    put(canvas, scrim(min(cw, row.size[0] * 1.7), min(ch, row.size[1] * 2.4), 0.45 * vis), cw / 2, ch * 0.42)
    layer = Image.new('RGBA', row.size, (0, 0, 0, 0))
    layer.alpha_composite(shadow_of(row, 16, 0.65))
    if flash or flip:
        layer.alpha_composite(glow_of(row, scol, 22, 1.4 if flash else 0.8))
    layer.alpha_composite(row)
    layer = blurred(layer, 8 * (1 - pin) + 6 * pout)
    layer = with_alpha(layer, pin * (1 - pout))
    jitter = (8 if (lf % 2) else -8) * S if flip else 0
    cy = ch * 0.42 + 26 * (1 - pin) * S - 10 * pout * S
    put(canvas, layer, cw / 2 + jitter, cy)
    # a thin rule under it, growing from the centre
    g = ease_out((i - 0.15 * FPS) / (0.8 * FPS))
    lw = int(w * 0.5 * g)
    if lw > 2:
        ry = int(cy + row.size[1] * 0.36)
        rule = Image.new('RGBA', (lw, max(1, int(3 * S))), (255, 255, 255, int(150 * pin * (1 - pout))))
        put(canvas, rule, cw / 2 + jitter, ry)
    return canvas


def el_stack(el, i):
    """Centre text: lines stacked, each animating in on its own delay; the whole fades / blurs out at its end."""
    cw, ch = el['w'], el['h']
    canvas = Image.new('RGBA', (cw, ch), (0, 0, 0, 0))
    n = el['n']
    lines = el['lines']
    # the layout from the lines' final sizes (steady while they animate)
    heights = []
    for sp in lines:
        fnt = font(*sp['font'])
        asc, desc = fnt.getmetrics()
        heights.append(asc + desc)
    gaps = [sp.get('gap', 30) * S for sp in lines]
    total = sum(heights) + sum(gaps[:-1])
    y = ch / 2 - total / 2 + el.get('dy', 0) * S
    scale = 1 + el.get('push', 0) * (i / max(1, n))
    if el.get('scrim', 0.45) > 0:
        bw = 0
        for sp in lines:
            fnt = font(*sp['font'])
            if sp.get('words'):
                wd = sum(fnt.getlength(w) for w in sp['words']) + fnt.getlength(' ') * 1.6 * (len(sp['words']) - 1)
            else:
                tr = sp.get('track', (0, 0))[1] * S
                wd = fnt.getlength(sp['text']) + tr * len(sp['text']) + (2 * sp.get('rules', 0) * S + 80 * S if sp.get('rules') else 0)
            bw = max(bw, wd)
        vin, vout = env(i, n, 0.6, el.get('fout', 0.5))
        put(canvas, scrim(min(cw, bw * 1.6 + 300 * S), min(ch, total * 2.4 + 200 * S), el.get('scrim', 0.45) * vin * (1 - vout)),
            cw / 2, y + total / 2)
    for sp, hgt, gp in zip(lines, heights, gaps):
        sp = dict(sp)
        sp.setdefault('fout', el.get('fout', 0.5))
        if sp.get('words'):
            put_words(canvas, sp, i, n, ch, y + hgt / 2)
        else:
            layer, rise = line_layer(sp, i, n)
            if layer is not None:
                if scale != 1:
                    layer = layer.resize((int(layer.size[0] * scale), int(layer.size[1] * scale)), Image.BILINEAR)
                put(canvas, layer, cw / 2, y + hgt / 2 + rise)
                if sp.get('rules'):
                    pin, pout = env(i, n, 1.0, sp['fout'], sp.get('delay', 0))
                    L = int(sp['rules'] * S * pin)
                    if L > 2:
                        rule = Image.new('RGBA', (L, max(1, int(3 * S))), (255, 255, 255, int(170 * pin * (1 - pout))))
                        half = layer.size[0] / 2 - (int(font(*sp['font']).size * 0.3) + 6) + 40 * S
                        put(canvas, rule, cw / 2 - half - L / 2, y + hgt / 2 + rise)
                        put(canvas, rule, cw / 2 + half + L / 2, y + hgt / 2 + rise)
        y += hgt + gp
    return canvas


def put_words(canvas, sp, i, n, ch, cy):
    fnt = font(*sp['font'])
    words = sp['words']
    space = fnt.getlength(' ') * 1.6
    widths = [fnt.getlength(w) for w in words]
    total = sum(widths) + space * (len(words) - 1)
    x = canvas.size[0] / 2 - total / 2
    for k, (w, wd) in enumerate(zip(words, widths)):
        spk = dict(sp, text=w, delay=sp.get('delay', 0) + k * sp.get('stagger', 0.3))
        layer, rise = line_layer(spk, i, n)
        if layer is not None:
            put(canvas, layer, x + wd / 2, cy + rise)
        x += wd + space


def render_frame(el, i):
    return el_place(el, i) if el['kind'] == 'place' else el_stack(el, i)


def _work(args):
    el, i = args
    img = render_frame(el, i)
    img.save(os.path.join(WORK, 'el', el['id'], f'{i:04d}.png'), compress_level=1)
    return 1


# ---- the elements of each piece
def place_el(id_, seg, a, b, suffix=None, suffix_color=None, mid=None):
    w, h = int(2900 * S), int(400 * S)
    return dict(id=id_, kind='place', start=seg['start'] + a, n=b - a, local0=a, mid=mid, title=seg['title'],
                suffix=suffix, suffix_color=suffix_color, w=w, h=h, x=(W - w) // 2, y=int(H * 0.885 - h * 0.42))


def stack_el(id_, start, n, lines, **kw):
    w, h = int(3700 * S), int(1250 * S)
    return dict(id=id_, kind='stack', start=start, n=n, lines=lines, w=w, h=h, x=(W - w) // 2, y=(H - h) // 2, **kw)


def trailer_elements(segs, total, end_n):
    T = C['text']
    col = C['colors']
    gold = (col['gold_top'], col['gold_bottom'])
    els = []
    sun = segs[0]
    els.append(stack_el('hook', 12, sun['n'] - 12, [
        dict(text=T['hook'][0], font=('mont', 78, 500), track=(60, 30), delay=0.0, fin=0.8, gap=34),
        dict(text=T['hook'][1], font=('cinzel', 196, 700), track=(40, 12), delay=0.45, fin=0.9, blur=14,
             glow=((255, 200, 150), 30, 0.5)),
    ], fout=0.35, dy=-60))
    els.append(place_el('p_sun', sun, 54, sun['n'] - 2, suffix=sun['suffix'], suffix_color='sunrise'))
    for seg in segs[1:]:
        n, mid, k = seg['n'], seg['mid'], seg['key']
        if seg['window'] == 'none':
            continue
        if seg['window'] == 'mid':
            a, b = max(18, mid - 150), min(n - 18, mid + 200)
        else:   # from up to 4 s before the strobe to 2 s after it
            a, b = max(18, mid - 240), min(n - 18, max(mid + 120, 18 + 260))
        els.append(place_el('p' + k, seg, a, b, mid=mid))
    s2 = segs[2]
    els.append(stack_el('title', s2['start'] + 15, s2['mid'] + 3 - 15, [
        dict(text=T['title_small'], font=('mont', 66, 400), track=(70, 34), delay=0.0, fin=0.8, gap=22),
        dict(text=T['title_big'], font=('cinzel', 270, 800), gradient=gold, track=(90, 24), delay=0.35, fin=1.1,
             blur=18, glow=((255, 190, 110), 40, 0.55), gap=8),
        dict(text=T['title_sub'], font=('cormi', 112, 500), delay=1.1, fin=0.9),
    ], fout=0.08, dy=-90, scrim=0.5))
    els.append(stack_el('clue', s2['start'] + s2['mid'] + 26, s2['n'] - s2['mid'] - 26 - 4, [
        dict(words=T['clue_words'], font=('corm', 124, 600), delay=0.0, stagger=0.36, fin=0.6, rise=30, gap=40),
        dict(text=T['clue_line'], font=('mont', 64, 400), track=(40, 18), delay=2.05, fin=0.8),
    ], fout=0.4, dy=-90))
    s7 = segs[7]
    els.append(stack_el('credit', s7['start'] + 20, 270, [
        dict(text=T['credit_small'], font=('mont', 58, 500), track=(70, 40), color=(240, 240, 240), delay=0.0, fin=0.9,
             rules=170, gap=26),
        dict(text=T['credit_name'], font=('corm', 168, 600), track=(30, 6), delay=0.4, fin=1.0, blur=12,
             glow=((255, 225, 190), 30, 0.4)),
    ], fout=0.6, push=0.035, dy=-40, scrim=0.7))
    s10 = segs[10]
    els.append(stack_el('cta_a', s10['start'] + 18, s10['mid'] - 4 - 18, [
        dict(text=T['cta_a'], font=('corm', 132, 600), delay=0.0, fin=0.8, blur=10),
    ], fout=0.25, dy=-40, scrim=0.62))
    els.append(stack_el('cta_b', s10['start'] + s10['mid'] + 24, s10['n'] - s10['mid'] - 24, [
        dict(text=T['cta_b'][0], font=('corm', 132, 600), delay=0.0, fin=0.8, blur=10, gap=18),
        dict(text=T['cta_b'][1], font=('cormi', 118, 500), delay=0.5, fin=0.8, blur=10,
             glow=((255, 210, 170), 26, 0.35)),
    ], fout=0.45, dy=-40, scrim=0.62))
    els.append(stack_el('end', total + 14, end_n - 14, [
        dict(text=T['title_big'], font=('cinzel', 250, 800), gradient=gold, track=(70, 22), delay=0.0, fin=1.0,
             blur=16, glow=((255, 190, 110), 44, 0.6), gap=10),
        dict(text=T['end_sub'], font=('cormi', 108, 500), delay=0.45, fin=0.8, gap=60),
        dict(text=T['credit_name'], font=('mont', 52, 400), track=(30, 16), color=(220, 220, 220), delay=0.9, fin=0.8),
    ], fout=0.5, dy=-20, scrim=0))
    return els


def film_elements(segs, kind):
    els = []
    for seg in segs[1:]:
        n, mid, k = seg['n'], seg['mid'], seg['key']
        if seg['window'] == 'none':
            continue
        if k in ('7', '9'):
            a, b = max(18, mid - 150), min(n - 18, mid + 160)
        else:
            a, b = 18, min(n - 18, 18 + 290)
        els.append(place_el(f'{kind}_p{k}', seg, a, b, suffix='(Day)' if kind == 'day' else '(Night)',
                            suffix_color=kind))
    return els


def shifted(els, by):
    out = []
    for e in els:
        e = dict(e)
        e['start'] -= by
        out.append(e)
    return out


def make_graphics(els):
    tasks = []
    for el in els:
        d = os.path.join(WORK, 'el', el['id'])
        if os.path.isdir(d):
            shutil.rmtree(d)
        os.makedirs(d)
        tasks += [(el, i) for i in range(el['n'])]
    with Pool(max(2, (os.cpu_count() or 4) - 1)) as pool:
        done = 0
        for _ in pool.imap_unordered(_work, tasks, chunksize=16):
            done += 1
    print(f'graphics: {len(els)} elements, {done} frames')


# ---------------------------------------------------------------------------------------------------- sound
_audio = {}


def audio_file(name):
    for r, _, files in os.walk(C['audio_dir']):
        for f in files:
            if os.path.splitext(f)[0] == name and f.endswith(('.ogg', '.wav', '.mp3')):
                return os.path.join(r, f)
    raise FileNotFoundError(name)


def load(name):
    if name not in _audio:
        r = subprocess.run([FFMPEG, '-v', 'error', '-i', audio_file(name), '-f', 'f32le', '-ac', '2', '-ar', str(SR), '-'],
                           capture_output=True)
        a = np.frombuffer(r.stdout, np.float32).reshape(-1, 2).astype(np.float64)
        _audio[name] = a
    return _audio[name]


def rms_norm(a, db=-24.0):
    r = math.sqrt(float(np.mean(a ** 2)) + 1e-12)
    return a * (10 ** (db / 20) / r)


def looped(a, n):
    if len(a) >= n:
        return a[:n]
    reps = n // len(a) + 1
    return np.tile(a, (reps, 1))[:n]


def fade(a, fin, fout):
    a = a.copy()
    n = len(a)
    i, o = min(n, int(fin * SR)), min(n, int(fout * SR))
    if i > 0:
        a[:i] *= np.linspace(0, 1, i)[:, None]
    if o > 0:
        a[n - o:] *= np.linspace(1, 0, o)[:, None]
    return a


def add(mix, sig, at, gain=1.0):
    s = int(round(at * SR))
    if s < 0:
        sig, s = sig[-s:], 0
    e = min(len(mix), s + len(sig))
    if e > s:
        mix[s:e] += sig[:e - s] * gain


def bed(spec, seconds, offset):
    """An ambience bed: the listed loops at their gains (each levelled first), from `offset` s into them."""
    n = int(round(seconds * SR))
    out = np.zeros((n, 2))
    for name, g in spec:
        a = rms_norm(load(name))
        off = int(offset * SR) % len(a)
        out += looped(np.roll(a, -off, axis=0), n) * g
    return out


rng = np.random.default_rng(7)


def noise(n, ch=2):
    return rng.standard_normal((n, ch))


def spectral(x, fn):
    X = np.fft.rfft(x, axis=0)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    return np.fft.irfft(X * fn(np.maximum(f, 1.0))[:, None], n=len(x), axis=0)


def lowpass(x, fc):
    return spectral(x, lambda f: 1 / (1 + (f / fc) ** 4))


def highpass(x, fc):
    return spectral(x, lambda f: (f / fc) ** 4 / (1 + (f / fc) ** 4))


def sweep_noise(dur, f0, f1, bw=0.5):
    n = int(dur * SR)
    x = noise(n)
    frame, hop = 2048, 512
    win = np.hanning(frame)[:, None]
    out = np.zeros((n + frame, 2))
    f = np.fft.rfftfreq(frame, 1 / SR)
    for s in range(0, n, hop):
        seg = np.zeros((frame, 2))
        part = x[s:s + frame]
        seg[:len(part)] = part
        fc = f0 * (f1 / f0) ** (s / max(1, n))
        mask = np.exp(-0.5 * (np.log2(np.maximum(f, 1) / fc) / bw) ** 2)
        y = np.fft.irfft(np.fft.rfft(seg * win, axis=0) * mask[:, None], n=frame, axis=0)
        out[s:s + frame] += y * win
    out = out[:n]
    return out / (np.max(np.abs(out)) + 1e-9)


def reverb(x, rt=1.4, mix=0.3):
    n = int(rt * SR)
    t = np.arange(n) / SR
    ir = noise(n) * np.exp(-t * 6.9 / rt)[:, None]
    ir = lowpass(ir, 6000)
    ir /= np.sqrt(np.sum(ir ** 2, axis=0, keepdims=True)) + 1e-9
    L = len(x) + n
    nfft = 1 << (L - 1).bit_length()
    wet = np.fft.irfft(np.fft.rfft(x, nfft, axis=0) * np.fft.rfft(ir, nfft, axis=0), nfft, axis=0)[:L]
    dry = np.zeros((L, 2))
    dry[:len(x)] = x
    return dry * (1 - mix) + wet * mix


def boom(dur=1.8, f_hi=55, f_lo=30, tau=0.45):
    t = np.arange(int(dur * SR)) / SR
    f = f_lo + f_hi * np.exp(-t * 7)
    ph = 2 * np.pi * np.cumsum(f) / SR
    s = np.sin(ph) * np.exp(-t / tau) + 0.35 * np.sin(2 * ph) * np.exp(-t / (tau * 0.5))
    s = np.tanh(1.8 * s)
    click = lowpass(noise(int(0.012 * SR)), 3000) * np.exp(-np.arange(int(0.012 * SR)) / SR / 0.003)[:, None]
    out = np.repeat(s[:, None], 2, axis=1)
    out[:len(click)] += click * 0.5
    return out / (np.max(np.abs(out)) + 1e-9)


def riser(dur=0.85):
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = sweep_noise(dur, 250, 7000, 0.55) * ((t / dur) ** 2.2)[:, None]
    f = 180 * (5 ** (t / dur))
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * (t / dur) ** 3 * 0.25
    x += tone[:, None]
    x[-int(0.01 * SR):] *= np.linspace(1, 0, int(0.01 * SR))[:, None]
    return x / (np.max(np.abs(x)) + 1e-9)


def tick():
    n = int(0.018 * SR)
    x = highpass(noise(n), 2500) * np.exp(-np.arange(n) / SR / 0.004)[:, None]
    return x / (np.max(np.abs(x)) + 1e-9)


def braam(dur=3.4):
    t = np.arange(int(dur * SR)) / SR
    s = np.zeros_like(t)
    for f, a in ((55, 1.0), (55.4, 0.8), (82.5, 0.6), (110, 0.45), (164.8, 0.25)):
        s += a * (2 * ((t * f) % 1) - 1)
    s = lowpass(np.repeat(s[:, None], 2, axis=1), 700)
    e = np.minimum(1, t / 0.06) * np.exp(-t / 1.2)
    s = s * e[:, None]
    s = s / (np.max(np.abs(s)) + 1e-9)
    b = boom(dur, 60, 28, 0.7)
    out = 0.7 * s + 0.6 * b[:len(s)]
    out = reverb(out, 2.0, 0.35)
    return out / (np.max(np.abs(out)) + 1e-9)


def shimmer(dur=3.0):
    t = np.arange(int(dur * SR)) / SR
    s = np.zeros((len(t), 2))
    for k, (f, a) in enumerate(((1318.5, 0.5), (1760, 0.6), (2637, 0.4), (3520, 0.3), (5274, 0.15))):
        s[:, 0] += a * np.sin(2 * np.pi * f * t + k)
        s[:, 1] += a * np.sin(2 * np.pi * f * 1.003 * t + 2 * k)
    s *= (1 + 0.3 * np.sin(2 * np.pi * 6 * t))[:, None]
    s += 0.25 * highpass(noise(len(t)), 6000)
    e = np.minimum(1, t / 0.6) ** 2 * np.exp(-np.maximum(0, t - 0.6) / 0.9)
    s *= e[:, None]
    s = reverb(s, 2.2, 0.45)
    return s / (np.max(np.abs(s)) + 1e-9)


def reverse_swell(dur=1.3):
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = highpass(noise(n), 3500) * ((t / dur) ** 3)[:, None]
    x[-int(0.02 * SR):] *= np.linspace(1, 0, int(0.02 * SR))[:, None]
    return x / (np.max(np.abs(x)) + 1e-9)


def spot_time(sp, seg):
    at = sp['at']
    if isinstance(at, str) and at.startswith('mid'):
        return (seg['start'] + seg['mid']) / FPS + float(at[3:] or 0)
    return seg['start'] / FPS + float(at)


def write_wav(path, mix):
    peak = np.max(np.abs(mix)) + 1e-9
    mix = np.tanh(mix / peak * 1.1) / math.tanh(1.1) * 0.9
    data = (mix * 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def make_trailer_audio(segs, total, end_n):
    secs = (total + end_n) / FPS
    mix = np.zeros((int(round(secs * SR)) + SR, 2))
    amb = C['ambience']
    xf = 0.12
    for seg in segs:
        a, n = seg['start'] / FPS, seg['n'] / FPS
        spec = amb[seg['key']]
        if seg['mid'] is None:
            add(mix, fade(bed(spec['day'], n + xf, a), xf, xf), a)
            continue
        m = seg['mid'] / FPS
        add(mix, fade(bed(spec['day'], m + 0.04, a), xf, 0.08), a)
        add(mix, fade(bed(spec['night'], n - m + xf, a + m), 0.05, xf), a + m)
    for sp in C['spot_sounds']:
        seg = next(s for s in segs if s['key'] == sp['where'])
        t = spot_time(sp, seg)
        if seg['mid'] is not None and sp.get('kind'):
            mt = (seg['start'] + seg['mid']) / FPS
            if (sp['kind'] == 'day') != (t < mt):
                continue
        add(mix, rms_norm(load(sp['file']), -18), t, sp['gain'])
    # the strobes: a riser into each, a boom on the first night frame, a tick on each flip
    r, b, tk = riser(), boom(), tick()
    for seg in segs[1:]:
        mt = (seg['start'] + seg['mid']) / FPS
        add(mix, r, mt - len(r) / SR, 0.09)
        add(mix, b, mt, 0.22)
        for f in range(seg['mid'], seg['mid'] + STROBE + 1):
            if is_day(f, seg['mid']) != is_day(f - 1, seg['mid']):
                add(mix, tk, (seg['start'] + f) / FPS, 0.05)
    # the titles
    wh = rms_norm(load('whoosh_1'), -16)
    add(mix, wh, 0.05, 0.35)
    s2, s7, s10 = segs[2], segs[7], segs[10]
    add(mix, braam(), (s2['start'] + 15) / FPS + 0.3, 0.5)
    add(mix, shimmer(), (s7['start'] + 20) / FPS + 0.2, 0.12)
    add(mix, wh, (s10['start'] + 18) / FPS - 0.1, 0.25)
    add(mix, wh, (s10['start'] + s10['mid'] + 24) / FPS - 0.1, 0.25)
    # the end: the beds out, a swell into the logo, a boom and a shimmer tail
    te = total / FPS
    e0 = int(te * SR)
    k = int(1.2 * SR)
    mix[e0:e0 + k] *= np.linspace(1, 0, k)[:, None]
    mix[e0 + k:] = 0
    rs = reverse_swell()
    add(mix, rs, te + 0.25 - len(rs) / SR, 0.18)
    add(mix, braam(), te + 0.25, 0.55)
    add(mix, shimmer(), te + 0.35, 0.1)
    mix = mix[:int(round(secs * SR))]
    write_wav(os.path.join(WORK, 'trailer.wav'), mix)
    print(f'audio: trailer {secs:.2f} s')


def make_film_audio(segs, kind):
    shots = segs[1:]
    start0 = shots[0]['start']
    secs = sum(s['n'] for s in shots) / FPS
    mix = np.zeros((int(round(secs * SR)) + SR, 2))
    xf = 0.12
    for seg in shots:
        a, n = (seg['start'] - start0) / FPS, seg['n'] / FPS
        add(mix, fade(bed(C['ambience'][seg['key']][kind], n + xf, a), xf, xf), a)
    for sp in C['spot_sounds']:
        if sp['where'] == 'sunrise' or sp.get('kind') not in (None, kind):
            continue
        seg = next(s for s in segs if s['key'] == sp['where'])
        t = spot_time(sp, seg) - start0 / FPS
        if kind == 'night' and isinstance(sp['at'], str):
            t = (seg['start'] - start0) / FPS + 0.8
        add(mix, rms_norm(load(sp['file']), -18), t, sp['gain'])
    mix = mix[:int(round(secs * SR))]
    k = int(1.0 * SR)
    mix[-k:] *= np.linspace(1, 0, k)[:, None]
    write_wav(os.path.join(WORK, f'film_{kind}.wav'), mix)
    print(f'audio: film {kind} {secs:.2f} s')


# ---------------------------------------------------------------------------------------------------- encodes
def overlay_chain(lines, inputs, els, last):
    for j, el in enumerate(els):
        ii = inputs.count('-i')
        inputs += ['-framerate', str(FPS), '-start_number', '0', '-i', os.path.join(WORK, 'el', el['id'], '%04d.png')]
        lines.append(f"[{ii}:v]setpts=PTS+{el['start'] / FPS:.6f}/TB[e{j}]")
        lines.append(f"[{last}][e{j}]overlay=x={el['x']}:y={el['y']}:eof_action=pass[o{j}]")
        last = f'o{j}'
    return last


def encode(inputs, lines, last, audio, seconds, out):
    lines.append(f'[{last}]colormatrix=bt601:bt709,format=yuv420p[vout]')
    ai = len([x for x in inputs if x == '-i'])
    inputs += ['-i', audio]
    graph = os.path.join(WORK, os.path.splitext(os.path.basename(out))[0] + '.graph')
    open(graph, 'w', encoding='utf-8').write(';\n'.join(lines))
    e = C['encode']
    cmd = [FFMPEG, '-y', '-hide_banner', '-loglevel', 'error', '-stats'] + inputs + [
        '-/filter_complex', graph, '-map', '[vout]', '-map', f'{ai}:a',
        '-c:v', 'libx264', '-preset', e['preset'], '-crf', str(e['crf']), '-profile:v', 'high', '-pix_fmt', 'yuv420p',
        '-r', str(FPS), '-fps_mode', 'cfr', '-g', str(FPS * 2),
        '-colorspace', 'bt709', '-color_primaries', 'bt709', '-color_trc', 'bt709', '-color_range', 'tv',
        '-af', f"loudnorm=I={e['loudness']}:TP=-1.5:LRA=11", '-c:a', 'aac', '-b:a', f"{e['audio_kbps']}k", '-ar', str(SR),
        '-t', f'{seconds:.4f}', '-movflags', '+faststart', out]
    print('encode:', os.path.basename(out))
    r = subprocess.run(cmd)
    if r.returncode != 0:
        raise SystemExit(f'ffmpeg failed ({r.returncode}) on {out}')


def encode_trailer(segs, total, end_n):
    inputs, lines = [], []
    sr = C['sunrise']
    inputs += ['-ss', str(sr['from']), '-t', str(sr['seconds']), '-i', os.path.join(SRC, sr['file'])]
    for seg in segs[1:]:
        inputs += ['-i', clip('day', seg['shot'])]
    for seg in segs[1:]:
        inputs += ['-i', clip('night', seg['shot'])]
    lines.append(f"[0:v]{grade(os.path.join(SRC, sr['file']), 'morning')},trim=end_frame={segs[0]['n']},setpts=PTS-STARTPTS[s0]")
    fl = C['strobe']['flash']
    for k, seg in enumerate(segs[1:], 1):
        mid = seg['mid']
        day = f"lt(t,{(mid - 0.5) / FPS:.5f})" + ''.join(f"+between(t,{(a - 0.5) / FPS:.5f},{(b - 0.5) / FPS:.5f})" for a, b in day_runs(mid))
        lines.append(f"[{k}:v]setpts=PTS-STARTPTS,{grade(clip('day', seg['shot']), 'day')}[d{k}]")
        lines.append(f"[{10 + k}:v]setpts=PTS-STARTPTS,{grade(clip('night', seg['shot']), 'night')}[n{k}]")
        lines.append(f"[n{k}][d{k}]overlay=enable='{day}',"
                     f"drawbox=x=0:y=0:w=iw:h=ih:color=white@{fl[0]}:t=fill:enable='between(t,{(mid - 0.5) / FPS:.5f},{(mid + 0.5) / FPS:.5f})',"
                     f"drawbox=x=0:y=0:w=iw:h=ih:color=white@{fl[1]}:t=fill:enable='between(t,{(mid + 0.5) / FPS:.5f},{(mid + 1.5) / FPS:.5f})'[s{k}]")
    lines.append(''.join(f'[s{k}]' for k in range(len(segs))) + f'concat=n={len(segs)}:v=1:a=0[c]')
    te = total / FPS
    secs = (total + end_n) / FPS
    lines.append(f"[c]vignette=angle={C['grade']['vignette']},tpad=stop_mode=clone:stop_duration={end_n / FPS:.4f},"
                 f"fade=t=out:st={te:.4f}:d=0.9[b0]")
    pb = C['progress_bar']
    bh = max(2, int(round(pb['height'] * S)))
    lines.append(f"color=c=white@{pb['track_alpha']}:s={W}x{bh}:r={FPS}:d={secs:.4f},format=rgba[trk]")
    lines.append(f"color=c={pb['bar_rgba']}:s={W}x{bh}:r={FPS}:d={secs:.4f},format=rgba[bar]")
    lines.append(f"[b0][trk]overlay=x=0:y=H-h:enable='lt(t,{te:.4f})'[b1]")
    lines.append(f"[b1][bar]overlay=x='-w+w*min(1,t/{te:.4f})':y=H-h:enable='lt(t,{te:.4f})'[b2]")
    last = overlay_chain(lines, inputs, trailer_elements(segs, total, end_n), 'b2')
    os.makedirs(OUT, exist_ok=True)
    encode(inputs, lines, last, os.path.join(WORK, 'trailer.wav'), secs, os.path.join(OUT, 'NainiVerse_First_Glimpse_1080p60.mp4'))


def encode_film(segs, kind):
    inputs, lines = [], []
    shots = segs[1:]
    for seg in shots:
        inputs += ['-i', clip(kind, seg['shot'])]
    for k, seg in enumerate(shots):
        lines.append(f"[{k}:v]setpts=PTS-STARTPTS,{grade(clip(kind, seg['shot']), kind)}[s{k}]")
    lines.append(''.join(f'[s{k}]' for k in range(len(shots))) + f'concat=n={len(shots)}:v=1:a=0[c]')
    secs = sum(s['n'] for s in shots) / FPS
    lines.append(f"[c]vignette=angle={C['grade']['vignette']},fade=t=in:st=0:d=0.5,fade=t=out:st={secs - 0.8:.4f}:d=0.8[b0]")
    last = overlay_chain(lines, inputs, shifted(film_elements(segs, kind), shots[0]['start']), 'b0')
    name = 'NainiVerse_Day_1080p60.mp4' if kind == 'day' else 'NainiVerse_Night_1080p60.mp4'
    encode(inputs, lines, last, os.path.join(WORK, f'film_{kind}.wav'), secs, os.path.join(OUT, name))


# ---------------------------------------------------------------------------------------------------- checks
def preview(segs, total, end_n):
    """Stills of key moments: the graded frame (day or night as the strobe has it) with the graphics on it."""
    d = os.path.join(WORK, 'preview')
    os.makedirs(d, exist_ok=True)
    els = trailer_elements(segs, total, end_n)
    picks = {'hook': 120, 'sun_title': 200}
    s1, s2, s7, s9, s10 = segs[1], segs[2], segs[7], segs[9], segs[10]
    picks['busstand_day'] = s1['start'] + 90
    picks['title'] = s2['start'] + 200
    picks['strobe_flash'] = s2['start'] + s2['mid']
    picks['strobe_day'] = s2['start'] + s2['mid'] + 2
    picks['clue'] = s2['start'] + s2['mid'] + 220
    picks['credit'] = s7['start'] + 160
    picks['church_night'] = s7['start'] + s7['mid'] + 60
    picks['peak_day'] = s9['start'] + s9['mid'] - 40
    picks['cta_a'] = s10['start'] + 200
    picks['cta_b'] = s10['start'] + s10['mid'] + 200
    picks['end'] = total + 120
    sheet = []
    for name, F in picks.items():
        if F >= total:
            seg = segs[10]
            lf, kind, src = seg['n'] - 1, 'night', clip('night', seg['shot'])
        else:
            seg = max((s for s in segs if s['start'] <= F), key=lambda s: s['start'])
            lf = F - seg['start']
            if seg['mid'] is None:
                kind, src, lf = 'morning', os.path.join(SRC, C['sunrise']['file']), lf + round(C['sunrise']['from'] * FPS)
            else:
                kind = 'day' if is_day(lf, seg['mid']) else 'night'
                src = clip(kind, seg['shot'])
        tmp = os.path.join(d, f'_{name}.png')
        vf = grade(src, kind) + f",vignette=angle={C['grade']['vignette']}"
        if F >= total:
            vf += ',eq=brightness=-1'
        r = run([FFMPEG, '-y', '-v', 'error', '-ss', f'{lf / FPS:.4f}', '-i', src, '-frames:v', '1', '-vf', vf, tmp])
        img = Image.open(tmp).convert('RGBA')
        if seg.get('mid') is not None and lf in (seg['mid'], seg['mid'] + 1) and F < total:
            a = C['strobe']['flash'][lf - seg['mid']]
            img = Image.blend(img, Image.new('RGBA', img.size, (255, 255, 255, 255)), a)
        for el in els:
            if el['start'] <= F < el['start'] + el['n']:
                img.alpha_composite(render_frame(el, F - el['start']), (el['x'], el['y']))
        out = os.path.join(d, f'{name}.jpg')
        img.convert('RGB').save(out, quality=90)
        os.remove(tmp)
        sheet.append((name, img.convert('RGB').resize((640, 360))))
    cols = 3
    rows = math.ceil(len(sheet) / cols)
    sh = Image.new('RGB', (640 * cols, 360 * rows))
    for k, (name, im) in enumerate(sheet):
        ImageDraw.Draw(im).text((6, 4), name, fill=(255, 255, 0))
        sh.paste(im, ((k % cols) * 640, (k // cols) * 360))
    sh.save(os.path.join(d, 'sheet.jpg'), quality=88)
    print('preview:', os.path.join(d, 'sheet.jpg'))


def verify():
    ok = True
    for f in sorted(os.listdir(OUT)):
        if not f.endswith('.mp4'):
            continue
        p = os.path.join(OUT, f)
        r = run([FFPROBE, '-v', 'error', '-show_entries', 'stream=codec_type,width,height,r_frame_rate,avg_frame_rate,nb_frames,duration',
                 '-of', 'json', p])
        st = json.loads(r.stdout)['streams']
        v = next(s for s in st if s['codec_type'] == 'video')
        a = next((s for s in st if s['codec_type'] == 'audio'), None)
        good = v['r_frame_rate'] == '60/1' and v['avg_frame_rate'] == '60/1' and int(v['width']) == W and int(v['height']) == H
        ok &= good and a is not None
        print(f"{f}: {v['width']}x{v['height']} r={v['r_frame_rate']} avg={v['avg_frame_rate']} frames={v.get('nb_frames')} "
              f"video {float(v['duration']):.3f} s, audio {float(a['duration']) if a else 0:.3f} s, {os.path.getsize(p) / 1e6:.0f} MB"
              f" {'OK' if good else 'CHECK'}")
    return ok


def main():
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    os.makedirs(WORK, exist_ok=True)
    segs, total, end_n = timeline()
    print(f'timeline: sunrise {segs[0]["n"]} + shots {total - segs[0]["n"]} frames, end card {end_n}; '
          f'trailer {(total + end_n) / FPS:.2f} s')
    if what in ('analyze', 'all'):
        measure(os.path.join(SRC, C['sunrise']['file']), 'morning')
        for seg in segs[1:]:
            for kind in ('day', 'night'):
                m = measure(clip(kind, seg['shot']), kind)
                print(f"  shot {seg['key']:>2} {kind:5s} Y {m['YAVG']:6.1f} low {m['YLOW']:6.1f} high {m['YHIGH']:6.1f} sat {m['SATAVG']:5.1f}")
    if what in ('preview',):
        preview(segs, total, end_n)
    if what in ('graphics', 'all'):
        els = trailer_elements(segs, total, end_n) + film_elements(segs, 'day') + film_elements(segs, 'night')
        make_graphics(els)
    if what in ('audio', 'all'):
        make_trailer_audio(segs, total, end_n)
        make_film_audio(segs, 'day')
        make_film_audio(segs, 'night')
    if what in ('trailer', 'all'):
        encode_trailer(segs, total, end_n)
    if what in ('films', 'all'):
        encode_film(segs, 'day')
        encode_film(segs, 'night')
    if what in ('verify', 'all'):
        print('verify:', 'OK' if verify() else 'CHECK')


if __name__ == '__main__':
    main()

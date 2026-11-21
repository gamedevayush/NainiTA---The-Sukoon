"""The radio's own stations (owner, 21 Nov 2026, FREEROAM #9: free stations - "jitna free allowance hain vo to kr hi do").
Instead of other people's tracks (licences to check, credits, takedown risk) the game makes its own: original music
synthesised here with numpy, owned by the game. Three stations x three tracks (~100 s each):
  pahadi  - "Pahadi Dhun FM": a bansuri line in a pentatonic (Bhupali-like) scale over a tanpura drone, soft tabla
  lofi    - "Naini Lo-fi": electric-piano chords, a lazy beat, vinyl crackle
  beats   - "Mall Road Beats": a dhol groove, claps, a bright synth hook
Writes Assets/Resources/Radio/<station>/<n>.ogg (ffmpeg, 96 kbps), stations.txt and CREDITS.txt.
Run: python Tools/radio/make_stations.py   (numpy; ffmpeg on PATH or the WinGet one)
"""
import os, shutil, subprocess, wave
import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Radio")
SR = 32000
rng = np.random.default_rng(2207)


def ffmpeg():
    p = shutil.which("ffmpeg")
    if p:
        return p
    base = os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WinGet\Packages")
    for d, _, files in os.walk(base):
        if "ffmpeg.exe" in files:
            return os.path.join(d, "ffmpeg.exe")
    raise SystemExit("ffmpeg not found")


def env(n, a, d, s, r):
    """ADSR over n samples (seconds for a, d, r; s = sustain level)."""
    e = np.ones(n) * s
    A, D, Rl = int(a * SR), int(d * SR), int(r * SR)
    A = min(A, n); e[:A] = np.linspace(0, 1, A, endpoint=False)
    D = min(D, max(0, n - A)); e[A:A + D] = np.linspace(1, s, D, endpoint=False)
    Rl = min(Rl, n); e[n - Rl:] *= np.linspace(1, 0, Rl)
    return e


def lowpass(x, k):
    """A cheap low-pass: a moving average of k samples (twice)."""
    if k <= 1:
        return x
    w = np.ones(k) / k
    return np.convolve(np.convolve(x, w, "same"), w, "same")


def add(buf, sig, at):
    i = int(at * SR)
    if i >= len(buf):
        return
    j = min(len(buf), i + len(sig))
    buf[i:j] += sig[: j - i]


# ---------------------------------------------------------------- instruments
def bansuri(f, dur, vib=5.2, glide_from=None):
    n = int(dur * SR)
    t = np.arange(n) / SR
    fr = np.full(n, f)
    if glide_from:   # meend: slide into the note
        g = min(n, int(0.12 * SR))
        fr[:g] = np.linspace(glide_from, f, g)
    fr = fr * (1 + 0.006 * np.sin(2 * np.pi * vib * t) * np.clip(t / 0.4, 0, 1))
    ph = 2 * np.pi * np.cumsum(fr) / SR
    tone = np.sin(ph) + 0.18 * np.sin(2 * ph) + 0.06 * np.sin(3 * ph)
    breath = lowpass(rng.standard_normal(n), 6) * 0.05
    return (tone + breath) * env(n, 0.08, 0.2, 0.8, min(0.25, dur * 0.4))


def tanpura(f, dur):
    n = int(dur * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for h in range(1, 12):   # rich, slowly beating harmonics (the jawari's buzz)
        s += (0.6 ** h) * np.sin(2 * np.pi * f * h * t * (1 + 0.0007 * h) + h)
    return s * np.exp(-t * 0.9) * env(n, 0.01, 0.1, 1, 0.3)


def tabla(kind, dur=0.5):
    n = int(dur * SR)
    t = np.arange(n) / SR
    if kind == "ge":   # the bayan: a low thud gliding down
        f = 120 * np.exp(-t * 6) + 70
        s = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 7)
    elif kind == "na":   # the dayan's ringing edge
        s = (np.sin(2 * np.pi * 520 * t) + 0.5 * np.sin(2 * np.pi * 1040 * t)) * np.exp(-t * 14)
        s += lowpass(rng.standard_normal(n), 2) * np.exp(-t * 60) * 0.3
    else:   # "ti": a dry tap
        s = lowpass(rng.standard_normal(n), 3) * np.exp(-t * 45) * 0.6
    return s


def epiano(freqs, dur):
    n = int(dur * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for f in freqs:
        s += np.sin(2 * np.pi * f * t) + 0.25 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-t * 3)
    s *= (1 + 0.15 * np.sin(2 * np.pi * 4.5 * t))   # tremolo
    return s * np.exp(-t * 0.7) * env(n, 0.005, 0.3, 0.7, 0.4) / max(1, len(freqs))


def kick(dur=0.35, f0=110, f1=45):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t * 25)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)


def snare(dur=0.25, tone=190):
    n = int(dur * SR)
    t = np.arange(n) / SR
    return (lowpass(rng.standard_normal(n), 2) * 0.7 + 0.4 * np.sin(2 * np.pi * tone * t)) * np.exp(-t * 18)


def hat(dur=0.06):
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = rng.standard_normal(n)
    return (x - lowpass(x, 4)) * np.exp(-t * 70) * 0.5


def clap():
    s = np.zeros(int(0.2 * SR))
    for k in range(3):
        add(s, snare(0.12, 900) * 0.6, k * 0.012)
    return s


def synth(f, dur):
    n = int(dur * SR)
    t = np.arange(n) / SR
    saw = 2 * ((f * t) % 1) - 1
    return lowpass(saw, 3) * env(n, 0.01, 0.15, 0.6, 0.12) * 0.5


# ---------------------------------------------------------------- scales and pieces
def note(base, semis):
    return base * 2 ** (semis / 12)


BHUPALI = [0, 2, 4, 7, 9, 12, 14, 16, 19, 21]


def piece_pahadi(seed, dur=100.0, base=277.18):
    r = np.random.default_rng(seed)
    buf = np.zeros(int(dur * SR))
    # tanpura: Pa Sa' Sa' Sa, every 4 s
    for k in range(int(dur / 1.0)):
        f = [note(base / 2, 7), note(base, 0), note(base, 0), note(base / 2, 0)][k % 4]
        add(buf, tanpura(f, 3.5) * 0.14, k * 1.0)
    # bansuri: phrases wandering up and down the scale, resting on Sa / Pa
    t, idx, prev = 2.0, 0, None
    while t < dur - 4:
        phrase = r.integers(4, 9)
        for i in range(phrase):
            idx = int(np.clip(idx + r.choice([-2, -1, -1, 1, 1, 2]), 0, len(BHUPALI) - 1))
            d = r.choice([0.35, 0.5, 0.5, 0.75, 1.0])
            f = note(base, BHUPALI[idx])
            add(buf, bansuri(f, d + 0.12, glide_from=prev if r.random() < 0.3 else None) * 0.22, t)
            prev, t = f, t + d
        land = note(base, r.choice([0, 7, 12]))
        add(buf, bansuri(land, 1.8, glide_from=prev) * 0.22, t)
        t += 1.8 + r.choice([0.5, 1.0, 1.5])
    # tabla: keherwa (8 beats) at ~84 bpm after the alaap
    beat = 60 / 84
    pattern = ["ge", "ti", "na", "ti", "na", "ti", "ge", "na"]
    start = 12.0
    k = 0
    while start + k * beat < dur - 2:
        add(buf, tabla(pattern[k % 8]) * 0.28, start + k * beat)
        k += 1
    return buf


def piece_lofi(seed, dur=100.0):
    r = np.random.default_rng(seed)
    buf = np.zeros(int(dur * SR))
    bpm = r.choice([72, 76, 80])
    beat = 60 / bpm
    root = 220.0 * 2 ** (r.integers(-3, 3) / 12)
    prog = [[0, 4, 7, 11], [9, 12, 16, 19], [5, 9, 12, 16], [7, 11, 14, 17]]   # Imaj7 vi7 IVmaj7 V7-ish
    bar = 4 * beat
    for b in range(int(dur / bar)):
        ch = prog[b % 4]
        add(buf, epiano([note(root, s) for s in ch], bar * 1.05) * 0.5, b * bar)
        add(buf, epiano([note(root / 2, ch[0])], bar) * 0.35, b * bar)   # the bass note
        for q in range(4):
            tt = b * bar + q * beat
            if q in (0, 2):
                add(buf, kick() * 0.5, tt + (0.02 if q == 2 else 0))
            if q in (1, 3):
                add(buf, snare() * 0.25, tt + 0.03)
            for e8 in (0, 0.5):
                add(buf, hat() * (0.25 if e8 == 0 else 0.15), tt + e8 * beat + 0.015)
    crackle = np.zeros(len(buf))
    pops = r.integers(0, len(buf), int(dur * 9))
    crackle[pops] = r.standard_normal(len(pops)) * 0.25
    buf += lowpass(crackle, 2) + lowpass(r.standard_normal(len(buf)), 40) * 0.01
    return lowpass(buf, 3)


def piece_beats(seed, dur=100.0):
    r = np.random.default_rng(seed)
    buf = np.zeros(int(dur * SR))
    bpm = r.choice([104, 110, 116])
    beat = 60 / bpm
    root = 293.66 * 2 ** (r.integers(-2, 3) / 12)
    scale = [0, 2, 4, 7, 9, 12, 14]
    hook = [scale[int(i)] for i in r.integers(0, len(scale), 8)]
    for k in range(int(dur / beat)):
        tt = k * beat
        # the dhol: dha on 1 and the "and" of 2, a ta on 3
        pos = k % 4
        if pos == 0:
            add(buf, kick(0.4, 140, 55) * 0.6, tt)
        if pos == 1:
            add(buf, kick(0.3, 160, 70) * 0.35, tt + beat * 0.5)
        if pos == 2:
            add(buf, snare(0.2, 300) * 0.35, tt)
            add(buf, clap() * 0.4, tt)
        if pos == 3:
            add(buf, kick(0.3, 150, 60) * 0.4, tt + beat * 0.5)
        add(buf, hat() * 0.2, tt + beat * 0.5)
        # the hook, every other bar pair
        if (k // 8) % 2 == 1:
            n = hook[k % 8]
            add(buf, synth(note(root, n), beat * 0.9) * 0.3, tt)
        if pos == 0:
            add(buf, synth(note(root / 2, scale[(k // 4) % 3 * 2]), beat * 3.5) * 0.25, tt)   # the bass
    return buf


def write(path, x):
    x = x / max(1e-6, np.max(np.abs(x))) * 0.85
    fade = int(2.0 * SR)
    x[:fade] *= np.linspace(0, 1, fade)
    x[-fade:] *= np.linspace(1, 0, fade)
    pcm = (x * 32767).astype(np.int16)
    tmp = path + ".wav"
    with wave.open(tmp, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    subprocess.run([ffmpeg(), "-y", "-loglevel", "error", "-i", tmp, "-c:a", "libvorbis", "-b:a", "96k", path], check=True)
    os.remove(tmp)


if __name__ == "__main__":
    stations = {"pahadi": ("Pahadi Dhun FM", piece_pahadi), "lofi": ("Naini Lo-fi", piece_lofi), "beats": ("Mall Road Beats", piece_beats)}
    for folder, (name, fn) in stations.items():
        d = os.path.join(OUT, folder)
        os.makedirs(d, exist_ok=True)
        for k in range(3):
            write(os.path.join(d, f"{folder}_{k + 1}.ogg"), fn(100 + k * 17 + len(folder)))
            print("made", folder, k + 1)
    with open(os.path.join(OUT, "stations.txt"), "w", encoding="utf-8") as f:
        for folder, (name, _) in stations.items():
            f.write(f"{folder} = {name}\n")
    with open(os.path.join(OUT, "CREDITS.txt"), "w", encoding="utf-8") as f:
        f.write("Radio stations (Pahadi Dhun FM, Naini Lo-fi, Mall Road Beats): original music synthesised for Naini Sukoon by\n"
                "Tools/radio/make_stations.py (22 Nov 2026). No third-party recordings or samples.\n")
    print("ok")

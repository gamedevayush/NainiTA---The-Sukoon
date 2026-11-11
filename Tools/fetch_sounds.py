"""Real recorded sounds for the game (user, 2026-11-11: "real ambience, footsteps, SFX, people, market").

python -I -u fetch_sounds.py <raw dir> <Unity Assets/Resources/Audio> [<extra mp3 dir>]

Sources: Mixkit sound effects (free licence, usable in games; not to be redistributed as sound files on their own).
Every id below was checked against Mixkit's own listing pages on 2026-11-11 (title and preview id read from the same
card). The catalog.json in the SamjhaaoSkills repo pairs ids with the wrong titles, so it is not used.

- loops: trimmed to at most 60 s, the end cross-faded into the start (seamless), loudness matched per group.
- one-shots: silence trimmed, peak-normalised, mono.
- footsteps: walking recordings cut into single steps (onset detection), six per surface, mono.
Writes Ogg Vorbis files Unity imports directly.
"""
import os, subprocess, sys, urllib.request
import numpy as np

RAW, OUT = sys.argv[1], sys.argv[2]
EXTRA = sys.argv[3] if len(sys.argv) > 3 else None
os.makedirs(RAW, exist_ok=True)
os.makedirs(OUT, exist_ok=True)
SR = 44100
UA = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"}

# name: (mixkit id, start s, max length s, target RMS dBFS, stereo)
LOOPS = {
    "amb_birds_day": (1210, 8, 60, -27, True),
    "amb_birds_morning": (2472, 5, 60, -27, True),
    "amb_forest": (1220, 5, 60, -30, True),
    "amb_wind_high": (1267, 0, 31, -27, True),
    "amb_wind": (2658, 0, 60, -29, True),
    "amb_night": (2475, 5, 60, -29, True),
    "amb_night_forest": (1224, 0, 47, -29, True),
    "amb_lake": (2458, 5, 55, -30, True),
    "amb_rain_light": (1253, 0, 39, -26, True),
    "amb_rain_heavy": (1262, 5, 60, -23, True),
    "amb_traffic": (2931, 0, 49, -28, True),
    "amb_street": (375, 0, 47, -26, True),
    "amb_market": (361, 10, 60, -25, True),
    "amb_fair": (368, 0, 15, -27, True),
    "amb_cafe": (444, 10, 60, -27, True),
    "amb_kids": (2264, 0, 60, -27, True),
    "amb_busstand": (2708, 0, 21, -26, True),
    "loop_swim": (1181, 0, 10, -22, False),
    "loop_jet": (1715, 1, 11, -20, False),
    "loop_engine": (1553, 2, 30, -22, False),
}
ONESHOTS = {
    "dog_1": 1, "dog_2": 54, "dog_3": 51, "dog_pack": 60, "rooster": 2462, "cow": 1744,
    "bird_1": 1211, "bird_2": 23, "bird_3": 2432,
    "thunder_1": 1298, "thunder_2": 1292, "thunder_3": 1296,
    "punch_1": 2155, "punch_2": 2151, "punch_3": 2153, "punch_4": 2143, "punch_5": 2165, "kick_1": 2163,
    "whoosh_1": 2142, "whoosh_2": 2048, "block_1": 2148, "grunt_1": 2174,
    "splash": 1311, "jump_water": 1180,
    "horn_1": 718, "horn_2": 719, "horn_3": 717, "horn_truck": 720, "horn_4": 1565,
    "car_pass": 1538, "bike_pass": 1606, "jet_start": 1725, "click": 1117,
}
STEPS = {"road": [1274], "stone": [1270], "grass": [1921], "forest": [1214], "gravel": [535]}
STEP_SINGLES = {"grass": [1920, 1922]}
LOCAL = {"temple_bell": "sfx__lib__temple_bell.mp3", "scooter_pass": "sfx__lib__scooter_pass.mp3"}


def fetch(i):
    path = os.path.join(RAW, f"{i}.mp3")
    if not os.path.exists(path) or os.path.getsize(path) < 1000:
        url = f"https://assets.mixkit.co/active_storage/sfx/{i}/{i}-preview.mp3"
        with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r, open(path, "wb") as f:
            f.write(r.read())
    return path


def decode(path, stereo=True):
    ch = 2 if stereo else 1
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", str(ch), "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, ch).copy()


def encode(x, name, q=4):
    path = os.path.join(OUT, name + ".ogg")
    ch = x.shape[1]
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ac", str(ch), "-ar", str(SR), "-i", "-",
                    "-c:a", "libvorbis", "-q:a", str(q), path], input=np.clip(x, -1, 1).astype(np.float32).tobytes(), check=True)
    return path


def rms_db(x):
    return 20 * np.log10(max(1e-9, float(np.sqrt(np.mean(x ** 2)))))


def loop(name, i, start, maxlen, target, stereo):
    x = decode(fetch(i), stereo)
    a = int(start * SR)
    x = x[a:a + int(maxlen * SR)]
    F = int(min(2.0, len(x) / SR * 0.15) * SR)
    t = np.linspace(0, 1, F, dtype=np.float32)[:, None]
    fin, fout = np.sin(t * np.pi / 2), np.cos(t * np.pi / 2)      # equal power
    y = x[: len(x) - F].copy()
    y[:F] = x[:F] * fin + x[len(x) - F:] * fout
    y *= 10 ** ((target - rms_db(y)) / 20)
    peak = np.abs(y).max()
    if peak > 0.97:
        y *= 0.97 / peak
    encode(y, name, 3)
    print(f"loop {name:18s} {len(y) / SR:5.1f}s rms {rms_db(y):6.1f}", flush=True)


def trim(x, thr_db=-45, pad=0.02):
    env = np.abs(x).max(axis=1)
    thr = env.max() * 10 ** (thr_db / 20)
    idx = np.flatnonzero(env > thr)
    if len(idx) == 0:
        return x
    a, b = max(0, idx[0] - int(pad * SR)), min(len(x), idx[-1] + int(0.08 * SR))
    y = x[a:b].copy()
    f = min(len(y), int(0.03 * SR))
    y[-f:] *= np.linspace(1, 0, f, dtype=np.float32)[:, None]
    return y


def oneshot(name, src, peak_db=-1.5):
    x = trim(decode(src, False))
    x *= 10 ** (peak_db / 20) / max(1e-6, np.abs(x).max())
    encode(x, name, 4)
    print(f"shot {name:18s} {len(x) / SR:5.2f}s", flush=True)


def steps(surface, ids, singles):
    cuts = []
    for i in ids:
        x = decode(fetch(i), False)[:, 0]
        win = int(0.01 * SR)
        env = np.convolve(np.abs(x), np.ones(win) / win, mode="same")
        thr = np.percentile(env, 90) * 0.6
        gap = int(0.28 * SR)
        k, peaks = 0, []
        while k < len(env):
            if env[k] > thr:
                seg = env[k:k + int(0.12 * SR)]
                p = k + int(np.argmax(seg))
                peaks.append(p)
                k = p + gap
            else:
                k += win
        for p in peaks:
            a, b = p - int(0.03 * SR), p + int(0.32 * SR)
            if a < 0 or b > len(x):
                continue
            c = x[a:b].copy()
            c[-int(0.12 * SR):] *= np.linspace(1, 0, int(0.12 * SR), dtype=np.float32)
            c[:int(0.005 * SR)] *= np.linspace(0, 1, int(0.005 * SR), dtype=np.float32)
            cuts.append(c)
    for i in singles:
        cuts.append(trim(decode(fetch(i), False))[:, 0])
    if not cuts:
        print("steps", surface, "none found")
        return
    energy = np.array([np.sqrt(np.mean(c ** 2)) for c in cuts])
    med = np.median(energy)
    order = np.argsort(np.abs(np.log(energy / med)))   # the most typical steps, not the odd loud or faint ones
    for k, j in enumerate(order[:6]):
        c = cuts[j] * (10 ** (-3 / 20) / max(1e-6, np.abs(cuts[j]).max()))
        encode(c[:, None], f"step_{surface}_{k}", 4)
    print(f"steps {surface:10s} {len(cuts)} cut, kept {min(6, len(cuts))}", flush=True)


for n, (i, s, m, t, st) in LOOPS.items():
    loop(n, i, s, m, t, st)
for n, i in ONESHOTS.items():
    oneshot(n, fetch(i))
for n, f in LOCAL.items():
    if EXTRA and os.path.exists(os.path.join(EXTRA, f)):
        oneshot(n, os.path.join(EXTRA, f))
for s, ids in STEPS.items():
    steps(s, ids, STEP_SINGLES.get(s, []))
print("done ->", OUT)

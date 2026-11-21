"""Vehicle sounds: one engine per kind of vehicle, skids, brakes, knocks by how hard, a set of horns (owner, 22 Nov 2026:
"sound effect sahi kro...har vehicle ka accleartion, braking..hit wgera tgda sound..like real..horn random kyun bjta har
bar..har gaadi ka assign krdo").

python -I -u fetch_vehicle_sounds.py <raw dir> <Unity Assets/Resources/Audio/veh>

Sources (both free for games, checked 22 Nov 2026):
- Freesound, only sounds under Creative Commons 0 (public domain: no credit needed; credited below anyway). The search
  was filtered to CC0 (freesound.org/search/?f=license:"Creative Commons 0"); the HQ preview of each is used.
- Mixkit sound effects (free licence, usable in games; ids read from the same card as the title on mixkit.co).

Engines are idle / steady-running recordings cut to a seamless loop; VehicleSound pitches them with simulated gears.
Every file is mono Ogg Vorbis (small on a phone).
"""
import os, subprocess, sys, urllib.request, time
import numpy as np

RAW, OUT = sys.argv[1], sys.argv[2]
os.makedirs(RAW, exist_ok=True)
os.makedirs(OUT, exist_ok=True)
SR = 44100
UA = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"}

# freesound id: (preview path, author, title) - all CC0
FS = {
    522225: ("522/522225_9250976", "Nox_Sound", "Vehicle_Car_Peugeot_308_Idle_Exterior_Loop_Mono_02"),
    369053: ("369/369053_1535801", "AndrewAlexander", "car_idle_loop"),
    128160: ("128/128160_1372389", "ibirdfilm", "Diesel Truck Idling Front"),
    540398: ("540/540398_11267296", "bikesnbassboi", "Bus Engine Idling"),
    54909: ("54/54909_71257", "qubodup", "Bus Motor/Engine Sound Loop"),
    361407: ("361/361407_1481531", "richwise", "Single Cylinder Motorcycle"),
    159326: ("159/159326_2870564", "ashimda", "bull1"),
    158891: ("158/158891_2559395", "druki", "Vespa Scooter PX 200 GS old exhaust"),
    697379: ("697/697379_9616576", "IENBA", "Electric Scooter"),
    353664: ("353/353664_4762007", "Kriaa", "Indian Auto rickshaw, start leave and approach"),
    71739: ("71/71739_995351", "audible-edge", "Chrysler LHS tire squeal 04"),
    104026: ("104/104026_179538", "RutgerMuller", "Tires Squeaking"),
    456764: ("456/456764_9514571", "WavJunction.com", "Car Brakes squeak screech squeal stop"),
    801435: ("801/801435_17244242", "okpato123", "air brake sound effect"),
    151624: ("151/151624_71257", "qubodup", "Clank Car Crash Collision"),
    332056: ("332/332056_71257", "qubodup", "Fast Collision"),
    807438: ("807/807438_3797507", "Logicogonist", "car crash short 1"),
    329516: ("329/329516_424694", "LPA134", "Hood Impact"),
    237375: ("237/237375_1502374", "squareal", "Car Crash"),
    592388: ("592/592388_11537497", "magnuswaker", "Car Crash (with Glass)"),
    78483: ("78/78483_1226546", "1sticky8", "Bike_Wreck"),
    182474: ("182/182474_3153523", "keweldog", "car horn"),
    434878: ("434/434878_3388578", "MicktheMicGuy", "Car Honking"),
    170243: ("170/170243_3147538", "BeatsbyCasper", "Car Horn"),
    423990: ("423/423990_4023430", "AmishRob", "Car horn beep beep two beeps honk honk"),
    451697: ("451/451697_612689", "kyles", "school bus truck int horn honk"),
    635681: ("635/635681_5685306", "LaurenPonder", "BusHorn_LAURENPOND"),
    546528: ("546/546528_5637404", "trezz77", "powerfull horn"),
}
# mixkit id: title
MK = {718: "Car horn", 719: "Car double horn", 1565: "Classic car horn", 724: "Car horn", 722: "Small car horn",
      2886: "Classic car horn", 717: "Small car horn", 720: "Truck horn", 2980: "Factory metal hard hit",
      1551: "Car window breaking"}


def fetch(src):
    kind, i = src
    path = os.path.join(RAW, f"{kind}{i}.mp3")
    if not os.path.exists(path) or os.path.getsize(path) < 1000:
        url = (f"https://cdn.freesound.org/previews/{FS[i][0]}-hq.mp3" if kind == "fs"
               else f"https://assets.mixkit.co/active_storage/sfx/{i}/{i}-preview.mp3")
        with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r, open(path, "wb") as f:
            f.write(r.read())
        time.sleep(0.3)
    return path


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).copy()


def encode(x, name, q=4):
    path = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ac", "1", "-ar", str(SR), "-i", "-",
                    "-c:a", "libvorbis", "-q:a", str(q), path], input=np.clip(x, -1, 1).astype(np.float32).tobytes(), check=True)


def rms_db(x):
    return 20 * np.log10(max(1e-9, float(np.sqrt(np.mean(x ** 2)))))


def highpass(x, hz=40.0):
    # one-pole high-pass: no rumble / DC under the engines
    a = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    prev_x = prev_y = 0.0
    for k in range(len(x)):
        prev_y = a * (prev_y + x[k] - prev_x)
        prev_x = x[k]
        y[k] = prev_y
    return y


def loop(name, src, start, length, target=-20.0, xfade=0.25):
    x = decode(fetch(src))
    a = int(start * SR)
    seg = x[a:a + int((length + xfade) * SR)].copy()
    F = int(xfade * SR)
    t = np.linspace(0, 1, F, dtype=np.float32)
    head = seg[:F] * np.sin(t * np.pi / 2) + seg[len(seg) - F:] * np.cos(t * np.pi / 2)   # equal power
    y = np.concatenate([head, seg[F:len(seg) - F]])
    y = highpass(y)
    y *= 10 ** ((target - rms_db(y)) / 20)
    peak = np.abs(y).max()
    if peak > 0.97:
        y *= 0.97 / peak
    encode(y, name, 4)
    print(f"loop {name:16s} {len(y) / SR:4.1f}s rms {rms_db(y):6.1f}  <- {src}", flush=True)


def shot(name, src, start=0.0, maxlen=None, loudest=None, peak_db=-1.0, fade=0.06, gate_db=-40, rms=None):
    x = decode(fetch(src))
    x = x[int(start * SR):]
    if loudest:   # the loudest `loudest` seconds (one honk out of several, the squeal of a stop)
        L = int(loudest * SR)
        if len(x) > L:
            e = np.convolve(x ** 2, np.ones(2048) / 2048, mode="same")
            c = np.cumsum(e)
            s = int(np.argmax(c[L:] - c[:-L]))
            x = x[s:s + L]
    env = np.abs(x)
    thr = env.max() * 10 ** (gate_db / 20)
    idx = np.flatnonzero(env > thr)
    if len(idx):
        x = x[max(0, idx[0] - int(0.01 * SR)):idx[-1] + int(0.05 * SR)]
    if maxlen and len(x) > maxlen * SR:
        x = x[:int(maxlen * SR)]
    f = min(len(x), int(fade * SR))
    x[len(x) - f:] *= np.linspace(1, 0, f, dtype=np.float32)
    x[:64] *= np.linspace(0, 1, 64, dtype=np.float32)
    x *= 10 ** (peak_db / 20) / max(1e-6, np.abs(x).max())
    if rms is not None:   # the same loudness across a set (horns), the peak kept under -1 dBFS
        x *= 10 ** ((rms - rms_db(x)) / 20)
        x *= min(1.0, 0.89 / max(1e-6, np.abs(x).max()))
    encode(x, name, 4)
    print(f"shot {name:16s} {len(x) / SR:4.1f}s rms {rms_db(x):6.1f}  <- {src}", flush=True)


fs = lambda i: ("fs", i)
mk = lambda i: ("mk", i)

# engines (idle / steady): car (two), diesel (SUV, pickup, van), bus, truck, commuter bike, big single (350), scooty,
# electric scooty, auto rickshaw
loop("eng_car_a", fs(522225), 6.0, 4.0)
loop("eng_car_b", fs(369053), 2.0, 4.0)
loop("eng_diesel", fs(128160), 13.0, 4.0)
loop("eng_bus", fs(540398), 2.0, 5.0, -19.0)
loop("eng_truck", fs(54909), 1.0, 6.0, -19.0)
loop("eng_bike", fs(361407), 34.0, 3.0)
loop("eng_bike_big", fs(159326), 5.0, 4.0)
loop("eng_scooty", fs(158891), 15.0, 4.0)
loop("eng_scooty_e", fs(697379), 3.0, 3.0, -24.0)
loop("eng_auto", fs(353664), 87.0, 4.0)
loop("skid", fs(71739), 5.0, 3.0, -18.0)

# brakes
shot("brake_air", fs(801435))
shot("brake_squeal", fs(456764), loudest=1.2, peak_db=-4.0)
shot("skid_short", fs(104026), maxlen=1.6, peak_db=-3.0)

# knocks: light / medium / heavy, glass, a two-wheeler going down
shot("hit_light_1", fs(151624))
shot("hit_light_2", fs(332056))
shot("hit_light_3", fs(807438))
shot("hit_med_1", mk(2980))
shot("hit_med_2", fs(329516), loudest=1.0)
shot("hit_heavy_1", fs(237375), maxlen=3.0)
shot("hit_heavy_2", fs(592388))
shot("glass_1", mk(1551))
shot("bike_fall", fs(78483), maxlen=2.8)

# horns: cars (each car keeps one), buses / trucks, small ones (pitched up for bikes, scooties, autos)
for k, src in enumerate([mk(718), mk(719), mk(1565), mk(724), mk(2886), fs(182474), fs(434878), fs(423990)]):
    shot(f"horn_car_{k}", src, loudest=1.1 if src == fs(182474) else None, maxlen=1.3, peak_db=-2.0, rms=-14.0)
for k, src in enumerate([mk(720), fs(451697), fs(635681), fs(546528)]):
    shot(f"horn_bus_{k}", src, loudest=1.4, peak_db=-1.5, rms=-12.5)
for k, src in enumerate([mk(717), mk(722)]):
    shot(f"horn_small_{k}", src, maxlen=1.0, peak_db=-2.0, rms=-14.0)
print("done")

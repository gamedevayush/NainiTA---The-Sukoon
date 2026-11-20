"""All '[Nainital] offsets' blocks since the last 'MARK' line in the Editor.log: per clip kind and skin, the range of
hips-over-butt, ankle-over-sole, head-top-over-hips."""
import os, re, collections
L = os.path.join(os.environ["LOCALAPPDATA"], "Unity", "Editor", "Editor.log")
lines = open(L, encoding="utf-8", errors="replace").read().splitlines()
start = max((i for i, l in enumerate(lines) if "offsets-mark" in l), default=0)
d = collections.defaultdict(list)
for l in lines[start:]:
    m = re.match(r"\s+seat \d+ (\S+): .*hips over butt ([\d.\-]+); .*ankle over sole ([\d.\-]+); head top over hips ([\d.\-]+); skin (\S+)", l)
    if not m: continue
    clip = m.group(1); kind = "ride" if "Riding" in clip else "pillion" if "Passenger" in clip else "drive" if clip == "driving" else "sit"
    skin = m.group(5)[:16]
    d[(kind, skin)].append((float(m.group(2)), float(m.group(3)), float(m.group(4))))
print(f"{'kind':8s} {'skin':16s} n  butt(min-max)   sole(min-max)   head(min-max)")
for (k, s), v in sorted(d.items()):
    b = [x[0] for x in v]; so = [x[1] for x in v]; h = [x[2] for x in v]
    print(f"{k:8s} {s:16s} {len(v):2d} {min(b):.3f}-{max(b):.3f}   {min(so):.3f}-{max(so):.3f}   {min(h):.3f}-{max(h):.3f}")
for k in ("sit", "drive", "ride", "pillion"):
    vs = [x for (kk, s), v in d.items() if kk == k for x in v]
    if vs:
        print(f"ALL {k}: butt {min(x[0] for x in vs):.3f}-{max(x[0] for x in vs):.3f}  sole {min(x[1] for x in vs):.3f}-{max(x[1] for x in vs):.3f}  head {min(x[2] for x in vs):.3f}-{max(x[2] for x in vs):.3f}")

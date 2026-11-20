"""One line of a ProfileZ block: python profile_line.py model x zmin zmax lo hi -> every row, surfaces in [lo, hi]."""
import os, re, sys
model, x, z0, z1, lo, hi = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5]), float(sys.argv[6])
L = os.path.join(os.environ["LOCALAPPDATA"], "Unity", "Editor", "Editor.log")
cur = None; xl = None; best = {}
for ln in open(L, encoding="utf-8", errors="replace").read().splitlines():
    m = re.match(r"\[Nainital\] profileZ (\w+):", ln)
    if m: cur = m.group(1); continue
    if cur != model: continue
    if not ln.startswith("  "): cur = None; continue
    m = re.match(r"\s+x (-?[\d.]+):", ln)
    if m: xl = float(m.group(1)); best.setdefault(xl, []); best[xl] = []; continue
    m = re.match(r"\s+z (-?[\d.]+): ?(.*)", ln)
    if m: best[xl].append((float(m.group(1)), m.group(2)))
k = min(best, key=lambda q: abs(q - x))
print(f"{model} line x {k}")
for z, s in best[k]:
    if z0 <= z <= z1:
        toks = [t for t in s.split() if re.match(r"-?[\d.]+", t) and lo <= float(re.match(r"-?[\d.]+", t).group(0)) <= hi]
        print(f"  z {z:+.3f}: {' '.join(toks)}")

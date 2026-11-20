"""Read the last ProfileZ blocks from the Editor.log and print, per vehicle and x line, the surfaces between LO and HI
(roof and underbody dropped), runs of equal z rows merged: 'z a..b: surfaces'."""
import os, re, sys
LO = float(sys.argv[1]) if len(sys.argv) > 1 else 0.0
HI = float(sys.argv[2]) if len(sys.argv) > 2 else 9.0
only = sys.argv[3].split(",") if len(sys.argv) > 3 else None
L = os.path.join(os.environ["LOCALAPPDATA"], "Unity", "Editor", "Editor.log")
lines = open(L, encoding="utf-8", errors="replace").read().splitlines()
blocks = {}
cur = None
for ln in lines:
    m = re.match(r"\[Nainital\] profileZ (\w+):", ln)
    if m:
        cur = m.group(1); blocks[cur] = []; continue
    if cur and ln.startswith("  "):
        blocks[cur].append(ln)
    else:
        cur = None
for name, bl in blocks.items():
    if only and name not in only: continue
    print(f"== {name}")
    xline = None; prev = None; start = None; last = None
    def flush():
        if prev is not None: print(f"    z {start:+.2f}..{last:+.2f}: {prev}")
    for ln in bl:
        m = re.match(r"\s+x (-?[\d.]+):", ln)
        if m:
            flush(); prev = None
            print(f"  x {m.group(1)}"); continue
        m = re.match(r"\s+z (-?[\d.]+): ?(.*)", ln)
        if not m: continue
        z = float(m.group(1)); s = []
        for tok in m.group(2).split():
            y = float(re.match(r"-?[\d.]+", tok).group(0))
            if LO <= y <= HI: s.append(tok)
        key = " ".join(s)
        if key != prev:
            flush(); prev = key; start = z
        last = z
    flush()

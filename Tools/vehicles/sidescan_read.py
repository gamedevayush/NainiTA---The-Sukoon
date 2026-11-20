"""The last sidescan blocks in the Editor.log: per vehicle and height, the half-width (|x| of the left and right hits)
along z, as compact rows, and the local bumps (a peg / a grip end sticks out past its neighbours)."""
import os, re, sys
L = os.path.join(os.environ["LOCALAPPDATA"], "Unity", "Editor", "Editor.log")
lines = open(L, encoding="utf-8", errors="replace").read().splitlines()
blocks = {}; cur = None; y = None
for ln in lines:
    m = re.match(r"\[Nainital\] sidescan (\w+):", ln)
    if m: cur = m.group(1); blocks[cur] = {}; continue
    if not cur: continue
    if not ln.startswith("  "): cur = None; continue
    m = re.match(r"\s+y (-?[\d.]+):", ln)
    if m: y = float(m.group(1)); blocks[cur][y] = []; continue
    m = re.match(r"\s+z (-?[\d.]+): L (\S+) R (\S+)", ln)
    if m:
        def val(s):
            mm = re.match(r"(-?[\d.]+)(?:\((.*)\))?", s)
            return (abs(float(mm.group(1))), mm.group(2) or "") if mm else (None, "")
        blocks[cur][y].append((float(m.group(1)), val(m.group(2)), val(m.group(3))))
only = sys.argv[1].split(",") if len(sys.argv) > 1 else None
for name, ys in blocks.items():
    if only and name not in only: continue
    print(f"== {name}")
    for y, rows in ys.items():
        s = []
        for (z, (lx, lp), (rx, rp)) in rows[::2]:
            tag = lambda x, p: "  -  " if x is None else f"{x:.2f}{'h' if p.startswith('Handle') else 'w' if p.startswith('Wheel') else ''}"
            s.append(f"{z:+.2f}:{tag(lx, lp)}/{tag(rx, rp)}")
        print(f"  y {y:.2f}: " + " ".join(s))

"""The '[Nainital] vsetup sheet' lines since the last 'sheets-mark' in the Editor.log: per vehicle and seat, the worst
foot / hand miss, the butt-gap range (seat of the trousers over the surface) and the smallest head room over all runs.
Flags: foot > 4 cm, hand > 4 cm, |butt gap| > 4 cm (sinks / floats), head room < 0."""
import os, re, collections
L = os.path.join(os.environ["LOCALAPPDATA"], "Unity", "Editor", "Editor.log")
lines = open(L, encoding="utf-8", errors="replace").read().splitlines()
start = max((i for i, l in enumerate(lines) if "sheets-mark" in l or "Reloading assemblies for play mode" in l), default=0)
d = collections.defaultdict(lambda: collections.defaultdict(lambda: collections.defaultdict(list)))
runs = collections.Counter()
for l in lines[start:]:
    m = re.search(r"\[Nainital\] vsetup sheet .*?\((\w+), box [^)]*\) (.*)", l)
    if not m: continue
    name, info = m.groups(); runs[name] += 1
    for seat in info.split("; "):
        s = re.match(r"seat (\d+): clip (\S+)", seat)
        if not s: continue
        k = int(s.group(1)); r = d[name][k]
        r["clip"].append(s.group(2))
        for key, pat in (("foot", r"footL-target (\d+)cm"), ("hand", r"handL-target (\d+)cm"), ("butt", r"butt-gap (-?\d+)cm"),
                         ("head", r"head-room (-?\d+)cm"), ("bend", r"bend (-?\d+)deg")):
            mm = re.search(pat, seat)
            if mm: r[key].append(int(mm.group(1)))
bad = 0
for name in sorted(d):
    print(f"== {name} ({runs[name]} runs)")
    for k in sorted(d[name]):
        r = d[name][k]; flags = []
        f = max(r["foot"]) if r["foot"] else None
        h = max(r["hand"]) if r["hand"] else None
        b = (min(r["butt"]), max(r["butt"])) if r["butt"] else None
        hd = min(r["head"]) if r["head"] else None
        if f is not None and f > 4: flags.append("FOOT")
        if h is not None and h > 4: flags.append("HAND")
        if b is not None and (b[0] < -4 or b[1] > 4): flags.append("BUTT")
        if hd is not None and hd < 0: flags.append("HEAD")
        bad += bool(flags)
        print(f"  s{k:<2d} {r['clip'][0][:22]:22s} foot {f if f is not None else '-':>3}cm hand {h if h is not None else '-':>3}cm"
              f" butt {('%d..%d' % b) if b else '-':>7}cm head {hd if hd is not None else '-':>4}cm bend {max(r['bend']) if r['bend'] else '-':>3}"
              + ("   <- " + " ".join(flags) if flags else ""))
print(f"\nseats flagged: {bad}")


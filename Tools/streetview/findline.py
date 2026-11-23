"""Street View layer screenshot -> the blue coverage-line pixels nearest the centre, as 'x,y' lines (nearest first,
each at least 30 px from the ones before)."""
import sys
import numpy as np
from PIL import Image

a = np.asarray(Image.open(sys.argv[1]).convert('RGB')).astype(int)
r, g, b = a[..., 0], a[..., 1], a[..., 2]
m = (r < 80) & (g > 115) & (g < 180) & (b > 125) & (b < 200) & (b >= g - 5)
H, W = m.shape
m[:, :80] = False          # side bar
m[:75, :] = False          # search bar + chips
m[70:185, 80:490] = False  # place card
m[610:, 480:880] = False   # Images legend
m[560:, 1100:] = False     # controls
m[610:, 80:180] = False    # layers button
ys, xs = np.nonzero(m)
if len(xs) == 0:
    sys.exit(0)
# keep pixels that are part of a line: enough matching neighbours in a 9x9 window
keep = []
for x, y in zip(xs, ys):
    win = m[max(0, y - 4):y + 5, max(0, x - 4):x + 5]
    if win.sum() >= 12:
        keep.append((x, y))
cx, cy = W / 2, H / 2
keep.sort(key=lambda p: (p[0] - cx) ** 2 + (p[1] - cy) ** 2)
out = []
for p in keep:
    if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 > 900 for q in out):
        out.append(p)
    if len(out) >= 4:
        break
for p in out:
    print(f'{p[0]},{p[1]},{((p[0] - cx) ** 2 + (p[1] - cy) ** 2) ** 0.5:.0f}')

"""Route DSA ground (The Flats) -> Khurpatal over the drivable roads of world.json; sample it every STEP metres,
count generated houses / trees / OSM buildings each side, write route.json (+ lat/lng per sample)."""
import json, math, heapq, sys
import numpy as np

W = r'D:\NainiTA---The-Sukoon\Assets\Nainital\Data\world.json'
OUT = sys.argv[1]
STEP = float(sys.argv[2]) if len(sys.argv) > 2 else 50.0
w = json.load(open(W, encoding='utf-8'))
CLAT, CLON = w['meta']['centerLat'], w['meta']['centerLon']
M_LAT = 110_860.0
M_LON = 111_320.0 * math.cos(math.radians(CLAT))

SKIP = {'path', 'footway', 'bridleway', 'steps', 'track', 'pedestrian', 'cycleway'}
nodes, key2id, adj = [], {}, {}


def nid(x, z):
    k = (round(x), round(z))
    for dx in (-1, 0, 1):
        for dz in (-1, 0, 1):
            kk = (k[0] + dx, k[1] + dz)
            if kk in key2id:
                i = key2id[kk]
                if math.hypot(nodes[i][0] - x, nodes[i][1] - z) < 1.6:
                    return i
    key2id[k] = len(nodes)
    nodes.append((x, z))
    return key2id[k]


for ri, r in enumerate(w['roads']):
    if r['kind'] in SKIP:
        continue
    p = r['pts']
    ids = [nid(p[i], p[i + 1]) for i in range(0, len(p), 2)]
    for a, b in zip(ids, ids[1:]):
        if a == b:
            continue
        d = math.hypot(nodes[a][0] - nodes[b][0], nodes[a][1] - nodes[b][1])
        adj.setdefault(a, []).append((b, d, ri))
        adj.setdefault(b, []).append((a, d, ri))


def nearest(x, z):
    return min(adj, key=lambda i: (nodes[i][0] - x) ** 2 + (nodes[i][1] - z) ** 2)


START = (-57.0, 156.0)       # Mallital, the north end of the Flats (DSA ground), where the road to Kaladhungi road leaves
END = (-2618.0, -2168.0)     # Khurpatal: the end of its road by the lake (TrafficBusTest)
KR = next(i for i, r in enumerate(w['roads']) if r['name'] == 'Kaldhungi-Nainital Road')
kr_nodes = {v for u in adj for v, l, ri in adj[u] if ri == KR}


def nearest_on(x, z, pool):
    return min(pool, key=lambda i: (nodes[i][0] - x) ** 2 + (nodes[i][1] - z) ** 2)


poi = {q['name']: (q['x'], q['z']) for q in w['pois']}
via = [nearest(*START)] + [nearest_on(*poi[n], kr_nodes) for n in
                           ('Eco Cave Garden', 'Barapathar', 'Lovers Point', 'Khurpatal Lake View Point', 'Sariyatal',
                            'Sariyatal Falls', 'Khurpatal View Point')] + [nearest(*END)]


def dijkstra(s, e):
    dist, prev = {s: 0.0}, {}
    pq = [(0.0, s)]
    while pq:
        d, u = heapq.heappop(pq)
        if u == e:
            break
        if d > dist[u]:
            continue
        for v, l, ri in adj.get(u, []):
            nd = d + l + (0 if ri == KR else l * 0.5)   # keep to the main road
            if nd < dist.get(v, 1e18):
                dist[v], prev[v] = nd, (u, ri)
                heapq.heappush(pq, (nd, v))
    path, used, u = [e], [], e
    while u != s:
        u, ri = prev[u]
        path.append(u)
        used.append(ri)
    return path[::-1], used[::-1]


path, roads_used = [via[0]], []
for a, b in zip(via, via[1:]):
    pth, used = dijkstra(a, b)
    path += pth[1:]
    roads_used += used
pts = np.array([nodes[i] for i in path])
dist = {path[-1]: float(np.hypot(*(pts[1:] - pts[:-1]).T).sum())}
e = path[-1]
print(f'route {dist[e]:.0f} m, {len(path)} nodes')
seen = []
for ri in roads_used:
    n = w['roads'][ri]['name'] or w['roads'][ri]['kind']
    if not seen or seen[-1] != n:
        seen.append(n)
print('roads:', ' -> '.join(seen))

# resample
seg = np.hypot(*(pts[1:] - pts[:-1]).T)
cum = np.concatenate([[0], np.cumsum(seg)])
H = np.array(w['houses'], np.float64).reshape(-1, 9)
T = np.array(w['trees'], np.float64).reshape(-1, 4)
B = [(np.mean(b['pts'][0::2]), np.mean(b['pts'][1::2])) for b in w['osmBuildings']]
B = np.array(B) if B else np.zeros((0, 2))
pois = w['pois']
samples = []
for m in np.arange(0, cum[-1], STEP):
    k = min(np.searchsorted(cum, m, side='right') - 1, len(seg) - 1)
    t = (m - cum[k]) / max(seg[k], 1e-6)
    p = pts[k] + t * (pts[k + 1] - pts[k])
    fwd = (pts[k + 1] - pts[k]) / max(seg[k], 1e-6)
    right = np.array([fwd[1], -fwd[0]])   # x right when z is north and fwd points north
    yaw = math.degrees(math.atan2(fwd[0], fwd[1])) % 360
    row = {'m': int(m), 'x': round(float(p[0]), 1), 'z': round(float(p[1]), 1), 'yaw': round(yaw, 1),
           'lat': round(CLAT + p[1] / M_LAT, 6), 'lng': round(CLON + p[0] / M_LON, 6)}
    for name, arr, near, far in (('h', H[:, :2], 4, 45), ('t', T[:, :2], 3, 30), ('b', B, 4, 45)):
        if len(arr) == 0:
            continue
        rel = arr - p
        along = rel @ fwd
        side = rel @ right
        mask = (np.abs(along) < STEP / 2)
        for sname, sm in (('L', (side < -near) & (side > -far)), ('R', (side > near) & (side < far))):
            row[name + sname] = int((mask & sm).sum())
    poi = min(pois, key=lambda q: (q['x'] - p[0]) ** 2 + (q['z'] - p[1]) ** 2)
    pd = math.hypot(poi['x'] - p[0], poi['z'] - p[1])
    row['near'] = f"{poi['name']} {pd:.0f}m" if pd < 200 else ''
    samples.append(row)
json.dump({'length': dist[e], 'roads': seen, 'samples': samples,
           'line': [[round(float(a), 1), round(float(b), 1)] for a, b in pts]}, open(OUT, 'w'), indent=0)
print(f"{'m':>5} {'x':>7} {'z':>7} {'yaw':>5}  hL hR  tL  tR  bL bR  near")
for r in samples:
    print(f"{r['m']:5d} {r['x']:7.0f} {r['z']:7.0f} {r['yaw']:5.0f}  {r.get('hL',0):2d} {r.get('hR',0):2d} {r.get('tL',0):3d} {r.get('tR',0):3d}  {r.get('bL',0):2d} {r.get('bR',0):2d}  {r['near']}")

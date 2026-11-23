"""The Kaladhungi road, DSA ground (the Flats) -> Khurpatal, as Google Street View shows it (owner, 23 Nov 2026: "ek ek
photo ko dekhke socho ki kya kam ya jyada krna hai... sbko ditto bnaado... building kam jyada ya texture wgera sab").

117 Street View panoramas (Aug 2022), one every 100 m along the road, were set beside game shots from the same spots
(report: Shots/kd_compare/index.html, made by the tools in this folder). This file holds what each photo says and
writes Assets/Nainital/Data/streetview_kaladhungi.json, which WorldBuilder.StreetViewRoad.cs builds on the final ground.

SURVEY: per point (index = metres from the Flats / 100) the LEFT and RIGHT side, as seen driving towards Khurpatal:
  town        built-up bazaar: nothing added (the houses come from the map)
  b1 / b2     bushes by the road: some / thick (on a hill cut they cover the cut, on the drop a narrower band)
  t1 / t2     trees by the road (2.5-14 m from the edge): a few / many;  o = banj oak, d = deodar, p = chir pine
  rail        a crash barrier (W-beam) along the drop;  blocks = concrete parapet blocks with gaps
  none        no barrier and no whitewashed parapet (the game had one where the photo shows a bank or a house)
  open        open ground: the tall trees within 70 m go (Khurpatal's meadow and scrub)
  ""          a built place (Lover's Point, the Khurpatal Lake View hairpin): left as built
ITEMS: buildings, sheds, stalls, walls read off the photos. along = metres along the road from the point (+ towards
Khurpatal), back = metres from the road edge to the front, w x d footprint, lv storeys, wall / roof = Atlas palette
(wall 0 white 1 cream 2 yellow 3 pale pink 4 sky blue 5 green 6 peach 7 grey stone 8 lavender 9 brick red 10 mustard
11 teal; roof 0 red 1 maroon 2 green 3 teal 4 blue 5 rust 6 grey).
RESTYLE: map buildings within r of (x, z) get this wall / roof colour.

usage: python -I Tools/streetview/kaladhungi_survey.py
"""
import json, math, os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))

T = "town"
SURVEY = {
    **{i: (T, T) for i in range(0, 11)},
    11: ("b2 t2 o", T),
    12: (T, T),
    13: ("b2 t1 o", "t1 o"),
    14: ("b2 t1 o", "t1 o"),
    15: ("b1 t2 o", "b2"),
    16: ("t2 d", "b2 t1 o"),
    17: ("b1 t2 d", "b2"),
    18: ("b1 t2 o rail", "b2 t1 o"),
    19: ("t1 o rail", "b1"),
    20: ("b1 t2 d rail", "b1 none"),
    21: ("b1 t1 o", "t1 o"),
    22: ("b2 none", "b1 t2 o"),
    23: ("b1", "b2 t2 o rail"),
    24: ("b2 t1 o", "b1 t2 o rail"),
    25: ("b2", "b1 t2 o rail"),
    26: ("b2 t2 o", "b1 t2 o rail"),
    27: ("b2 t1 o", "b1"),
    28: ("b2 t2 o", "b1 t2 o rail"),
    29: ("b2", "b2 rail"),
    30: ("", ""),
    31: ("b2 t1 o", "b2 rail"),
    32: ("b2", "b1"),
    33: ("b2 t1 o", "t2 o rail"),
    34: ("b2", "t2 o rail"),
    35: ("b2 blocks", "t2 o blocks"),
    36: ("b2", "b2 t1 o blocks"),
    37: ("b2 t2 o", "b1 t2 o"),
    38: ("b2 t1 o", "t2 o blocks"),
    39: ("b2 t2 o", "t2 o"),
    40: ("b2 t2 o", "t2 o blocks"),
    41: ("b2", "b1 t1 o"),
    42: ("b2", "b1 rail"),
    43: ("", "b1"),
    44: ("b1 t1 d rail", "b2"),
    45: ("b1 t2 d blocks", "b2"),
    46: ("b1 t1 d blocks", "b2"),
    47: ("b1 t2 o blocks", "b2"),
    48: ("b2 t1 o rail", "b2"),
    49: ("b2", "b2"),
    50: ("b2", "t2 o blocks"),
    51: ("b2", "t2 d blocks"),
    52: ("b2", "t2 o blocks"),
    53: ("b2", "t2 d rail"),
    54: ("b1", "t1 d blocks"),
    55: ("b2", "b1 rail"),
    56: ("b1", ""),
    57: ("b1 rail", "b2"),
    58: ("t2 d", "b2"),
    59: ("b1 rail", "b2"),
    60: ("t1 d rail", "b2"),
    61: ("t2 o rail", "b2"),
    62: ("t2 o rail", "b1"),
    63: ("b1", "b1"),
    64: ("b1", "b1"),
    65: ("", "b1"),
    66: ("b1 rail", "b2"),
    67: ("b1 blocks", "b2"),
    68: ("b2 blocks", "b2"),
    69: ("b1 rail", "b1"),
    70: ("b1 rail", "b2"),
    71: ("b1 blocks", "b2"),
    72: ("b1 blocks", "b2"),
    73: ("b1", "b2"),
    74: ("b1 blocks", "b2"),
    75: ("b1", "b1 blocks"),
    76: ("", "b1"),
    77: ("b2", "b1 rail"),
    78: ("b1", "b1"),
    79: ("b2", "b1 rail"),
    80: ("b2", "b2"),
    81: ("t2 o", "b2"),
    82: ("b1", "b2"),
    83: ("", "b2"),
    84: ("", "b1"),
    85: ("t2 o rail", "b2"),
    86: ("t2 o rail", "b2"),
    87: ("t1 p rail", "b2"),
    88: ("b2", "b1 rail"),
    89: ("b2", ""),
    90: ("b2 rail", ""),
    91: ("b2", "t2 p rail"),
    92: ("t2 p rail", "b2 t1 p"),
    93: ("t2 p", "b2"),
    94: ("t2 p rail", "b2"),
    95: ("t2 p rail", "b2"),
    96: ("t1 p rail", "b2"),
    97: ("t1 p rail", ""),
    98: ("t2 p rail", "b2"),
    99: ("t1 p rail", "b2"),
    100: ("b1 t1 p rail", "b2"),
    101: ("t2 p rail", "b2"),
    102: ("t2 p rail", "b2"),
    103: ("t2 p rail", "b2"),
    104: ("t2 p rail", "b2"),
    105: ("t2 p", "b2"),
    106: ("t2 p rail", "b2"),
    107: ("t2 o", "b1"),
    108: ("b2 t2 p", "b2"),
    109: ("b1 t2 p", "b2"),
    110: ("b1", "b2 t1 p"),
    111: ("b2 t1 o", "b1 t1 o"),
    112: ("b2 open", "b1 open"),
    113: ("b2 open", "open"),
    114: ("b2 open", "b2 open"),
    115: ("b2 open", "b2 open"),
    116: ("b2 open", "b2 open"),
}


def H(i, side, along, back, w, d, lv, wall, roof, flat=True, shop=False, note=""):
    return dict(i=i, side=side, kind="house", along=along, back=back, w=w, d=d, lv=lv, wall=wall, roof=roof,
                flat=flat, shop=shop, tarp=0, len=0.0, note=note)


def K(i, side, kind, along, back, w=0.0, d=0.0, roof=0, tarp=0, length=0.0, note=""):
    return dict(i=i, side=side, kind=kind, along=along, back=back, w=w, d=d, lv=1, wall=0, roof=roof,
                flat=False, shop=False, tarp=tarp, len=length, note=note)


ITEMS = [
    # Bara Bazar: a row of two-storey wooden shop-houses right on the road (#003 left)
    H(3, "L", -10, 0.8, 8.5, 8, 2, 7, 2, False, True, "shop-house, carved gable"),
    H(3, "L", -1, 0.8, 9, 8, 2, 1, 1, False, True, "Al Gariba chicken corner"),
    H(3, "L", 8.5, 0.8, 9, 8, 2, 0, 2, False, True, "shop-house, green awning"),
    # Hotel Shanta Hill and its neighbour on the road (#004 right)
    H(4, "R", -4, 1.0, 10, 9, 3, 0, 6, True, True, "Hotel Shanta Hill"),
    H(4, "R", 7, 1.0, 8, 8, 3, 1, 6, True, False, "house with balconies"),
    # Rio Grand: grey three-storey front with a garage, a white glass-fronted block behind (#019 right)
    H(19, "R", -5, 4.5, 16, 10, 3, 7, 6, True, False, "Rio Grand front, garage"),
    H(19, "R", 8, 9, 12, 10, 4, 0, 6, True, False, "Rio Grand block behind"),
    # Two shuttered shops with a floor of flats over them and steps up the side (#023 left)
    H(23, "L", 1, 2.0, 10, 8, 3, 1, 6, True, False, "shuttered shops, steps"),
    # White iron arch over a path up into the bushes (#049 right)
    K(49, "R", "arch", -2, 5.0, note="white lattice arch"),
    # Tea stall with plastic chairs under the deodars on the gravel pull-off (#058 left)
    K(58, "L", "stall", 3, 8, tarp=1, note="tea stall"),
    K(58, "L", "stall", 7.5, 8, tarp=0, note="tea stall"),
    K(58, "L", "chairs", -2, 6, note="plastic chairs"),
    # Forest department: two tiers of mossy stone wall with whitewashed pillars and a board (#062-#064 right)
    K(62, "R", "terrace", 0, 1.2, length=40, note="van vibhag terrace walls"),
    K(63, "R", "terrace", 0, 1.2, length=40),
    K(64, "R", "terrace", 0, 1.2, length=40),
    K(63, "L", "kiosk", 2, 6, note="orange food kiosk"),
    # Long single-storey block, bare grey walls, red tin roof, across a gravel yard (#065 left)
    H(65, "L", -9, 13, 12, 7, 1, 7, 0, False, False, "red-roofed block"),
    H(65, "L", -0.5, 19, 10, 7, 1, 7, 0, False, False, "red-roofed block"),
    # A thatched dhaba, a jeep's stand, a flat-roofed white house behind (#073 left); a white house above a stone wall (#073 right)
    K(73, "L", "thatch", -14, 5, 6, 4, note="thatched dhaba"),
    H(73, "L", 12, 14, 9, 6, 1, 0, 6, True, False, "flat-roofed house"),
    H(73, "R", -5, 6, 7, 6, 1, 0, 4, True, False, "white house, blue door"),
    # A row of single-storey shops, red shutters, an open tin shed (#076 left); a long thatched shed (#076 right)
    H(76, "L", -9, 8, 9, 6, 1, 0, 2, True, True, "shops"),
    H(76, "L", 0.5, 8, 8, 6, 1, 9, 1, True, True, "red-shuttered shops"),
    K(76, "L", "shed", 8, 8, 7, 5, roof=5, note="open tin shed"),
    K(76, "R", "thatch", -12, 12, 12, 5, note="long thatched shed"),
    K(76, "R", "stall", 6, 10, tarp=3),
    # Sariyatal: stalls with umbrellas and tables at the road (#083 left); a painted shop-house with outside stairs (#084 right)
    K(83, "L", "stall", -6, 4, tarp=0),
    K(83, "L", "stall", -1, 4, tarp=1),
    K(83, "L", "stall", 4, 4, tarp=2),
    H(84, "R", 6, 3, 6, 6, 2, 9, 0, False, True, "painted shop, stairs"),
    # The orange-red house with a green tin wing and an orange compound wall on the bend ahead (#090 right)
    H(90, "R", 24, 5, 9, 7, 2, 9, 2, True, False, "orange house on the bend"),
    H(90, "R", 32, 6, 6, 6, 1, 9, 2, False, False, "its green-roofed wing"),
    # A small hut with a green board on a pull-off (#091 left)
    K(91, "L", "shop", 0, 3.5, 3.2, note="hut"),
    # The Khurpatal village gate: a tin-roofed shed and a stall under a green tarp (#107 right)
    K(107, "R", "shed", 6, 12, 7, 5, roof=5, note="tin shed"),
    K(107, "R", "stall", -1, 9, tarp=3),
    # A white house down the path (#111 right)
    H(111, "R", 5, 22, 9, 8, 2, 0, 0, False, False, "white house, red roof"),
]

# the big hotels at Khurpatal: pale walls and maroon tin (#112-#116), not the random yellow
RESTYLE = [
    dict(x=-2567.0, z=-2270.0, r=40.0, wall=3, roof=1),
    dict(x=-2468.0, z=-2211.0, r=35.0, wall=3, roof=1),
]


def main():
    route = json.load(open(os.path.join(HERE, "kaladhungi_route.json")))
    world = json.load(open(os.path.join(ROOT, "Assets", "Nainital", "Data", "world.json"), encoding="utf-8"))
    line = route["line"]
    # half width per vertex: the nearest drivable road's
    roads = [(r["width"] * 0.5, list(zip(r["pts"][0::2], r["pts"][1::2]))) for r in world["roads"]
             if r["kind"] not in ("path", "footway", "bridleway", "steps", "track", "pedestrian", "cycleway")]
    hw = []
    for x, z in line:
        best = (1e9, 3.0)
        for half, pts in roads:
            for (ax, az), (bx, bz) in zip(pts, pts[1:]):
                dx, dz = bx - ax, bz - az
                L2 = dx * dx + dz * dz or 1e-9
                t = max(0.0, min(1.0, ((x - ax) * dx + (z - az) * dz) / L2))
                d = math.hypot(x - ax - t * dx, z - az - t * dz)
                if d < best[0]:
                    best = (d, half)
        hw.append(round(best[1], 2))
    metres = [0.0]
    for (ax, az), (bx, bz) in zip(line, line[1:]):
        metres.append(round(metres[-1] + math.hypot(bx - ax, bz - az), 2))
    points = []
    for s_i, s in enumerate(route["samples"]):
        L, R = SURVEY.get(s_i, ("", ""))
        points.append(dict(i=s_i, m=float(s["m"]), L=L, R=R))
    for it in ITEMS:
        it["along"] = float(it["along"]); it["back"] = float(it["back"]); it["w"] = float(it["w"]); it["d"] = float(it["d"])
        it["len"] = float(it["len"])
    out = dict(note="Kaladhungi road (DSA ground -> Khurpatal) read off Street View, 23 Nov 2026: Tools/streetview/kaladhungi_survey.py",
               line=[round(v, 2) for p in line for v in p], hw=hw, metres=metres, points=points, items=ITEMS, restyle=RESTYLE)
    dst = os.path.join(ROOT, "Assets", "Nainital", "Data", "streetview_kaladhungi.json")
    json.dump(out, open(dst, "w"), indent=0)
    print(f"{len(points)} points, {len(ITEMS)} items, {len(RESTYLE)} restyles, line {len(line)} vertices, {metres[-1]:.0f} m -> {dst}")


if __name__ == "__main__":
    main()

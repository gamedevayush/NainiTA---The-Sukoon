"""Shared map-area definition for the Nainital game world.

The world is a square of SIZE_M metres centred on Naini Lake.
Local coordinates: x = metres east of centre, z = metres north of centre.
"""
import math

CENTER_LAT = 29.3905
CENTER_LON = 79.4550
SIZE_M = 6000.0          # world is SIZE_M x SIZE_M metres (2026-11-11: grown west to take in Sariyatal and Khurpatal)
HEIGHTMAP_RES = 4097     # Unity terrain heightmap resolution (2^n + 1) -> ~1.46 m per sample

M_PER_DEG_LAT = 110_860.0
M_PER_DEG_LON = 111_320.0 * math.cos(math.radians(CENTER_LAT))

HALF = SIZE_M / 2.0
# The map square's centre in local metres. Local (0, 0) stays at CENTER_LAT/LON so every place keeps its coordinates;
# the square can sit off it (e.g. west, to take in Sariyatal and Khurpatal).
MAP_CX = -600.0   # covers x -3600 .. 2400: Khurpatal (-2.6 km) to the zoo and the Bhowali road
MAP_CZ = -300.0   # covers z -3300 .. 2700: Hanuman Garhi (-2.7 km) to Naina Peak
X0, Z0 = MAP_CX - HALF, MAP_CZ - HALF          # west and south edges, local metres
SOUTH = CENTER_LAT + Z0 / M_PER_DEG_LAT
NORTH = CENTER_LAT + (MAP_CZ + HALF) / M_PER_DEG_LAT
WEST = CENTER_LON + X0 / M_PER_DEG_LON
EAST = CENTER_LON + (MAP_CX + HALF) / M_PER_DEG_LON


def to_local(lat: float, lon: float) -> tuple[float, float]:
    """WGS84 -> local metres (x east, z north) relative to the map centre."""
    return (lon - CENTER_LON) * M_PER_DEG_LON, (lat - CENTER_LAT) * M_PER_DEG_LAT

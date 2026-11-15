using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The Tallital high street (user, 2026-11-14, Street View at the Nainital Highway Rd junction: "apne me to kuch hai
    /// hi nahi, real vs game bahut farq hai"). In the real town the main roads into Tallital are lined, wall to wall, with
    /// painted 3 and 4 storey concrete buildings (a purple lodge, a blue block, cream and pink hotels) with shops and
    /// shutters at street level. The generated houses along those roads were 1-2 storey, pale and set back. Houses that
    /// front a main road near the Tallital junction now take that look: 3-4 storeys, a bright wall, a flat terrace and a
    /// shop floor. Same houses, same footprints; only height, colour and the ground floor change.
    /// </summary>
    public static partial class WorldBuilder
    {
        const float HighStreetReach = 300f;   // metres from the Tallital traffic post
        // strong purple, blue and rose pink (Atlas.StrongWallPalette, made for the Sukhatal and Tallital Street View),
        // then teal, mustard, cream (the cream hotels), brick, yellow, lavender, pale pink
        static readonly int[] HighStreetWalls = { 12, 12, 13, 13, 14, 14, 11, 10, 1, 9, 2, 8, 3 };
        static readonly int[] ColonialWalls = { 0, 0, 1, 1, 4, 5, 3 };   // white, cream, pale blue, pale green, pale pink
        static readonly int[] ColonialRoofs = { 0, 0, 1, 2, 2, 3 };      // red, maroon, green, teal tin
        static List<Vector2[]> highStreetLines;

        /// <summary>The nearest point on the lake's shore line.</summary>
        static Vector2 NearestOnLakeEdge(WorldCtx ctx, Vector2 p)
        {
            Vector2 best = ctx.lake[0];
            float bd = float.MaxValue;
            for (int i = 0; i < ctx.lake.Length; i++)
            {
                Vector2 a = ctx.lake[i], b = ctx.lake[(i + 1) % ctx.lake.Length], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                Vector2 q = a + ab * t;
                float d = (q - p).sqrMagnitude;
                if (d < bd) { bd = d; best = q; }
            }
            return best;
        }
        // The two high streets: the Tallital junction (traffic post), and the Sukhatal / High Court chauraha on the
        // Kaladhungi road above Mallital, where the user's Street View was taken (Hotel Kohinoor, Beena Lodge;
        // located 2026-11-14 from his Scene view camera: looking at (-527, 522)).
        static readonly List<(Vector2 c, float r)> highStreetZones = new List<(Vector2, float)>();
        static readonly List<float> highStreetHalf = new List<float>();
        static readonly List<(Vector2 c, float r)> highStreetTaken = new List<(Vector2, float)>();

        static bool InHighStreetZone(Vector2 p)
        {
            foreach (var z in highStreetZones) if ((p - z.c).sqrMagnitude < z.r * z.r) return true;
            return false;
        }

        static void PrepareHighStreet(WorldCtx ctx)
        {
            highStreetLines = new List<Vector2[]>();
            highStreetHalf.Clear();
            highStreetTaken.Clear();
            highStreetZones.Clear();
            highStreetZones.Add((ctx.Poi("Tallital Traffic Post") ?? new Vector2(825f, -1145f), HighStreetReach));
            highStreetZones.Add((new Vector2(-527f, 522f), 260f));
            foreach (var r in ctx.world.roads)
            {
                if (r.pts == null || r.pts.Length < 4) continue;
                if (r.kind != "trunk" && r.kind != "primary" && r.kind != "trunk_link" && r.kind != "secondary") continue;
                if (r.name == "Mall Road" || r.name == "Upper Mall Road" || r.name == "Lower Mall Road") continue;   // Mall Road has its own look
                var pts = Geo.ToPoints(r.pts);
                var run = new List<Vector2>();
                foreach (var p in pts)
                {
                    if (InHighStreetZone(p)) { run.Add(p); continue; }
                    if (run.Count > 1) { highStreetLines.Add(run.ToArray()); highStreetHalf.Add(r.width * 0.5f); }
                    run.Clear();
                }
                if (run.Count > 1) { highStreetLines.Add(run.ToArray()); highStreetHalf.Add(r.width * 0.5f); }
            }
            Debug.Log($"[Nainital] high streets (Tallital, Sukhatal chauraha): {highStreetLines.Count} road runs");
        }

        /// <summary>Remember a building's footprint inside a high-street zone, so the infill does not overlap it.</summary>
        static void HighStreetOccupy(Vector2 c, float radius)
        {
            if (InHighStreetZone(c)) highStreetTaken.Add((c, radius));
        }

        /// <summary>
        /// The street wall (Street View: shops wall to wall, no grass between). Walking each high-street road on both
        /// sides in 1.5 m steps, a 3-4 storey painted shop building goes wherever the frontage is empty, starting where the
        /// last one ended: its front 1.5 m behind the road edge, not on another road, the lake or a kept-clear site, and
        /// clear of every building already there. Where a full-size one does not fit, a narrower or shallower one is
        /// tried (in front of a house set back from the road, beside a lane).
        /// (README 14 Nov, "grass gaps between high-street buildings": the old pass placed one every 8 m and tested
        /// overlap with circles of 0.8 x the half diagonal, about 9 m apart for two of them, so every other one failed.)
        /// </summary>
        static int HighStreetInfill(WorldCtx ctx, System.Func<Vector2, MeshBuilder[]> chunk)
        {
            if (highStreetLines == null) return 0;
            var allRoads = new List<(Vector2[] line, float half)>();
            foreach (var r in ctx.world.roads)
            {
                if (r.pts == null || r.pts.Length < 4) continue;
                var line = Geo.ToPoints(r.pts);
                bool near = false;
                foreach (var p in line) if (InHighStreetZone(p)) { near = true; break; }
                if (near) allRoads.Add((line, r.width * 0.5f));
            }
            bool OffRoads(Vector2 p, float margin)
            {
                foreach (var (line, half) in allRoads) if (Geo.DistanceToPolyline(line, p, false) < half + margin) return false;
                return true;
            }
            var existing = new List<(Vector2 c, float r)>(highStreetTaken);   // the map's houses (circles)
            var placed = new List<(Vector2 c, Vector2 ax, Vector2 ay, float hx, float hy)>();
            // a rectangle (centre, unit axes, half sizes) against a circle: the rectangle's nearest point to its centre
            bool HitsHouse(Vector2 c, Vector2 ax, Vector2 ay, float hx, float hy)
            {
                foreach (var t in existing)
                {
                    Vector2 d = t.c - c;
                    if (d.sqrMagnitude > (t.r + hx + hy) * (t.r + hx + hy)) continue;
                    Vector2 q = c + ax * Mathf.Clamp(Vector2.Dot(d, ax), -hx, hx) + ay * Mathf.Clamp(Vector2.Dot(d, ay), -hy, hy);
                    if ((q - t.c).sqrMagnitude < t.r * t.r) return true;
                }
                return false;
            }
            // two rectangles (separating axes); touching walls are allowed
            bool HitsInfill(Vector2 c, Vector2 ax, Vector2 ay, float hx, float hy)
            {
                foreach (var o in placed)
                {
                    Vector2 d = o.c - c;
                    if (d.sqrMagnitude > (hx + hy + o.hx + o.hy) * (hx + hy + o.hx + o.hy)) continue;
                    bool apart = false;
                    foreach (var axis in new[] { ax, ay, o.ax, o.ay })
                    {
                        float ra = hx * Mathf.Abs(Vector2.Dot(ax, axis)) + hy * Mathf.Abs(Vector2.Dot(ay, axis));
                        float rb = o.hx * Mathf.Abs(Vector2.Dot(o.ax, axis)) + o.hy * Mathf.Abs(Vector2.Dot(o.ay, axis));
                        if (Mathf.Abs(Vector2.Dot(d, axis)) >= ra + rb - 0.15f) { apart = true; break; }
                    }
                    if (!apart) return true;
                }
                return false;
            }
            // full size first, then narrower, then shallower
            (float w, float d)[] sizes = { (7.5f, 9f), (6f, 9f), (7.5f, 6.5f), (5f, 6.5f), (5f, 4.5f), (3.6f, 4.5f) };
            const float step = 1.5f;
            var rng = new System.Random(4242);
            int made = 0;
            for (int li = 0; li < highStreetLines.Count; li++)
            {
                var line = highStreetLines[li];
                float half = highStreetHalf[li];
                foreach (float side in new[] { -1f, 1f })
                {
                    float s = -step * 0.5f, nextFree = float.MinValue;
                    foreach (var (p, tan) in ResampleWithTangent(line, step))
                    {
                        s += step;
                        if (s < nextFree) continue;
                        Vector2 n = new Vector2(-tan.y, tan.x), toRoad = -n * side, along = tan;
                        Vector2 frontStart = p + n * side * (half + 1.5f);
                        foreach (var (w0, d) in sizes)
                        {
                            float w = w0 + (float)(rng.NextDouble() - 0.5) * 0.8f;
                            Vector2 c = frontStart + n * side * (d * 0.5f) + along * (w * 0.5f);
                            float hx = w * 0.5f, hy = d * 0.5f;
                            if (!InHighStreetZone(c) || ctx.InLake(c) || ctx.LakeDistance(c) < 10f + hy || ctx.InKeepClear(c, Mathf.Max(hx, hy))) continue;
                            if (HitsHouse(c, along, toRoad, hx - 0.3f, hy - 0.3f) || HitsInfill(c, along, toRoad, hx, hy)) continue;
                            Vector2 fl = c + toRoad * hy - along * hx, fr = c + toRoad * hy + along * hx;
                            Vector2 bl = c - toRoad * hy - along * hx, br = c - toRoad * hy + along * hx;
                            if (!OffRoads(fl, 1.2f) || !OffRoads(fr, 1.2f) || !OffRoads(bl, 1.2f) || !OffRoads(br, 1.2f) || !OffRoads(c, 1.2f)) continue;
                            float gMin = float.MaxValue, gMax = float.MinValue;
                            foreach (var q in new[] { fl, fr, bl, br, c })
                            {
                                float g = ctx.H(q);
                                gMin = Mathf.Min(gMin, g); gMax = Mathf.Max(gMax, g);
                            }
                            if (gMax - gMin > 7f) continue;   // a cliff: leave it
                            float front = ctx.H(p);           // the shop floor at the road's level, as in the photos
                            float streetTop = Mathf.Max(front + 0.25f, gMin + 0.4f);
                            float yaw = Mathf.Atan2(toRoad.x, toRoad.y) * Mathf.Rad2Deg;
                            int levels = rng.NextDouble() < 0.4 ? 4 : 3;
                            var st = new BuildingKit.Style
                            {
                                wall = HighStreetWalls[rng.Next(HighStreetWalls.Length)], roof = rng.Next(Atlas.RoofColors),
                                flatRoof = true, shop = true, litChance = 0.6f,
                            };
                            var ch = chunk(c);
                            BuildingKit.House(ch[0], ch[1], c, w, d, yaw, levels, gMin, Mathf.Min(gMax, streetTop), st, rng);
                            placed.Add((c, along, toRoad, hx, hy));
                            highStreetTaken.Add((c, Mathf.Min(hx, hy)));
                            nextFree = s + w;
                            made++;
                            break;
                        }
                    }
                }
            }
            return made;
        }

        static IEnumerable<(Vector2 p, Vector2 tan)> ResampleWithTangent(Vector2[] line, float step)
        {
            float carry = step * 0.5f;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 1e-3f) continue;
                Vector2 tan = (b - a) / len;
                float t = carry;
                while (t < len) { yield return (a + tan * t, tan); t += step; }
                carry = t - len;
            }
        }

        /// <summary>
        /// Trees between the houses of the town (user, 2026-11-14, satellite vs game round Pant Park, the Boat House Club and
        /// the Flats: "trees wagera nahi hain"). The map has only the forest and a few town trees; the satellite shows a deodar
        /// or an oak in most gaps and rows of them round the parks and grounds. This fills open ground within 500 m of the lake
        /// (not on a road, a roof, a court, the Flats or a kept-clear site) where no tree stands within 7 m. Instanced like the
        /// forest, so it costs little.
        /// </summary>
        static int TownTreeFill(WorldCtx ctx, List<float> data, System.Random rnd)
        {
            var grounds = new List<Vector2[]>();
            var parks = new List<Vector2[]>();
            foreach (var a in ctx.world.areas)
            {
                if (a.kind == "stadium" || a.kind == "pitch" || a.kind == "playground") grounds.Add(Geo.ToPoints(a.pts));
                else if (a.kind == "park") parks.Add(Geo.ToPoints(a.pts));
            }
            bool In(List<Vector2[]> polys, Vector2 p) { foreach (var poly in polys) if (Geo.PointInPolygon(poly, p)) return true; return false; }

            // existing trees in 7 m cells, so the fill only goes where the map has none
            const float cell = 7f;
            var taken = new HashSet<Vector2Int>();
            for (int i = 0; i + 5 < data.Count; i += 6)
                taken.Add(new Vector2Int(Mathf.FloorToInt(data[i] / cell), Mathf.FloorToInt(data[i + 2] / cell)));
            bool Near(Vector2 p)
            {
                int cx = Mathf.FloorToInt(p.x / cell), cz = Mathf.FloorToInt(p.y / cell);
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) if (taken.Contains(new Vector2Int(cx + dx, cz + dz))) return true;
                return false;
            }

            Vector2 lo = ctx.lake[0], hi = ctx.lake[0];
            foreach (var p in ctx.lake) { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
            lo -= Vector2.one * 500f; hi += Vector2.one * 500f;
            Physics.SyncTransforms();
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 10) | (1 << 4));
            int made = 0;
            for (float z = lo.y; z < hi.y; z += 8f)
                for (float x = lo.x; x < hi.x; x += 8f)
                {
                    var p = new Vector2(x + (float)(rnd.NextDouble() - 0.5) * 6f, z + (float)(rnd.NextDouble() - 0.5) * 6f);
                    bool park = In(parks, p);
                    if (rnd.NextDouble() > (park ? 0.55 : 0.3)) continue;
                    if (ctx.InLake(p)) continue;
                    float dl = ctx.LakeDistance(p);
                    if (dl < 5f || dl > 500f) continue;
                    float y = ctx.H(p);
                    if (y - ctx.lakeY > 140f) continue;
                    if (Near(p) || NearAnyRoad(ctx, p) || ctx.InKeepClear(p, 3f) || In(grounds, p) || NearMallRoad(ctx, p, 12f)) continue;
                    if (Physics.Raycast(new Vector3(p.x, y + 40f, p.y), Vector3.down, out var hit, 60f, mask, QueryTriggerInteraction.Ignore)
                        && hit.collider is not TerrainCollider && hit.point.y > y - 0.6f) continue;   // a roof, a wall, a stall
                    bool blocked = false;   // a wall or a stall within 2 m of the trunk
                    foreach (var c in Physics.OverlapSphere(new Vector3(p.x, y + 2f, p.y), 2.2f, mask, QueryTriggerInteraction.Ignore))
                        if (c is not TerrainCollider) { blocked = true; break; }
                    if (blocked) continue;
                    double r = rnd.NextDouble();
                    float type = r < 0.45 ? 0 : r < 0.8 ? 2 : r < 0.9 ? 3 : 1;   // deodar, banj oak, cypress, chir
                    data.Add(p.x); data.Add(y - 0.15f); data.Add(p.y);
                    data.Add(type); data.Add(0.8f + (float)rnd.NextDouble() * 0.35f); data.Add((float)rnd.NextDouble() * 360f);
                    taken.Add(new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell)));
                    made++;
                }
            return made;
        }

        /// <summary>
        /// Forest on the open hillsides round the lake (user, 2026-11-15: "make more photorealistic"). From the Mallital ghat
        /// and the Flats, the slopes across the lake read as bald khaki bands with a scatter of trees, where every photo shows
        /// one dark canopy of banj oak and deodar. The map's forest data and TownTreeFill (only up to 140 m over the lake)
        /// both leave those slopes empty. This fills open ground on slopes of 14-48 degrees (flat lawns and grounds are left,
        /// and so are cliffs and slips, to stay bare), 25-520 m over the lake within 1.6 km of it, where the ground is not
        /// rock or bare earth and no tree, road, roof or kept-clear site is near. Instanced like the rest of the forest.
        /// </summary>
        static int HillForestFill(WorldCtx ctx, List<float> data, System.Random rnd)
        {
            const int Cap = 40000;
            const float cell = 7f;
            var taken = new HashSet<Vector2Int>();
            for (int i = 0; i + 5 < data.Count; i += 6)
                taken.Add(new Vector2Int(Mathf.FloorToInt(data[i] / cell), Mathf.FloorToInt(data[i + 2] / cell)));
            // its own 7 m cell only: the map's forest is there but thin (one tree in ~20 m), and testing the 3 x 3 cells
            // round each point counted every thin patch as full (the first run added 353 trees)
            bool Near(Vector2 p) => taken.Contains(new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell)));
            var grounds = new List<Vector2[]>();
            foreach (var a in ctx.world.areas)
                if (a.kind == "stadium" || a.kind == "pitch" || a.kind == "playground" || a.kind == "park") grounds.Add(Geo.ToPoints(a.pts));
            bool InGround(Vector2 p) { foreach (var poly in grounds) if (Geo.PointInPolygon(poly, p)) return true; return false; }

            var td = ctx.terrain.terrainData;
            Vector3 tp = ctx.terrain.transform.position, sz = td.size;
            int ar = td.alphamapResolution, layers = td.alphamapLayers;
            // rock (2) and bare earth (3) of the splat at p
            float Bare(Vector2 p)
            {
                if (layers < 4) return 0f;
                int ax = Mathf.Clamp(Mathf.RoundToInt((p.x - tp.x) / sz.x * (ar - 1)), 0, ar - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt((p.y - tp.z) / sz.z * (ar - 1)), 0, ar - 1);
                var w = td.GetAlphamaps(ax, az, 1, 1);
                return w[0, 0, 2] + w[0, 0, 3];
            }

            Physics.SyncTransforms();
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 10) | (1 << 4));
            Vector2 c0 = ctx.lakeCenter;
            const float reach = 1600f;
            var found = new List<(Vector2 p, float y, float type, float scale, float rot)>();
            var why = new int[9];   // why open spots were refused: lake, height, flat, steep, chance, tree, road/site/ground, bare cliff, roof/wall
            for (float z = c0.y - reach; z < c0.y + reach; z += cell)
                for (float x = c0.x - reach; x < c0.x + reach; x += cell)
                {
                    var p = new Vector2(x + (float)(rnd.NextDouble() - 0.5) * 6f, z + (float)(rnd.NextDouble() - 0.5) * 6f);
                    if ((p - c0).sqrMagnitude > reach * reach) continue;
                    if (ctx.InLake(p) || ctx.LakeDistance(p) < 14f) { why[0]++; continue; }
                    float y = ctx.H(p), rel = y - ctx.lakeY;
                    if (rel < 10f || rel > 560f) { why[1]++; continue; }
                    float gx = ctx.H(p + new Vector2(3f, 0f)) - ctx.H(p - new Vector2(3f, 0f));
                    float gz = ctx.H(p + new Vector2(0f, 3f)) - ctx.H(p - new Vector2(0f, 3f));
                    float steep = Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz) / 6f) * Mathf.Rad2Deg;
                    if (steep < 14f) { why[2]++; continue; }
                    // oak and deodar hold on far steeper ground than 48 degrees in Kumaon; only the true cliffs stay bare -
                    // except the 63-80 degree face just above the west shore (a 30 m DEM edge at the lake): bare, it read
                    // as a sunlit khaki band from Mallital whatever the rock colour (a darker tint moved it only 7 %), so
                    // smaller trees cling to it there, as they do on the real slopes along Thandi Sadak
                    bool clingFace = steep > 62f && ctx.LakeDistance(p) < 160f && rel < 140f;
                    if (steep > 62f && !clingFace) { why[3]++; continue; }
                    if (rnd.NextDouble() > 0.7) { why[4]++; continue; }
                    if (Near(p)) { why[5]++; continue; }
                    // the face rises straight off Thandi Sadak, inside the 7-13 m road margin that kept every tree off it;
                    // on the face, 8 m over the lake is already above the path (about 3 m back on a 70 degree slope)
                    if (clingFace && rel < 8f) { why[6]++; continue; }
                    if (ctx.InKeepClear(p, 3f) || (!clingFace && NearAnyRoad(ctx, p)) || InGround(p)) { why[6]++; continue; }
                    // rock under the trees is fine (the hills across the lake are rock-floored forest); bare slips stay bare
                    if (steep > 45f && Bare(p) > 0.6f && !clingFace) { why[7]++; continue; }
                    if (Physics.Raycast(new Vector3(p.x, y + 40f, p.y), Vector3.down, out var hit, 60f, mask, QueryTriggerInteraction.Ignore)
                        && hit.collider is not TerrainCollider && hit.point.y > y - 0.6f) { why[8]++; continue; }   // a roof, a wall, a stall
                    bool blocked = false;
                    foreach (var col in Physics.OverlapSphere(new Vector3(p.x, y + 2f, p.y), 3f, mask, QueryTriggerInteraction.Ignore))
                        if (col is not TerrainCollider) { blocked = true; break; }
                    if (blocked) { why[8]++; continue; }
                    // banj oak and deodar above all, some chir pine low down, a few cypress near the town
                    double r = rnd.NextDouble();
                    float type = rel < 200f ? (r < 0.38 ? 0 : r < 0.75 ? 2 : r < 0.9 ? 1 : 3)
                                            : (r < 0.35 ? 0 : r < 0.85 ? 2 : 1);
                    if (type == 2 && rel > 90f && rnd.NextDouble() < 0.25) type = 4;   // buransh in the oak forest
                    // on the face: smaller, and sunk a little more so the roots sit in the rock
                    float scale = clingFace ? 0.65f + (float)rnd.NextDouble() * 0.3f : 0.85f + (float)rnd.NextDouble() * 0.45f;
                    found.Add((p, clingFace ? y - 0.6f : y, type, scale, (float)rnd.NextDouble() * 360f));
                    taken.Add(new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell)));
                }
            // over the cap: thinned evenly over the whole basin, not cut off at whichever row the count ran out
            double keep = found.Count > Cap ? (double)Cap / found.Count : 1.0;
            int made = 0;
            foreach (var f in found)
            {
                if (keep < 1.0 && rnd.NextDouble() > keep) continue;
                data.Add(f.p.x); data.Add(f.y - 0.2f); data.Add(f.p.y);
                data.Add(f.type); data.Add(f.scale); data.Add(f.rot);
                made++;
            }
            Debug.Log($"[Nainital] hillside forest: {found.Count} open spots found, {made} planted; refused - lake {why[0]}, height {why[1]}, " +
                      $"flat {why[2]}, cliff {why[3]}, chance {why[4]}, tree there {why[5]}, road/site/ground {why[6]}, bare slip {why[7]}, roof/wall {why[8]}");
            return made;
        }

        /// <summary>True when a house fronts a main road near the Tallital junction; sets its storeys and wall colour.</summary>
        static bool HighStreetHouse(WorldCtx ctx, Vector2 c, float w, float d, int index, ref int levels, ref int wall)
        {
            if (highStreetLines == null || highStreetLines.Count == 0) return false;
            if (!InHighStreetZone(c)) return false;
            if (ctx.LakeDistance(c) < 10f) return false;
            float reach = Mathf.Max(w, d) * 0.5f + 16f;
            bool front = false;
            foreach (var line in highStreetLines)
                if (Geo.DistanceToPolyline(line, c, false) < reach) { front = true; break; }
            if (!front) return false;
            uint hsh = (uint)index * 2654435761u;
            levels = Mathf.Max(levels, (hsh >> 8) % 3 == 0 ? 4 : 3);
            wall = HighStreetWalls[(int)((hsh >> 12) % (uint)HighStreetWalls.Length)];
            return true;
        }
    }
}

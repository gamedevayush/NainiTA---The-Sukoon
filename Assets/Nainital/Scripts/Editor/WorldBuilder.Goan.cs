using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// G.O.A.N. buildings in Nainital: civic buildings at their real OSM places (hospital, schools) or on
    /// the nearest flat free plot when the data has no location (mosque, kotwali, market rows, temple,
    /// water tower), bus-stop shelters along Mall Road, and G.O.A.N. houses replacing some generated
    /// houses (town houses near the lake, village houses on the outskirts). Planned before the generated
    /// houses so those are not built where a G.O.A.N. building stands.
    /// </summary>
    public static partial class WorldBuilder
    {
        public class GoanPlacement
        {
            public string kit;
            public Vector2 pos;
            public float yaw, scale = 1f;
            public float top, gMin;      // building stands at top; plinth (civic only) reaches down to gMin
            public bool civic;
            public string title, info;
        }

        static readonly string[] GoanTownHouses =
        {
            "HouseProto_1", "HouseProto_2", "HouseProto_3", "HouseProto_4", "HouseProto_5", "HouseProto_6", "HouseProto_7",
            "HouseProto_8", "HouseProto_10", "HouseProto_11", "HouseProto_12", "HouseProto_13", "HouseProto_14",
            "HouseProto_15", "HouseProto_16", "HouseProto_17",
        };

        static List<Vector2[]> roadLines;

        /// <summary>
        /// Plazas, bazaars and the bus stand are level ground in the real town. Without this they sit on
        /// tall stone blocks wherever the DEM slopes. Runs before the roads are draped over the terrain.
        /// </summary>
        static void FlattenSites(WorldCtx ctx)
        {
            void Pad(Vector2? p, float r, float minLake = 14f)
            {
                if (p.HasValue) FlattenPad(ctx, ctx.PushFromLake(p.Value, minLake + r * 0.5f), r + 2f);
            }
            Pad(ctx.Poi("Naina Devi"), 12f);
            Pad(ctx.Poi("Gurudwara"), 10f);
            Pad(ctx.Poi("Church"), 15f);
            Pad(ctx.Poi("Raj Bhavan"), 30f);
            Pad(ctx.Poi("Bada Bazar"), 14f);
            Pad(ctx.Poi("Snow View"), 19f);              // the plaza on the top
            Pad(ctx.Poi("Naina Peak summit"), 8f);       // viewpoints stand on levelled ground, not on stone blocks
            Pad(ctx.Poi("Tiffin Top"), 8f);
            Pad(ctx.Poi("Land's End"), 7f);
            Pad(ctx.Poi("Himalaya Darshan"), 9f);
            Pad(ctx.Poi("Tower 360"), 14f);
            Pad(ctx.Poi("Lovers Point"), 7f);
            Pad(ctx.Poi("Barapathar"), 11f);
            Pad(ctx.Poi("Khurpatal Lake View Point"), 8f);
            Pad(ctx.Poi("Khurpatal View Point"), 8f);
            Pad(ctx.Poi("Eco Cave Garden"), 13f);
            Pad(ctx.Poi("Nainital Zoo"), 7f);
            Pad(ctx.Poi("Nainital Stadium"), 16f);
            var flats = ctx.Poi("Nainital Stadium");
            var tibetan = ctx.Poi("Tibetan Market");   // by Naina Devi's gate (user's photo spheres)
            if (tibetan.HasValue) FlattenPad(ctx, tibetan.Value, 16f);
            foreach (var (c, r) in TallitalPads(ctx)) FlattenPad(ctx, c, r);                                   // Tallital bus stand
            Pad(ctx.Poi("BD Pandey"), 14f);
            foreach (var s in new[] { "Sherwood College", "Saint Joseph's College", "Saint Mary's Convent", "All Saint's College",
                                      "Longview Public School", "Government Girls Inter College", "Government Primary School, Tallital" })
                Pad(ctx.Poi(s), 13f);
        }

        /// <summary>Level the terrain to its median height inside radius, blending back to the hillside outside.</summary>
        static void FlattenPad(WorldCtx ctx, Vector2 c, float radius)
        {
            var td = ctx.terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 size = td.size, tp = ctx.terrain.transform.position;
            float blend = Mathf.Max(radius * 0.9f, 12f), R = radius + blend;
            float cellX = size.x / (res - 1), cellZ = size.z / (res - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - R - tp.x) / cellX), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + R - tp.x) / cellX), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.y - R - tp.z) / cellZ), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((c.y + R - tp.z) / cellZ), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            if (w < 2 || h < 2) return;
            var hs = td.GetHeights(x0, z0, w, h);
            var inside = new List<float>();
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    float wx = tp.x + (x0 + x) * cellX, wz = tp.z + (z0 + z) * cellZ;
                    if ((new Vector2(wx, wz) - c).sqrMagnitude <= radius * radius) inside.Add(hs[z, x]);
                }
            if (inside.Count == 0) return;
            inside.Sort();
            float target = Mathf.Max(inside[inside.Count / 2], (ctx.lakeY + 1.6f - tp.y) / size.y);
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    float wx = tp.x + (x0 + x) * cellX, wz = tp.z + (z0 + z) * cellZ;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius, R, (new Vector2(wx, wz) - c).magnitude));
                    hs[z, x] = Mathf.Lerp(target, hs[z, x], t);
                }
            td.SetHeights(x0, z0, hs);
        }

        static void PlanGoan(WorldCtx ctx)
        {
            ctx.goanKit = GoanKit.Load();
            if (ctx.goanKit.Count == 0) { Debug.Log("[Nainital] G.O.A.N. kit not imported: skipped"); return; }
            roadLines = new List<Vector2[]>();
            foreach (var r in ctx.world.roads) roadLines.Add(Geo.ToPoints(r.pts));

            // procedural Gurudwara / Church landmarks are swapped for the G.O.A.N. models by LandmarkBuilder
            foreach (var poi in new[] { "Gurudwara", "Church of St. Francis" })
            {
                var p = ctx.Poi(poi);
                if (p.HasValue) ctx.keepClear.Add((p.Value, 14f));
            }

            Civic(ctx, "Proto_Hospital", ctx.Poi("BD Pandey"), 0f, "BD Pandey District Hospital",
                  "Nainital ka sarkari zila aspataal (Mallital). Chot lagi ho ya tabiyat kharab, yahin aana.");
            var schools = new (string poi, string info)[]
            {
                ("Sherwood College", "1869 ka mashhoor boarding school - Amitabh Bachchan yahin padhe the."),
                ("Saint Joseph's College", "1888 mein bana boys' school - Nainital ke purane schoolon mein se ek."),
                ("Saint Mary's Convent", "1878 se chal raha ladkiyon ka convent school."),
                ("All Saint's College", "1869 ka ladkiyon ka boarding school, pahadi par."),
                ("Longview Public School", "Tallital ke paas ka public school."),
                ("Government Girls Inter College", "Ladkiyon ka sarkari inter college."),
                ("Government Primary School, Tallital", "Tallital ka sarkari primary school."),
            };
            foreach (var s in schools)
            {
                var p = ctx.Poi(s.poi);
                if (p.HasValue) Civic(ctx, "Proto_School", p, 0f, PoiName(ctx, s.poi), s.info);
            }
            // no location in the data: nearest suitable flat plot around a sensible spot
            var nainaDevi = ctx.Poi("Naina Devi");
            var flats = ctx.Poi("Nainital Stadium");
            var bazaar = ctx.Poi("Bada Bazar");
            Civic(ctx, "Proto_Mosque", nainaDevi, 140f, "Jama Masjid (Mallital)",
                  "Mallital ki masjid - Naina Devi mandir aur Gurudwara ke paas. Teen dharm, ek mohalla.");
            Civic(ctx, "Proto_PoliceStation", flats, 160f, "Mallital Kotwali",
                  "Police station. Kuch gadbad ho - chori, jhagda, gunde - to yahan report karo.");
            Civic(ctx, "Proto_MarketRow", bazaar, 120f, null, null);
            Civic(ctx, "Proto_MarketRow", bazaar, 160f, null, null);
            Civic(ctx, "Proto_WaterTower", ctx.Poi("Sherwood College"), 200f, null, null);
            Civic(ctx, "Proto_Temple", ctx.Poi("Sukha Tal") ?? PondCentre(ctx, "Sukha Tal"), 220f, "Chhota Mandir",
                  "Pahadi raaste ka chhota mandir. Raahgir yahan ruk kar haath jodte hain.");
            BusStops(ctx);
            Debug.Log($"[Nainital] G.O.A.N. plan: {ctx.goan.Count} civic sites");
        }

        static string PoiName(WorldCtx ctx, string contains)
        {
            foreach (var p in ctx.world.pois)
                if (!string.IsNullOrEmpty(p.name) && p.name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0) return p.name;
            return contains;
        }

        static Vector2? PondCentre(WorldCtx ctx, string name)
        {
            foreach (var p in ctx.world.ponds)
                if (p.name == name)
                {
                    var pts = Geo.ToPoints(p.pts);
                    Vector2 c = Vector2.zero;
                    foreach (var q in pts) c += q;
                    return c / pts.Length;
                }
            return null;
        }

        /// <summary>A civic G.O.A.N. building at target (search radius 0 = exactly there), facing the nearest road.</summary>
        static void Civic(WorldCtx ctx, string kit, Vector2? target, float search, string title, string info)
        {
            if (!target.HasValue || !ctx.goanKit.TryGetValue(kit, out var b)) return;
            float r = 0.5f * Mathf.Sqrt(b.size.x * b.size.x + b.size.z * b.size.z);
            Vector2 p = target.Value;
            if (search > 0f)
            {
                var found = FindPlot(ctx, target.Value, r, search);
                if (!found.HasValue) { Debug.LogWarning($"[Nainital] G.O.A.N. {kit}: no free plot near {target.Value}"); return; }
                p = found.Value;
            }
            else p = ctx.PushFromLake(p, r + 10f);
            Ground(ctx, p, r, out float gMin, out float gMax);
            ctx.goan.Add(new GoanPlacement
            {
                kit = kit, pos = p, yaw = FaceRoad(p), top = Mathf.Max(gMax + 0.15f, ctx.lakeY + 1.5f), gMin = gMin,
                civic = true, title = title, info = info,
            });
            ctx.keepClear.Add((p, r + 3f));
        }

        static void Ground(WorldCtx ctx, Vector2 p, float r, out float gMin, out float gMax)
        {
            gMin = float.MaxValue; gMax = float.MinValue;
            for (int i = 0; i < 12; i++)
                foreach (float rr in new[] { 0f, r * 0.6f, r })
                {
                    float a = i * Mathf.PI / 6f;
                    float g = ctx.H(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
                    gMin = Mathf.Min(gMin, g); gMax = Mathf.Max(gMax, g);
                }
        }

        static float NearestRoad(Vector2 p, out Vector2 onRoad)
        {
            float best = float.MaxValue;
            onRoad = p;
            foreach (var line in roadLines)
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    Vector2 a = line[i], ab = line[i + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    Vector2 q = a + ab * t;
                    float d = (p - q).sqrMagnitude;
                    if (d < best) { best = d; onRoad = q; }
                }
            return Mathf.Sqrt(best);
        }

        static float FaceRoad(Vector2 p)
        {
            NearestRoad(p, out var q);
            Vector2 f = q - p;
            return f.sqrMagnitude > 0.01f ? Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg : 0f;
        }

        /// <summary>Flattest free plot within search of near: off the lake and roads, clear of POIs and other sites.</summary>
        static Vector2? FindPlot(WorldCtx ctx, Vector2 near, float r, float search)
        {
            Vector2? best = null;
            float bestScore = float.MaxValue;
            for (float d = r + 10f; d <= search; d += 6f)
            {
                int n = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * d / 8f));
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    Vector2 p = near + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    if (ctx.InLake(p) || ctx.LakeDistance(p) < r + 14f || ctx.InKeepClear(p, r + 4f)) continue;
                    float road = NearestRoad(p, out _);
                    if (road < r + 5f || road > r + 40f) continue;          // beside a road, not on it
                    bool nearPoi = false;
                    foreach (var poi in ctx.world.pois)
                        if ((new Vector2(poi.x, poi.z) - p).sqrMagnitude < (r + 22f) * (r + 22f)) { nearPoi = true; break; }
                    if (nearPoi) continue;
                    Ground(ctx, p, r, out float gMin, out float gMax);
                    float score = (gMax - gMin) * 10f + d * 0.05f;            // flat first, then close
                    if (score < bestScore) { bestScore = score; best = p; }
                }
            }
            return best;
        }

        static void BusStops(WorldCtx ctx)
        {
            if (RoadPoints(ctx, MallUpper) != null || !ctx.goanKit.TryGetValue("Proto_BusStand", out var b)) return;   // no bus stops on the real Mall Road
            float spacing = 320f, next = 120f;
            foreach (var line in ctx.mallRoad)
            {
                float run = 0f;
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    Vector2 a = line[i], ab = line[i + 1] - a;
                    float len = ab.magnitude;
                    while (run + len >= next)
                    {
                        Vector2 dir = ab / Mathf.Max(len, 1e-3f), n = new Vector2(-dir.y, dir.x);
                        Vector2 c = a + dir * (next - run);
                        // the side away from the lake
                        Vector2 p = ctx.LakeDistance(c + n * 6f) > ctx.LakeDistance(c - n * 6f) ? c + n * 6.5f : c - n * 6.5f;
                        if (!ctx.InLake(p) && !ctx.InKeepClear(p, 3f))
                        {
                            Ground(ctx, p, 2.5f, out float gMin, out float gMax);
                            Vector2 f = c - p;
                            ctx.goan.Add(new GoanPlacement
                            {
                                kit = "Proto_BusStand", pos = p, yaw = Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg,
                                top = gMax + 0.1f, gMin = gMin, civic = true,
                            });
                            ctx.keepClear.Add((p, 4f));
                        }
                        next += spacing;
                    }
                    run += len;
                }
            }
        }

        /// <summary>
        /// Called for each generated house: may swap it for a G.O.A.N. house (town houses within 1 km of the
        /// lake, village houses beyond). Adds a plinth to the chunk mesh and records the placement.
        /// </summary>
        static bool TryGoanHouse(WorldCtx ctx, int index, Vector2 c, float w, float d, float yaw, float gMin, float gMax,
                                 bool shop, MeshBuilder opaque)
        {
            if (shop || ctx.goanKit.Count == 0) return false;
            var rnd = new System.Random(index * 7919 + 13);
            double roll = rnd.NextDouble();
            bool rural = ctx.LakeDistance(c) > 1000f;
            if (rural ? roll > 0.55 : roll > 0.12) return false;

            float big = Mathf.Max(w, d), small = Mathf.Min(w, d);
            var fits = new List<(string kit, float scale, bool turn)>();
            if (rural)
            {
                foreach (var kv in ctx.goanKit)
                    if (kv.Key.StartsWith("RuralHouse_")) AddFit(fits, kv.Value, big, small, w >= d);
            }
            else
            {
                foreach (var k in GoanTownHouses)
                    if (ctx.goanKit.TryGetValue(k, out var b)) AddFit(fits, b, big, small, w >= d);
            }
            if (fits.Count == 0) return false;
            var pick = fits[rnd.Next(fits.Count)];
            float top = gMax + 0.05f;
            float y = yaw + (pick.turn ? 90f : 0f) + (rnd.NextDouble() < 0.5 ? 180f : 0f);
            if (top - gMin > 0.25f)
            {
                float hgt = top - gMin + 0.3f;
                opaque.Box(new Vector3(c.x, top - hgt * 0.5f, c.y), new Vector3(w * 0.96f, hgt, d * 0.96f),
                           Quaternion.Euler(0, yaw, 0), Atlas.MaterialUV(Atlas.MatStone));
            }
            ctx.goan.Add(new GoanPlacement { kit = pick.kit, pos = c, yaw = y, scale = pick.scale, top = top, gMin = gMin });
            return true;
        }

        static void AddFit(List<(string, float, bool)> fits, GoanKit.Building b, float big, float small, bool lotAlongX)
        {
            float bx = b.size.x, bz = b.size.z;
            float bBig = Mathf.Max(bx, bz), bSmall = Mathf.Min(bx, bz);
            float s = Mathf.Min(big / bBig, small / bSmall);
            if (s < 0.8f) return;                        // building would hang out of the lot
            s = Mathf.Min(s, 1.15f);
            bool turn = (bx >= bz) != lotAlongX;         // rotate 90 deg so the long sides line up
            fits.Add((b.name, s, turn));
        }

        static void BuildGoan(WorldCtx ctx)
        {
            if (ctx.goan.Count == 0) return;
            var parent = new GameObject("G.O.A.N. buildings").transform;
            parent.SetParent(ctx.worldRoot, false);
            var plinths = new MeshBuilder();
            bool anyPlinth = false;
            int houses = 0, civic = 0;
            foreach (var g in ctx.goan)
            {
                if (!ctx.goanKit.TryGetValue(g.kit, out var b)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(b.prefab, parent);
                var rot = Quaternion.Euler(0, g.yaw, 0);
                go.transform.SetPositionAndRotation(new Vector3(g.pos.x, g.top, g.pos.y), rot);
                go.transform.localScale = Vector3.one * g.scale;
                if (!g.civic) { houses++; continue; }
                civic++;
                go.name = g.title ?? b.name;
                float pad = g.kit == "Proto_BusStand" ? 0.6f : 1.5f;
                if (g.top - g.gMin > 0.1f)
                {
                    float hgt = g.top - g.gMin + 0.4f;
                    plinths.Box(new Vector3(g.pos.x, g.top - hgt * 0.5f, g.pos.y),
                                new Vector3(b.size.x + pad * 2f, hgt, b.size.z + pad * 2f), rot, Atlas.MaterialUV(Atlas.MatStone));
                    anyPlinth = true;
                }
                if (!string.IsNullOrEmpty(g.title))
                {
                    var c3 = new Vector3(g.pos.x, g.top, g.pos.y);
                    float r = 0.5f * Mathf.Max(b.size.x, b.size.z);
                    ctx.Zone(g.title, c3, r + 25f);
                    ctx.landmarkSites[g.title] = c3;
                    Vector3 signPos = c3 + rot * new Vector3(r * 0.6f, 0f, b.size.z * 0.5f + pad + 1.2f);
                    signPos.y = Mathf.Max(ctx.H(new Vector2(signPos.x, signPos.z)), g.top - 0.2f);
                    ctx.InfoSign(g.title, g.info ?? g.title, signPos, rot, parent);
                }
            }
            if (anyPlinth) ctx.MeshObject("G.O.A.N. plinths", parent, plinths, ctx.mats.atlas, null, true, true);
            Debug.Log($"[Nainital] G.O.A.N. buildings: {civic} civic + {houses} houses");
        }

        /// <summary>LandmarkBuilder hook: put a G.O.A.N. model on a landmark site instead of the code-built one.</summary>
        public static bool PlaceGoanLandmark(WorldCtx ctx, string kit, Vector3 origin, Quaternion rot, Transform parent, out Vector3 size)
        {
            size = Vector3.zero;
            if (ctx.goanKit == null || !ctx.goanKit.TryGetValue(kit, out var b)) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(b.prefab, parent);
            go.transform.SetPositionAndRotation(origin, rot);
            size = b.size;
            return true;
        }
    }
}

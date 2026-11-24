using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>Where the town's sound beds come from (AudioManager.Zone): the bazaars, Mall Road, cafes and dhabas,
        /// children on the Flats, the Tallital bus stand, and the main roads.</summary>
        static void BuildSoundZones(WorldCtx ctx, AudioManager audio)
        {
            var kinds = new List<int>();
            var pos = new List<Vector3>();
            var radii = new List<float>();
            void Add(AudioManager.Zone k, Vector3 p, float r) { kinds.Add((int)k); pos.Add(p); radii.Add(r); }
            Vector3 At(Vector2 p) => new Vector3(p.x, ctx.H(p) + 1.5f, p.y);

            foreach (var kv in ctx.landmarkSites)
            {
                string n = kv.Key;
                if (n.Contains("Bazar") || n.Contains("Bazaar") || n.Contains("Market")) Add(AudioManager.Zone.Market, kv.Value, 70f);
                else if (n.Contains("Cafe") || n.Contains("Restaurant") || n.Contains("Dhaba") || n.Contains("Sakley") || n.Contains("Snow View") || n.Contains("Tiffin Top"))
                    Add(AudioManager.Zone.Cafe, kv.Value, 40f);
                else if (n.Contains("Flats")) Add(AudioManager.Zone.Kids, kv.Value, 95f);
                else if (n.Contains("Bus Stand") || n.Contains("Bus Station")) Add(AudioManager.Zone.BusStand, kv.Value, 55f);
            }
            if (ctx.hasBusStand) Add(AudioManager.Zone.BusStand, ctx.busStand, 55f);
            foreach (var key in new[] { "Bada Bazar", "Tibetan Market" })
            {
                var poi = ctx.Poi(key);
                if (poi.HasValue) Add(AudioManager.Zone.Market, At(poi.Value), 70f);
            }
            foreach (var line in ctx.mallRoad)
            {
                var pts = Resample(line, 30f, false);
                for (int i = 0; i < pts.Count; i++)
                {
                    Add(AudioManager.Zone.Street, At(pts[i]), 30f);
                    if (i % 5 == 2) Add(AudioManager.Zone.Cafe, At(pts[i]), 22f);   // cafes and restaurants all along the Mall
                }
            }
            foreach (var r in ctx.world.roads)
            {
                if (r.kind != "trunk" && r.kind != "primary" && r.kind != "secondary" && r.kind != "tertiary") continue;
                foreach (var q in Resample(Geo.ToPoints(r.pts), 40f, false)) Add(AudioManager.Zone.Traffic, At(q), 38f);
            }
            // (owner, 25 Nov 2026: "chidiya ka sound 3d space pe ho... har jagah murga, chidiya sunaai dera.. kuch places pe set
            // rkdo") birds only where there are trees, roosters only from the houses away from the bazaars.
            var town = new List<Vector2>();
            foreach (var line in ctx.mallRoad) town.AddRange(Resample(line, 40f, false));
            foreach (var key in new[] { "Bada Bazar", "Tibetan Market", "New Bus Station" }) { var t = ctx.Poi(key); if (t.HasValue) town.Add(t.Value); }
            if (ctx.hasBusStand) town.Add(new Vector2(ctx.busStand.x, ctx.busStand.z));
            float TownDist(Vector2 p)
            {
                float best = float.MaxValue;
                foreach (var t in town) best = Mathf.Min(best, (t - p).sqrMagnitude);
                return Mathf.Sqrt(best);
            }
            bool Inside(Vector2 p) => Mathf.Abs(p.x - ctx.world.meta.mapCx) < 2900f && Mathf.Abs(p.y - ctx.world.meta.mapCz) < 2900f;

            // birds: 80 m cells with 12+ trees, not in the heart of town (the Mall and the bazaars)
            var cells = new Dictionary<long, Vector3>();   // x sum, z sum, count
            long Key(float x, float z, float size) => ((long)Mathf.FloorToInt(x / size) << 32) ^ (uint)Mathf.FloorToInt(z / size);
            var tr = ctx.world.trees;
            if (tr != null)
                for (int i = 0; i + 3 < tr.Length; i += 4)
                {
                    long k = Key(tr[i], tr[i + 1], 80f);
                    cells.TryGetValue(k, out var c);
                    cells[k] = new Vector3(c.x + tr[i], c.y + tr[i + 1], c.z + 1f);
                }
            int birdZones = 0;
            foreach (var c in cells.Values)
            {
                if (c.z < 12f) continue;
                var p = new Vector2(c.x / c.z, c.y / c.z);
                if (!Inside(p) || TownDist(p) < 60f) continue;
                Add(AudioManager.Zone.Birds, At(p) + Vector3.up * 5f, c.z >= 60f ? 75f : 60f);
                birdZones++;
            }
            // the lakeside: trees along the shore and the Flats' edge (every 60 m round the lake, outside the Mall's own stretch)
            if (ctx.world.lake != null && ctx.world.lake.pts != null)
                foreach (var q in Resample(Geo.ToPoints(ctx.world.lake.pts), 60f, true))
                    if (TownDist(q) > 45f) { Add(AudioManager.Zone.Birds, new Vector3(q.x, ctx.lakeY + 4f, q.y), 45f); birdZones++; }

            // roosters: 120 m cells with 6+ houses, 150 m+ from the Mall, the bazaars and the bus stand
            cells.Clear();
            var hs = ctx.world.houses;
            if (hs != null)
                for (int i = 0; i + 8 < hs.Length; i += 9)
                {
                    long k = Key(hs[i], hs[i + 1], 120f);
                    cells.TryGetValue(k, out var c);
                    cells[k] = new Vector3(c.x + hs[i], c.y + hs[i + 1], c.z + 1f);
                }
            int roosterZones = 0;
            foreach (var c in cells.Values)
            {
                if (c.z < 6f) continue;
                var p = new Vector2(c.x / c.z, c.y / c.z);
                if (!Inside(p) || TownDist(p) < 150f) continue;
                // one in two such clusters keeps hens (hashed, the same every build)
                if (((uint)(Mathf.FloorToInt(p.x) * 73856093 ^ Mathf.FloorToInt(p.y) * 19349663) >> 7 & 1) == 0) continue;
                Add(AudioManager.Zone.Rooster, At(p) + Vector3.up * 1f, 160f);
                roosterZones++;
            }

            audio.zoneKinds = kinds.ToArray();
            audio.zonePositions = pos.ToArray();
            audio.zoneRadii = radii.ToArray();
            var count = new int[8];
            foreach (var k in kinds) count[k]++;
            Debug.Log($"[Nainital] sound zones: market {count[0]}, street {count[1]}, cafe {count[2]}, kids {count[3]}, bus {count[4]}, traffic {count[5]}, birds {count[6]}, rooster {count[7]}");
        }
    }
}

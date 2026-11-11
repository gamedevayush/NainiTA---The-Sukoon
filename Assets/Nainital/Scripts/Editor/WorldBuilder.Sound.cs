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
            audio.zoneKinds = kinds.ToArray();
            audio.zonePositions = pos.ToArray();
            audio.zoneRadii = radii.ToArray();
            var count = new int[6];
            foreach (var k in kinds) count[k]++;
            Debug.Log($"[Nainital] sound zones: market {count[0]}, street {count[1]}, cafe {count[2]}, kids {count[3]}, bus {count[4]}, traffic {count[5]}");
        }
    }
}

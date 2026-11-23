using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The town's two petrol pumps (owner, 21-22 Nov 2026, FREEROAM #4: "only the real ones in the map ... the real station's
    /// shape, a FAKE brand name"; his pins: Tallital 29.380533, 79.463878 and Sukhatal 29.393247, 79.446111). A roadside
    /// Indian pump: a flat canopy on four pillars with a fascia band in the brand's colours and its name in 3D letters, two
    /// dispenser islands with their hoses, a small office, an air pump, the price totem by the road; the forecourt levelled
    /// to the road and painted as cement into the terrain (no road mesh). Placed beside the nearest motor road on the pin's
    /// side, slid along the road (up to 32 m) and back from it until nothing stands in the way - no house, wall or road.
    /// Each gets a FuelStation (Runtime/World/FuelStation.cs: the forecourt where VehicleFuel fills up), a place name and a
    /// map blip, and two lamps under the canopy for the night.
    /// The brand is invented: "Himgiri Fuels" in green and white with a saffron stripe (not any real company's colours).
    /// </summary>
    public static partial class LandmarkBuilder
    {
        const string PumpBrand = "HIMGIRI";
        static readonly (string place, Vector2 pin)[] Pumps =
        {
            ("Tallital", new Vector2(688.5f, -1153.6f)),   // (owner, 23 Nov 2026: "petrol pump is at wrong place") the real one, IndianOil's locator: 29.380094, 79.462100 - west of the chowk on the lake road, not on Mall Road
            ("Sukhatal", new Vector2(-862.1f, 304.5f)),
        };

        // the forecourt, in the pump's frame: +x along the road, +z away from it, from the road's edge
        const float CourtW = 18f, CourtD = 13f;

        // planned before the houses (step "Pump and sit-out plans"): the forecourt's road-edge middle, the road's direction,
        // the way away from it, the point whose height is the road's level. The houses skip the reserved ground (keepClear)
        // - the first build (22 Nov) found no free spot at Tallital: every forecourt there touched a house.
        static readonly Dictionary<string, (Vector2 o, Vector2 along, Vector2 away, Vector2 level)> pumpPlans = new Dictionary<string, (Vector2, Vector2, Vector2, Vector2)>();

        internal static void PlanPetrolPumps(WorldCtx ctx)
        {
            pumpPlans.Clear();
            foreach (var (place, pin) in Pumps)
            {
                var why = new Dictionary<string, int>();
                if (!PumpSpot(ctx, pin, false, why, out Vector2 o, out Vector2 along, out Vector2 away, out Vector2 level))
                {
                    Debug.LogWarning($"[Nainital] petrol pump {place}: nothing to plan near ({pin.x:F0}, {pin.y:F0}) - {Why(why)}");
                    continue;
                }
                pumpPlans[place] = (o, along, away, level);
                ctx.keepClear.Add((o + away * (CourtD * 0.5f), new Vector2(CourtW, CourtD).magnitude * 0.5f + 2.5f));   // the corners too (22 Nov: a house stood on the first plan's corner)
                Debug.Log($"[Nainital] petrol pump {place}: planned at ({o.x:F1}, {o.y:F1}), {Vector2.Distance(o + away * (CourtD * 0.5f), pin):F1} m from the pin");
            }
        }

        static string Why(Dictionary<string, int> why)
        {
            var parts = new List<string>();
            foreach (var kv in why) parts.Add(kv.Key + " x" + kv.Value);
            parts.Sort((a, b) => string.CompareOrdinal(a, b));
            return parts.Count == 0 ? "no road" : string.Join(", ", parts);
        }

        static void Count(Dictionary<string, int> why, string k)
        {
            if (why == null) return;
            why.TryGetValue(k, out int n);
            why[k] = n + 1;
        }

        internal static void BuildPetrolPumps(WorldCtx ctx, Transform parent)
        {
            Physics.SyncTransforms();
            foreach (var (place, pin) in Pumps)
            {
                Vector2 o, along, away, level;
                var why = new Dictionary<string, int>();
                if (pumpPlans.TryGetValue(place, out var plan) && !CourtBlocked(ctx, plan.o, plan.along, plan.away, true, why))
                    (o, along, away, level) = plan;
                else
                {
                    if (pumpPlans.ContainsKey(place)) Debug.LogWarning($"[Nainital] petrol pump {place}: the planned forecourt is taken ({Why(why)}), looking again");
                    why.Clear();
                    if (!PumpSpot(ctx, pin, true, why, out o, out along, out away, out level))
                    {
                        Debug.LogWarning($"[Nainital] petrol pump {place}: no free spot near ({pin.x:F0}, {pin.y:F0}) - {Why(why)}");
                        continue;
                    }
                }
                BuildPump(ctx, parent, place, o, along, away, ctx.H(level));
            }
        }

        /// <summary>A free forecourt near the pin: its road-edge middle o, the road's direction and the way away from it.
        /// colliders: also test what is built (false while planning, before the houses: then the other plans count).</summary>
        static bool PumpSpot(WorldCtx ctx, Vector2 pin, bool colliders, Dictionary<string, int> why, out Vector2 o, out Vector2 along, out Vector2 away, out Vector2 level)
        {
            o = along = away = level = Vector2.zero;
            if (!WorldBuilder.NearestMotorRoad(ctx, pin, 80f, out Vector2 best, out Vector2 tan)) return false;
            tan = tan.normalized;
            Vector2 n = new Vector2(-tan.y, tan.x);
            float pinSide = Vector2.Dot(pin - best, n);
            var road = NearestRoadData(ctx, best);
            float half = road != null ? road.width * 0.5f : 3.5f;
            var sides = Mathf.Abs(pinSide) < 2f ? new[] { 1f, -1f } : new[] { Mathf.Sign(pinSide), -Mathf.Sign(pinSide) };
            foreach (float side in sides)
                for (int k = 0; k <= 16; k++)
                {
                    float s = (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * 4f;   // 0, +4, -4, +8, -8 ... 32
                    foreach (float back in new[] { 0.6f, 2f, 3.5f })
                    {
                        if (!WorldBuilder.NearestMotorRoad(ctx, best + tan * s, 20f, out Vector2 b2, out Vector2 t2)) continue;
                        t2 = t2.normalized;
                        if (Vector2.Dot(t2, tan) < 0f) t2 = -t2;
                        Vector2 n2 = new Vector2(-t2.y, t2.x) * side;
                        Vector2 edge = b2 + n2 * (half + back);
                        if (CourtBlocked(ctx, edge, t2, n2, colliders, why)) continue;
                        o = edge;
                        along = t2;
                        away = n2;
                        level = b2 + n2 * (half - 0.3f);
                        return true;
                    }
                }
            return false;
        }

        static RoadData NearestRoadData(WorldCtx ctx, Vector2 p)
        {
            RoadData best = null;
            float bd = float.MaxValue;
            foreach (var r in ctx.world.roads)
            {
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                float d = Geo.DistanceToPolyline(line, p, false);
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        /// <summary>Something in the forecourt: another road, a house, a wall, a pole (the ground itself does not count).</summary>
        static bool CourtBlocked(WorldCtx ctx, Vector2 o, Vector2 along, Vector2 away, bool colliders, Dictionary<string, int> why)
        {
            for (float x = -CourtW * 0.5f; x <= CourtW * 0.5f + 0.01f; x += 2f)
                for (float z = 0.5f; z <= CourtD + 0.01f; z += 2f)
                {
                    Vector2 p = o + along * x + away * z;
                    if (ctx.InLake(p)) { Count(why, "lake"); return true; }
                    if (!colliders && ctx.InKeepClear(p)) { Count(why, "another plan"); return true; }
                    foreach (var r in ctx.world.roads)
                    {
                        // a bus stand's plaza and its parking aisles (Tallital: the pump stands in the paved square) do not count
                        if (r.kind == "path" || r.kind == "footway" || r.kind == "steps" || r.kind == "track" ||
                            r.kind == "pedestrian" || r.kind == "service" || r.kind == "living_street") continue;
                        var line = Geo.ToPoints(r.pts);
                        if (line.Length < 2) continue;
                        if (Geo.DistanceToPolyline(line, p, false) < r.width * 0.5f + 0.3f) { Count(why, "road " + r.kind); return true; }
                    }
                }
            if (!colliders) return false;
            Vector2 c = o + away * (CourtD * 0.5f);
            float y = ctx.H(c);
            var q = Quaternion.LookRotation(new Vector3(away.x, 0f, away.y));
            var hits = Physics.OverlapBox(new Vector3(c.x, y + 3.5f, c.y), new Vector3(CourtW * 0.5f, 3f, CourtD * 0.5f), q, ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore);
            foreach (var h in hits) if (!(h is TerrainCollider)) { Count(why, "built " + h.name); return true; }
            return false;
        }

        static void BuildPump(WorldCtx ctx, Transform parent, string place, Vector2 o, Vector2 along, Vector2 away, float y0)
        {
            Vector3 A = new Vector3(along.x, 0f, along.y), Z = new Vector3(away.x, 0f, away.y);
            var q = Quaternion.LookRotation(Z);           // +z away from the road
            Vector3 P(float x, float y, float z) => new Vector3(o.x, y0, o.y) + A * x + Vector3.up * y + Z * z;

            // the ground: level with the road over the forecourt, eased back into the hillside
            Vector2 c2 = o + away * (CourtD * 0.5f);
            const float ease = 9f;
            float R = Mathf.Sqrt(CourtW * CourtW + CourtD * CourtD) * 0.5f + ease;
            ShapeTerrain(ctx, c2 - Vector2.one * R, c2 + Vector2.one * R, (p, y) =>
            {
                Vector2 d = p - o;
                float lx = Vector2.Dot(d, along), lz = Vector2.Dot(d, away);
                float ox = Mathf.Max(0f, Mathf.Abs(lx) - CourtW * 0.5f), oz = Mathf.Max(0f, Mathf.Max(-lz, lz - CourtD));
                float out_ = Mathf.Sqrt(ox * ox + oz * oz);
                if (lz < -0.5f) return y;                      // never touch the road side
                float k = Mathf.Clamp01(out_ / ease);
                k = k * k * (3f - 2f * k);
                return Mathf.Lerp(y0 - 0.02f, y, k);
            });
            // paint it as cement into the terrain's road mask (WorldBuilder.RoadMask, channel 1)
            var paint = new MeshBuilder();
            paint.Box(P(0f, 0f, CourtD * 0.5f), new Vector3(CourtW, 0.02f, CourtD), q, S(Pal.Concrete));
            WorldBuilder.PaintRoadSurface(paint, 1);

            var sm = new SiteMeshes();
            int brandPal = Pal.BoatGreen, stripePal = Pal.Saffron, white = Pal.ShirtWhite, grey = Pal.StoneGrey, dark = Pal.IronBlack;

            // ---- the canopy: a flat roof on four pillars, a fascia band in the brand's colours, white underneath
            const float cw = 13f, cd = 8f, ch = 5.4f, fasciaH = 0.95f;
            Vector3 cc = P(0f, ch, 6.2f);
            sm.atlas.Box(cc + Vector3.up * (fasciaH * 0.5f), new Vector3(cw, fasciaH, cd), q, S(brandPal));                     // the band all round
            sm.atlas.Box(cc + Vector3.up * (fasciaH * 0.30f), new Vector3(cw + 0.04f, 0.14f, cd + 0.04f), q, S(stripePal));        // the saffron stripe
            sm.atlas.Box(cc + Vector3.up * 0.01f, new Vector3(cw - 0.1f, 0.04f, cd - 0.1f), q, S(white));                           // the white underside
            foreach (float px in new[] { -4.2f, 4.2f })
                foreach (float pz in new[] { 4.4f, 8.0f })
                {
                    Vector3 b = P(px, 0f, pz);
                    sm.atlas.Box(b + Vector3.up * (ch * 0.5f), new Vector3(0.42f, ch, 0.42f), q, S(white));
                    sm.atlas.Box(b + Vector3.up * 0.45f, new Vector3(0.48f, 0.9f, 0.48f), q, S(brandPal));                       // the painted foot
                    sm.col.Box(b + Vector3.up * (ch * 0.5f), new Vector3(0.45f, ch, 0.45f), q, Vector2.zero);
                }
            // the brand in 3D letters on the band, on the road side and the far side
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 face = Z * side;
                Vector3 at = cc + Vector3.up * (fasciaH * 0.58f) + face * (cd * 0.5f + 0.02f);
                Glyph3D.Write(sm.loose, PumpBrand + " FUELS", Glyph3D.Face.Block, at, face, 0.46f, 0.03f, cw * 0.86f, S(white), S(grey));
            }
            // ---- two dispenser islands under the canopy
            foreach (float ix in new[] { -2.6f, 2.6f })
            {
                Vector3 b = P(ix, 0f, 6.2f);
                sm.atlas.Box(b + Vector3.up * 0.09f, new Vector3(1.1f, 0.18f, 4.2f), q, S(Pal.BoatYellow));                      // the kerb
                sm.atlas.Box(b + Vector3.up * 0.185f, new Vector3(0.9f, 0.01f, 4.0f), q, S(grey));
                sm.col.Box(b + Vector3.up * 0.4f, new Vector3(1.1f, 0.8f, 4.2f), q, Vector2.zero);
                foreach (float dz in new[] { -0.9f, 0.9f })
                {
                    Vector3 d = b + Z * dz + Vector3.up * 0.18f;
                    sm.atlas.Box(d + Vector3.up * 0.95f, new Vector3(0.75f, 1.9f, 0.5f), q, S(white));                        // the body
                    sm.atlas.Box(d + Vector3.up * 1.72f, new Vector3(0.78f, 0.36f, 0.53f), q, S(brandPal));                    // the head
                    sm.atlas.Box(d + Vector3.up * 1.25f + A * 0.0f + Z * 0.0f + A * 0.3f * 0f, new Vector3(0.5f, 0.28f, 0.54f), q, S(dark));   // the display
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Vector3 side = A * (sx * 0.39f);
                        sm.atlas.Box(d + side + Vector3.up * 0.95f, new Vector3(0.04f, 0.22f, 0.12f), q, S(dark));             // the nozzle in its holster
                        sm.atlas.Limb(d + side + Vector3.up * 0.85f, d + side * 1.35f + Vector3.up * 0.25f, 0.022f, 0.022f, 5, S(dark), false);   // the hose's loop
                        sm.atlas.Limb(d + side * 1.35f + Vector3.up * 0.25f, d + side + Vector3.up * 1.4f, 0.022f, 0.022f, 5, S(dark), false);
                    }
                }
            }
            // ---- the office at the back, the air pump, a water drum
            Vector3 ob = P(5.6f, 0f, 11.3f);
            sm.atlas.Box(ob + Vector3.up * 1.5f, new Vector3(4.6f, 3.0f, 3.2f), q, S(white));
            sm.atlas.Box(ob + Vector3.up * 3.08f, new Vector3(5.0f, 0.16f, 3.6f), q, S(brandPal));
            sm.atlas.Box(ob + Vector3.up * 2.72f - Z * 1.61f, new Vector3(4.6f, 0.5f, 0.02f), q, S(stripePal));
            sm.atlas.Box(ob + Vector3.up * 1.05f - Z * 1.61f - A * 1.2f, new Vector3(0.95f, 2.1f, 0.03f), q, S(Pal.WoodDark));      // the door
            sm.atlas.Box(ob + Vector3.up * 1.55f - Z * 1.61f + A * 0.8f, new Vector3(1.8f, 1.1f, 0.03f), q, S(Pal.WindowGlow));     // the window, lit
            sm.col.Box(ob + Vector3.up * 1.5f, new Vector3(4.6f, 3.0f, 3.2f), q, Vector2.zero);
            Glyph3D.Write(sm.loose, "OFFICE", Glyph3D.Face.Block, ob + Vector3.up * 2.72f - Z * 1.64f, -Z, 0.24f, 0.015f, 3.5f, S(dark), S(grey));
            Vector3 air = P(-7.6f, 0f, 9.5f);
            sm.atlas.Box(air + Vector3.up * 0.6f, new Vector3(0.35f, 1.2f, 0.3f), q, S(Pal.BoatRed));
            sm.atlas.Cylinder(air + Vector3.up * 1.2f - Z * 0.16f, 0.12f, 0.12f, 0.04f, 10, S(white), true);
            sm.atlas.Limb(air + Vector3.up * 0.9f + A * 0.18f, air + Vector3.up * 0.2f + A * 0.45f, 0.02f, 0.02f, 5, S(dark), false);
            Glyph3D.Write(sm.loose, "AIR", Glyph3D.Face.Block, air + Vector3.up * 1.45f - Z * 0.16f, -Z, 0.14f, 0.01f, 0.5f, S(white), S(grey));
            sm.col.Box(air + Vector3.up * 0.6f, new Vector3(0.4f, 1.2f, 0.35f), q, Vector2.zero);
            Vector3 drum = P(-7.6f, 0f, 11.2f);
            sm.atlas.Cylinder(drum, 0.32f, 0.32f, 0.9f, 10, S(Pal.BoatBlue), true);
            // ---- the price totem by the road
            Vector3 tot = P(-8.2f, 0f, 1.4f);
            float yaw = Quaternion.LookRotation(-Z).eulerAngles.y;
            sm.atlas.Box(tot + Vector3.up * 2.2f, new Vector3(1.5f, 4.4f, 0.4f), q, S(brandPal));
            sm.col.Box(tot + Vector3.up * 2.2f, new Vector3(1.5f, 4.4f, 0.4f), q, Vector2.zero);
            Board(sm, tot + Vector3.up * 3.55f - Z * 0.21f, yaw, 1.3f, 1.2f, brandPal, new[] { PumpBrand, "FUELS" }, new[] { 0.3f, 0.2f }, white, brandPal, y0, Glyph3D.Face.Block);
            Board(sm, tot + Vector3.up * 1.75f - Z * 0.21f, yaw, 1.3f, 1.4f, Pal.IronBlack,
                  new[] { "PETROL  100.00", "DIESEL  100.00", "Rs / LITRE" }, new[] { 0.16f, 0.16f, 0.11f }, Pal.LampBulb, Pal.IronBlack, y0, Glyph3D.Face.Block);

            string title = "Himgiri Fuels, " + place;
            sm.Emit(ctx, title, parent);

            // the forecourt the cars fill up in, a place name, a map blip, two lamps under the canopy
            var go = new GameObject("FuelStation " + place);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(P(0f, 0f, CourtD * 0.5f), q);
            var fs = go.AddComponent<FuelStation>();
            fs.brand = "Himgiri";
            fs.place = place;
            fs.title = title;
            fs.size = new Vector3(CourtW, 7f, CourtD + 3f);
            ctx.Zone(title, P(0f, 0f, CourtD * 0.5f), 16f);   // the smallest circle names the place: Tallital's "Mall Road" (22 m) won over 28 m
            ctx.keepClear.Add((new Vector2(o.x, o.y) + away * (CourtD * 0.5f), CourtW * 0.5f + 1f));   // no tree through the canopy
            ctx.landmarkSites[title] = P(0f, 0f, CourtD * 0.5f);
            ctx.lampHeads.Add(P(-2.6f, ch - 0.15f, 6.2f));
            ctx.lampHeads.Add(P(2.6f, ch - 0.15f, 6.2f));
            Debug.Log($"[Nainital] petrol pump {place}: forecourt at ({o.x:F1}, {o.y:F1}) facing ({away.x:F2}, {away.y:F2}), level {y0:F1}");
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Shops and restaurants the player can use (owner, 21 Nov 2026, FREEROAM #5: "10-15 interactable shops, each its own
    /// goods ... fake shop names at the real places - Mall Road, Bara Bazaar, Tibetan Market"; "restaurants 5-6, people at
    /// the tables eating"). Runs after the town is built (WorldBuilder step "Shops and restaurants"): every shop takes the
    /// built shop front (ctx.shopFronts) nearest its spot - along Upper Mall Road, in Bada Bazar, in the Tibetan Market -
    /// and gets a Shop (Runtime/Interaction/Shop.cs) on the pavement in front, with a small standing board of its name.
    /// Each restaurant gets an open sit-out beside its spot (real eateries' places): levelled, paved (grey pavers painted
    /// into the terrain), three tables with two chairs each under umbrellas, a counter with the name board; the chairs are
    /// RestaurantSeats, the counter is the kitchen the waiter walks out of (Runtime/Interaction/Restaurant.cs).
    /// All names are invented.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        // name (Loc key), the area, where along it (Mall: 0..1 along Upper Mall Road south to north; else metres off the
        // area's POI), the goods (ShopCatalog ids)
        static readonly (string name, string area, Vector2 at, string[] items)[] ShopDefs =
        {
            ("Naini Woollens Emporium", "Mall Road", new Vector2(0.18f, 0f), new[] { "wool_shawl", "wool_sweater", "wool_muffler", "wool_gloves", "wool_socks", "wool_blanket" }),
            ("Himalaya Candle House", "Mall Road", new Vector2(0.29f, 0f), new[] { "candle_rose", "candle_jar", "candle_lake", "candle_set" }),
            ("Kumaon Kapda Ghar", "Mall Road", new Vector2(0.40f, 0f), new[] { "outfit_maroon", "outfit_mustard", "outfit_olive", "outfit_winter", "outfit_default" }),
            ("Barsaat Chhaata Ghar", "Mall Road", new Vector2(0.50f, 0f), new[] { "umbrella_black", "umbrella_rainbow", "umbrella_raincoat", "bag_backpack" }),
            ("Sukoon Medicos", "Mall Road", new Vector2(0.60f, 0f), new[] { "med_firstaid", "med_balm", "med_vicks", "now_painkiller", "now_ors" }),
            ("Lake View Book Depot", "Mall Road", new Vector2(0.70f, 0f), new[] { "book_corbett", "book_trek", "book_comics", "book_diary" }),
            ("Pahadi Bakers", "Mall Road", new Vector2(0.80f, 0f), new[] { "bakery_plumcake", "bakery_cookies", "bakery_bread", "now_patties", "now_pastry" }),
            ("Fit Pahad Sports", "Mall Road", new Vector2(0.90f, 0f), new[] { "gym_dumbbells", "gym_mat", "gym_shoes", "gym_bottle" }),
            ("Shri Bal Mithai Bhandar", "Bada Bazar", new Vector2(-10f, 8f), new[] { "sweet_balmithai", "sweet_singori", "now_balmithai", "now_jalebi" }),
            ("Naini Mobile Point", "Bada Bazar", new Vector2(12f, -6f), new[] { "svc_recharge_small", "svc_recharge_big", "gadget_earphones", "gadget_powerbank", "gadget_cover" }),
            ("Taaza Phal Bhandar", "Bada Bazar", new Vector2(2f, 18f), new[] { "fruit_basket", "fruit_apples", "now_banana", "now_peach" }),
            ("Tashi Souvenirs and Toys", "Tibetan Market", new Vector2(-6f, 4f), new[] { "toy_monkey", "souv_boat", "souv_magnet", "souv_snowglobe", "toy_ropeway", "souv_bell" }),
            ("Dolma Jacket Store", "Tibetan Market", new Vector2(8f, -4f), new[] { "outfit_pahadi", "outfit_winter", "outfit_maroon", "wool_muffler", "wool_gloves" }),
        };

        // name, the spot (local metres), the menu
        static readonly (string name, Vector2 at, string[] menu)[] RestaurantDefs =
        {
            ("Tashi Momo Corner", new Vector2(-122f, -92f), new[] { "meal_momos", "meal_thukpa", "meal_maggi", "meal_chai", "meal_softy" }),
            ("Pahadi Rasoi", new Vector2(845f, -844f), new[] { "meal_thali", "meal_bhatt", "meal_rajma", "meal_paratha", "meal_chai" }),
            ("Bazaar Bhature Wala", new Vector2(0f, 0f), new[] { "meal_chole", "meal_paratha", "meal_pakode", "meal_chai", "meal_softy" }),   // (0,0): beside Bada Bazar
            ("Lake Breeze Cafe", new Vector2(640f, -350f),   // on Upper Mall Road by the lake (Chandni Chowk and Sonam Fast Food are real eateries in the map data)
              new[] { "meal_coffee", "meal_sandwich", "meal_pizza", "meal_softy", "meal_chai" }),
            ("Barf Pizza Point", new Vector2(706f, 582f), new[] { "meal_pizza", "meal_maggi", "meal_coffee", "meal_momos", "meal_chai" }),
        };

        internal static void BuildShopsAndRestaurants(WorldCtx ctx)
        {
            Physics.SyncTransforms();
            var parent = new GameObject("Shops and restaurants").transform;
            parent.SetParent(ctx.worldRoot, false);
            var sm = new SiteMeshes();

            // ---- shops on built shop fronts
            var used = new List<Vector3>();
            int shops = 0;
            Vector2? bazar = ctx.Poi("Bada Bazar"), tibet = ctx.Poi("Tibetan Market");
            foreach (var d in ShopDefs)
            {
                Vector3 want;
                if (d.area == "Mall Road")
                {
                    var drive = ctx.mallUpperDrive;
                    if (drive == null || drive.Count < 2) continue;
                    want = drive[Mathf.Clamp(Mathf.RoundToInt(d.at.x * (drive.Count - 1)), 0, drive.Count - 1)];
                }
                else
                {
                    var poi = d.area == "Bada Bazar" ? bazar : tibet;
                    if (!poi.HasValue) { Debug.LogWarning("[Nainital] shops: no POI " + d.area); continue; }
                    want = V3(poi.Value + d.at, 0f);
                }
                int best = -1;
                float bd = 90f * 90f;   // 60 m left the Tibetan Market one without a front once a sit-out took a building (22 Nov)
                for (int i = 0; i < ctx.shopFronts.Count; i++)
                {
                    var f = ctx.shopFronts[i];
                    float dd = (new Vector2(f.pos.x, f.pos.z) - new Vector2(want.x, want.z)).sqrMagnitude;
                    if (dd >= bd) continue;
                    bool near = false;
                    foreach (var u in used) if ((u - f.pos).sqrMagnitude < 7f * 7f) { near = true; break; }
                    if (near) continue;
                    bd = dd; best = i;
                }
                if (best < 0) { Debug.LogWarning($"[Nainital] shops: no free shop front near {d.name}"); continue; }
                var front = ctx.shopFronts[best];
                used.Add(front.pos);
                Vector3 n = new Vector3(front.normal.x, 0f, front.normal.z).normalized;
                Vector3 at = front.pos + n * 1.3f;
                at.y = GroundAt(ctx, at);
                var go = new GameObject("Shop " + d.name);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(at + Vector3.up * 0.9f, Quaternion.LookRotation(-n));
                var shop = go.AddComponent<Shop>();
                shop.shopName = d.name;
                shop.place = d.area;
                shop.items = d.items;
                shop.radius = 2.6f;
                // a small A-board with the name on the pavement, to the side of the door
                Vector3 side = Vector3.Cross(Vector3.up, n) * (front.width * 0.5f - 0.4f);
                Vector3 bp = front.pos + n * 0.9f + side;
                float gy = GroundAt(ctx, bp);
                float yaw = Quaternion.LookRotation(n).eulerAngles.y;
                Board(sm, new Vector3(bp.x, gy + 1.05f, bp.z), yaw, 1.1f, 0.55f, Pal.IronBlack, new[] { d.name.ToUpperInvariant() }, new[] { 0.09f }, Pal.LampBulb, Pal.WoodDark, gy, Glyph3D.Face.Block);
                shops++;
            }

            // ---- restaurants: open sit-outs
            int rests = 0;
            foreach (var r in RestaurantDefs)
                if (BuildRestaurant(ctx, parent, sm, r.name, RestaurantSpot(ctx, r.at), r.menu)) rests++;
            sm.Emit(ctx, "Shops and restaurants (boards, tables)", parent);
            Debug.Log($"[Nainital] shops: {shops} of {ShopDefs.Length} on shop fronts; restaurants: {rests} of {RestaurantDefs.Length}");
        }

        static float GroundAt(WorldCtx ctx, Vector3 p)
        {
            float top = ctx.H(new Vector2(p.x, p.z));
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, top) + 4f, p.z), Vector3.down, out var hit, 12f,
                    ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f)
                return hit.point.y;
            return top;
        }

        const float SitW = 9f, SitD = 7f;

        static Vector2 RestaurantSpot(WorldCtx ctx, Vector2 at)
        {
            if (at == Vector2.zero) { var bazar = ctx.Poi("Bada Bazar"); if (bazar.HasValue) return bazar.Value + new Vector2(-14f, -10f); }
            return at;
        }

        // planned before the houses (step "Pump and sit-out plans"): the sit-out's middle, the road's direction, the way away
        // from the road, the road-edge point whose height is its level; the houses skip it (keepClear). The first build
        // (22 Nov) found no free patch in Bada Bazar or by the lake at Lake Breeze (houses everywhere) and put the other three
        // in back yards and an empty field: now a dhaba-style sit-out right on the road's edge, like the pumps.
        static readonly Dictionary<string, (Vector2 o, Vector2 along, Vector2 away, Vector2 level)> sitPlans = new Dictionary<string, (Vector2, Vector2, Vector2, Vector2)>();

        internal static void PlanRestaurants(WorldCtx ctx)
        {
            sitPlans.Clear();
            foreach (var r in RestaurantDefs)
            {
                Vector2 spot = RestaurantSpot(ctx, r.at);
                var why = new Dictionary<string, int>();
                if (!SitSpot(ctx, spot, false, why, out Vector2 o, out Vector2 along, out Vector2 away, out Vector2 level))
                {
                    Debug.LogWarning($"[Nainital] restaurant {r.name}: nothing to plan near ({spot.x:F0}, {spot.y:F0}) - {Why(why)}");
                    continue;
                }
                sitPlans[r.name] = (o, along, away, level);
                ctx.keepClear.Add((o, new Vector2(SitW, SitD).magnitude * 0.5f + 2f));   // the corners too
                Debug.Log($"[Nainital] restaurant {r.name}: planned at ({o.x:F1}, {o.y:F1}), {Vector2.Distance(o, spot):F1} m from its spot");
            }
        }

        /// <summary>A free, fairly level patch on the edge of the road nearest the spot (a motor road or Upper Mall Road), on
        /// the spot's side first, slid along the road up to 36 m and back from it up to 3 m; with no road within 60 m (Snow
        /// View's hilltop) an open patch within 40 m. colliders: also test what is built (false while planning, before the
        /// houses: then the other plans count).</summary>
        static bool SitSpot(WorldCtx ctx, Vector2 spot, bool colliders, Dictionary<string, int> why, out Vector2 o, out Vector2 along, out Vector2 away, out Vector2 level)
        {
            o = along = away = level = Vector2.zero;
            if (!WorldBuilder.NearestEateryRoad(ctx, spot, 60f, out Vector2 best, out Vector2 tan))
            {
                // no road: an open, fairly level patch, its counter towards the nearest road. Not planned: the hilltop's own
                // plan (Snow View) covers it; found once the hill is built, as in the first build
                if (!colliders) { Count(why, "no road (found at build time)"); return false; }
                Vector2 toward = WorldBuilder.NearestMotorRoad(ctx, spot, 400f, out Vector2 far, out _) ? (far - spot).normalized : Vector2.down;
                away = -toward;
                along = new Vector2(away.y, -away.x);
                for (float r = 0f; r <= 40f; r += 3f)
                    for (int k = 0; k < (r == 0f ? 1 : 12); k++)
                    {
                        float a = k * Mathf.PI * 2f / 12f;
                        Vector2 c = spot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        if (SitBlocked(ctx, c, along, away, colliders, why, out float lo0, out float hi0)) continue;
                        if (hi0 - lo0 > 2.2f) { Count(why, "slope"); continue; }
                        o = c;
                        level = c;
                        return true;
                    }
                return false;
            }
            tan = tan.normalized;
            Vector2 n = new Vector2(-tan.y, tan.x);
            float spotSide = Vector2.Dot(spot - best, n);
            var road = NearestRoadData(ctx, best);
            float half = road != null ? road.width * 0.5f : 3f;
            var sides = Mathf.Abs(spotSide) < 2f ? new[] { 1f, -1f } : new[] { Mathf.Sign(spotSide), -Mathf.Sign(spotSide) };
            foreach (float side in sides)
                for (int k = 0; k <= 24; k++)
                {
                    float s = (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * 3f;   // 0, +3, -3 ... 36
                    foreach (float back in new[] { 0.6f, 1.6f, 3f })
                    {
                        if (!WorldBuilder.NearestEateryRoad(ctx, best + tan * s, 15f, out Vector2 b2, out Vector2 t2)) continue;
                        t2 = t2.normalized;
                        if (Vector2.Dot(t2, tan) < 0f) t2 = -t2;
                        Vector2 n2 = new Vector2(-t2.y, t2.x) * side;
                        Vector2 al = new Vector2(n2.y, -n2.x);
                        Vector2 c = b2 + n2 * (half + back + SitD * 0.5f);
                        if (SitBlocked(ctx, c, al, n2, colliders, why, out float lo, out float hi)) continue;
                        if (hi - lo > 4f) { Count(why, "slope"); continue; }   // the ground is eased to the road's level (ShapeTerrain below)
                        o = c;
                        along = al;
                        away = n2;
                        level = b2 + n2 * (half - 0.3f);
                        return true;
                    }
                }
            return false;
        }

        static bool BuildRestaurant(WorldCtx ctx, Transform parent, SiteMeshes sm, string name, Vector2 spot, string[] menu)
        {
            Vector2 o = Vector2.zero, along = Vector2.zero, away = Vector2.zero, level = Vector2.zero;
            var why = new Dictionary<string, int>();
            bool found = false;
            if (sitPlans.TryGetValue(name, out var plan))
            {
                // the planned patch, tested again now that the houses and landmarks stand
                if (!SitBlocked(ctx, plan.o, plan.along, plan.away, true, why, out _, out _)) { (o, along, away, level) = plan; found = true; }
                else Debug.LogWarning($"[Nainital] restaurant {name}: the planned patch is taken ({Why(why)}), looking again");
            }
            if (!found)
            {
                why.Clear();
                found = SitSpot(ctx, spot, true, why, out o, out along, out away, out level);
            }
            if (!found) { Debug.LogWarning($"[Nainital] restaurant {name}: no free patch near ({spot.x:F0}, {spot.y:F0}) - {Why(why)}"); return false; }
            float y0 = ctx.H(level);   // level with the road it opens on

            // level it and pave it (grey pavers into the terrain)
            const float ease = 5f;
            float R = Mathf.Sqrt(SitW * SitW + SitD * SitD) * 0.5f + ease;
            ShapeTerrain(ctx, o - Vector2.one * R, o + Vector2.one * R, (p, y) =>
            {
                Vector2 d = p - o;
                if (Vector2.Dot(d, away) < -SitD * 0.5f - 0.3f) return y;   // never touch the road side
                float lx = Mathf.Abs(Vector2.Dot(d, along)) - SitW * 0.5f, lz = Mathf.Abs(Vector2.Dot(d, away)) - SitD * 0.5f;
                float outd = Mathf.Sqrt(Mathf.Max(0f, lx) * Mathf.Max(0f, lx) + Mathf.Max(0f, lz) * Mathf.Max(0f, lz));
                float k = Mathf.Clamp01(outd / ease);
                k = k * k * (3f - 2f * k);
                return Mathf.Lerp(y0, y, k);
            });
            Vector3 A = new Vector3(along.x, 0f, along.y), Z = new Vector3(away.x, 0f, away.y);
            var q = Quaternion.LookRotation(Z);
            Vector3 P(float x, float y, float z) => new Vector3(o.x, y0, o.y) + A * x + Vector3.up * y + Z * z;
            var paint = new MeshBuilder();
            paint.Box(P(0f, 0f, 0f), new Vector3(SitW, 0.02f, SitD), q, S(Pal.Concrete));
            WorldBuilder.PaintRoadSurface(paint, 2);

            var root = new GameObject("Restaurant " + name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(P(0f, 0f, 0f), q);
            var rest = root.AddComponent<Restaurant>();
            rest.title = name;
            rest.menu = menu;

            // the counter at the back, the kitchen door, the name board over it
            Vector3 cb = P(2.6f, 0f, SitD * 0.5f - 0.8f);
            sm.atlas.Box(cb + Vector3.up * 0.55f, new Vector3(2.6f, 1.1f, 0.8f), q, S(Pal.WoodDark));
            sm.atlas.Box(cb + Vector3.up * 1.13f, new Vector3(2.7f, 0.06f, 0.9f), q, S(Pal.ShirtWhite));
            sm.atlas.Box(cb + Vector3.up * 1.32f + A * -0.7f, new Vector3(0.5f, 0.32f, 0.35f), q, Atlas.ExtraUV(Px.Chrome));
            sm.atlas.Box(cb + Vector3.up * 1.25f + A * 0.6f, new Vector3(0.35f, 0.18f, 0.35f), q, S(Pal.JacketRed));
            sm.col.Box(cb + Vector3.up * 0.55f, new Vector3(2.6f, 1.1f, 0.8f), q, Vector2.zero);
            foreach (float px in new[] { -1.4f, 1.4f })
            {
                sm.atlas.Box(cb + A * px + Z * 0.3f + Vector3.up * 1.35f, new Vector3(0.08f, 2.7f, 0.08f), q, S(Pal.WoodDark));
            }
            sm.atlas.Box(cb + Z * 0.3f + Vector3.up * 2.75f, new Vector3(3.2f, 0.08f, 1.6f), q, S(Pal.Awning));   // the counter's awning
            float yaw = Quaternion.LookRotation(-Z).eulerAngles.y;
            Board(sm, cb + Z * -0.42f + Vector3.up * 2.25f, yaw, 2.6f, 0.62f, Pal.BoatRed, new[] { name.ToUpperInvariant() }, new[] { 0.2f }, Pal.ShirtWhite, Pal.WoodDark, y0, Glyph3D.Face.Block);
            var kitchen = new GameObject("Kitchen").transform;
            kitchen.SetParent(root.transform, false);
            kitchen.position = cb + Z * -0.8f + A * -1.6f + Vector3.up * 0.05f;
            rest.kitchen = kitchen;

            // three tables, two chairs each, an umbrella over each table
            var seats = new List<RestaurantSeat>();
            Vector3[] tables = { P(-2.8f, 0f, -1.2f), P(0.0f, 0f, 0.4f), P(-2.6f, 0f, 1.9f) };
            int[] umbrellaPal = { Pal.BoatRed, Pal.BoatGreen, Pal.BoatBlue };
            for (int t = 0; t < tables.Length; t++)
            {
                Vector3 tc = tables[t];
                sm.atlas.Cylinder(tc, 0.07f, 0.07f, 0.72f, 8, S(Pal.IronBlack), true);
                sm.atlas.Cylinder(tc + Vector3.up * 0.72f, 0.48f, 0.48f, 0.04f, 16, S(Pal.ShirtWhite), true);
                sm.col.Box(tc + Vector3.up * 0.38f, new Vector3(0.9f, 0.76f, 0.9f), q, Vector2.zero);
                // the umbrella
                sm.atlas.Cylinder(tc + Vector3.up * 0.76f, 0.025f, 0.025f, 1.6f, 6, S(Pal.WoodLight), true);
                sm.atlas.Cylinder(tc + Vector3.up * 2.25f, 1.15f, 0.06f, 0.42f, 10, S(umbrellaPal[t]), true);
                // a cup and a plate already on it
                sm.loose.Cylinder(tc + Vector3.up * 0.76f + A * 0.18f, 0.035f, 0.03f, 0.08f, 8, S(Pal.ShirtWhite), true);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 cpos = tc + A * (side * 0.82f);
                    var cq = Quaternion.LookRotation(-A * side);   // facing the table
                    sm.atlas.Box(cpos + Vector3.up * 0.45f, new Vector3(0.42f, 0.05f, 0.42f), cq, S(Pal.WoodLight));
                    sm.atlas.Box(cpos + Vector3.up * 0.7f + cq * new Vector3(0f, 0f, -0.2f), new Vector3(0.42f, 0.5f, 0.04f), cq, S(Pal.WoodLight));
                    foreach (var lx in new[] { -0.17f, 0.17f })
                        foreach (var lz in new[] { -0.17f, 0.17f })
                            sm.atlas.Box(cpos + cq * new Vector3(lx, 0.22f, lz), new Vector3(0.035f, 0.44f, 0.035f), cq, S(Pal.WoodDark));
                    var sgo = new GameObject("Seat " + (seats.Count + 1));
                    sgo.transform.SetParent(root.transform, false);
                    sgo.transform.SetPositionAndRotation(cpos + Vector3.up * 0.05f, cq);
                    var seat = sgo.AddComponent<RestaurantSeat>();
                    seat.restaurant = rest;
                    seat.index = seats.Count;
                    seat.radius = 1.2f;
                    var tableT = new GameObject("Table").transform;
                    tableT.SetParent(sgo.transform, false);
                    tableT.position = tc + Vector3.up * 0.76f;
                    seat.table = tableT;
                    seats.Add(seat);
                }
            }
            rest.seats = seats.ToArray();
            ctx.Zone(name, P(0f, 0f, 0f), 18f);
            ctx.keepClear.Add((o, SitW * 0.5f + 1f));   // no tree among the tables
            ctx.landmarkSites[name] = P(0f, 0f, 0f);
            ctx.lampHeads.Add(cb + Z * 0.3f + Vector3.up * 2.6f);
            Debug.Log($"[Nainital] restaurant {name}: at ({o.x:F1}, {o.y:F1}), level {y0:F1}, {seats.Count} seats");
            return true;
        }

        /// <summary>The sit-out's patch holds a road, the lake, a house, a wall, a pole (the ground itself does not count);
        /// lo / hi: the ground's range over it.</summary>
        static bool SitBlocked(WorldCtx ctx, Vector2 c, Vector2 along, Vector2 away, bool colliders, Dictionary<string, int> why, out float lo, out float hi)
        {
            lo = float.MaxValue; hi = float.MinValue;
            for (float x = -SitW * 0.5f; x <= SitW * 0.5f + 0.01f; x += 1.5f)
                for (float z = -SitD * 0.5f; z <= SitD * 0.5f + 0.01f; z += 1.5f)
                {
                    Vector2 p = c + along * x + away * z;
                    if (ctx.InLake(p) || ctx.LakeDistance(p) < 2f) { Count(why, "lake"); return true; }
                    if (!colliders && ctx.InKeepClear(p)) { Count(why, "another plan"); return true; }
                    float h = ctx.H(p);
                    lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
                    foreach (var r in ctx.world.roads)
                    {
                        if (r.kind == "track") continue;
                        var line = Geo.ToPoints(r.pts);
                        if (line.Length < 2) continue;
                        if (Geo.DistanceToPolyline(line, p, false) < r.width * 0.5f + 0.4f) { Count(why, "road " + r.kind); return true; }
                    }
                }
            if (!colliders) return false;
            float y = ctx.H(c);
            var q = Quaternion.LookRotation(new Vector3(away.x, 0f, away.y));
            var hits = Physics.OverlapBox(new Vector3(c.x, y + 2.2f, c.y), new Vector3(SitW * 0.5f, 2f, SitD * 0.5f), q, ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore);
            foreach (var h in hits) if (!(h is TerrainCollider)) { Count(why, "built " + h.name); return true; }
            return false;
        }
    }
}

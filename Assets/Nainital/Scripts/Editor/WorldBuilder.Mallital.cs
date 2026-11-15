using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Mallital from the user's Street View and photo spheres: the Capitol Cinema facade facing the children's
    /// park, tyre play things in the park, Naina Devi's lavender-pillared golden gate opening onto the stone plaza of the
    /// Tibetan (Bhotia) market, the stall row, the boat-point ghat with moored boats, a lakeside bandstand and the police
    /// booth where Mall Road reaches Mallital. Positions are POIs from process_data.py (MANUAL_POIS) or OSM.</summary>
    public static partial class WorldBuilder
    {
        static readonly (float u, float v)[] BoxProbe =
            { (-0.5f, -0.5f), (0.5f, -0.5f), (-0.5f, 0.5f), (0.5f, 0.5f), (-0.5f, 0f), (0.5f, 0f), (0f, -0.5f), (0f, 0.5f) };

        static (Vector2 c, Vector2 front, float w, float d)? CapitolBox(WorldCtx ctx)
        {
            foreach (var b in ctx.world.osmBuildings)
            {
                if (string.IsNullOrEmpty(b.name) || b.name.IndexOf("Capitol", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var poly = Geo.ToPoints(b.pts);
                if (poly.Length < 3) return null;
                Vector2 c = Vector2.zero;
                foreach (var p in poly) c += p;
                c /= poly.Length;
                float bestA = float.MaxValue, bw = 0f, bd = 0f, bang = 0f;
                Vector2 mid = c;
                for (int a = 0; a < 90; a++)
                {
                    Vector2 u = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)), v = new Vector2(-u.y, u.x);
                    float u0 = 1e9f, u1 = -1e9f, v0 = 1e9f, v1 = -1e9f;
                    foreach (var p in poly) { float pu = Vector2.Dot(p - c, u), pv = Vector2.Dot(p - c, v); u0 = Mathf.Min(u0, pu); u1 = Mathf.Max(u1, pu); v0 = Mathf.Min(v0, pv); v1 = Mathf.Max(v1, pv); }
                    if ((u1 - u0) * (v1 - v0) < bestA) { bestA = (u1 - u0) * (v1 - v0); bw = u1 - u0; bd = v1 - v0; bang = a; mid = c + u * ((u0 + u1) * 0.5f) + v * ((v0 + v1) * 0.5f); }
                }
                c = mid;
                Vector2 ux = new Vector2(Mathf.Cos(bang * Mathf.Deg2Rad), Mathf.Sin(bang * Mathf.Deg2Rad)), vx = new Vector2(-ux.y, ux.x);
                // the front looks at the children's park
                var park = ctx.Poi("Children's Park (Capitol)");
                Vector2 toPark = park.HasValue ? (park.Value - c).normalized : Vector2.up;
                Vector2[] dirs = { ux, -ux, vx, -vx };
                Vector2 front = dirs[0];
                foreach (var dd in dirs) if (Vector2.Dot(dd, toPark) > Vector2.Dot(front, toPark)) front = dd;
                bool frontIsU = Mathf.Abs(Vector2.Dot(front, ux)) > 0.5f;
                float width = Mathf.Min(frontIsU ? bd : bw, 40f), depth = Mathf.Min(frontIsU ? bw : bd, 40f);
                // off the walk (user, 2026-11-16 screenshot: "capitol waali building road me kyun ghusri"): the OSM outline
                // reaches 1.3 m into the 5.5 m walk to Naina Devi. The building stands clear of every road by 1.65 m, room for the
                // retaining wall at the walk's edge (WorldBuilder.CapitolGround.cs)
                Vector2 side = new Vector2(front.y, -front.x);
                for (int pass = 0; pass < 3; pass++)
                {
                    float worst = 0f; Vector2 push = Vector2.zero;
                    foreach (var r in ctx.world.roads)
                    {
                        if (NotRoads.Contains(r.kind)) continue;
                        var line = Geo.ToPoints(r.pts);
                        foreach (var (u, v) in BoxProbe)   // the corners and the middles of the sides
                        {
                            Vector2 q = c + side * (u * width) + front * (v * depth);
                            float dl = Geo.DistanceToPolyline(line, q, false);
                            float need = r.width * 0.5f + 1.65f - dl;   // the 1.5 m retaining wall and a gap
                            if (need > worst && NearestRoadPoint(ctx, q, dl + 0.5f, rr => rr == r, out var rp, out _)) { worst = need; push = (q - rp).normalized * need; }
                        }
                    }
                    if (worst <= 0.01f) break;
                    c += push;
                }
                return (c, front, width, depth);   // the real footprint: 20 m front, 35 m deep
            }
            return null;
        }

        static void PlanMallital(WorldCtx ctx)
        {
            var cap = CapitolBox(ctx);
            if (cap.HasValue) ctx.keepClear.Add((cap.Value.c, Mathf.Max(cap.Value.w, cap.Value.d) * 0.5f + 1f));
            KeepVideoGamesClear(ctx);   // the hall beside it (WorldBuilder.Capitol.cs)
            foreach (var (name, r) in new[] { ("Tibetan Market", 18f), ("Naina Devi Gate", 9f), ("Mallital Police Booth", 3f), ("Bandstand", 6f), ("Children's Park (Capitol)", 10f) })
            {
                var p = ctx.Poi(name);
                if (p.HasValue) ctx.keepClear.Add((p.Value, r));
            }
            foreach (var spot in PlacedSpots(ctx)) ctx.keepClear.Add((spot, 11f));   // the Shaktipeeth gate and anything else placed by hand
            if (MasjidSite(ctx, out var masjid, out _)) ctx.keepClear.Add((masjid, 20f));
            foreach (var spot in LandmarkBuilder.SnowViewSpots()) ctx.keepClear.Add(spot);   // the Snow View hilltop
            foreach (var spot in LandmarkBuilder.BoatClubSpots()) ctx.keepClear.Add(spot);   // the Boat House Club
            foreach (var (name, r) in new[] { ("Naina Devi", 20f), ("Gurudwara", 15f), ("Church", 16f) })
            {
                var p = ctx.Poi(name);
                if (p.HasValue) ctx.keepClear.Add((p.Value, r));
            }
        }

        static void BuildMallital(WorldCtx ctx)
        {
            var parent = new GameObject("Mallital").transform;
            parent.SetParent(ctx.worldRoot, false);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            var notes = new List<string>();
            var flatsC = ctx.Poi("Nainital Stadium");
            // no blanket of paving round the Flats any more (user, 2026-11-12: "aisi hai hi nahi real mein"): the roads,
            // the plazas and the entry garden are paved, the rest is the town ground
            var bazaarC = ctx.Poi("Bada Bazar");
            if (bazaarC.HasValue) PaveZone(ctx, parent, bazaarC.Value, 115f, "Bada Bazar ground");

            var cap = CapitolBox(ctx);
            if (cap.HasValue)
            {
                CapitolCinema(ctx, parent, mb, col, font, cap.Value.c, cap.Value.front, cap.Value.w, cap.Value.d);
                VideoGamesHall(ctx, parent, mb, col, font, cap.Value.c, cap.Value.front, cap.Value.w, cap.Value.d);
                notes.Add("Capitol");
                // the boat stand below the hall (user, 2026-11-16: "wahan boat stand bhi hai"; the satellite photo shows the
                // rowing boats moored off the shore there)
                int bv = 0; float bbd = float.MaxValue;
                var boatAt = new Vector2(34f, -60f);
                for (int i = 0; i < ctx.lake.Length; i++) { float dd = (ctx.lake[i] - boatAt).sqrMagnitude; if (dd < bbd) { bbd = dd; bv = i; } }
                if (bbd < 30f * 30f) { BuildJetty(ctx, parent, bv, "Capitol Boat Stand"); notes.Add("Capitol boat stand"); }
            }
            var park = ctx.Poi("Children's Park (Capitol)");
            if (park.HasValue) { TyrePark(ctx, mb, col, park.Value); notes.Add("park"); }
            var temple = ctx.Poi("Naina Devi Temple");
            var gate = ctx.Poi("Naina Devi Gate");
            var plaza = ctx.Poi("Tibetan Market");
            if (gate.HasValue && plaza.HasValue)
            {
                Vector2 face = (plaza.Value - gate.Value).normalized;
                TempleGate(ctx, parent, mb, col, font, gate.Value, face);
                notes.Add("Naina Devi gate");
            }
            if (plaza.HasValue) { TibetanPlaza(ctx, parent, plaza.Value, temple ?? plaza.Value + Vector2.right * 30f); notes.Add("Tibetan market"); }
            // the police chowki and the Shaktipeeth gate on Mall Road are one prefab placed by hand (WorldBuilder.PlacedByHand.cs)
            var band = ctx.Poi("Bandstand");
            if (band.HasValue) { Bandstand(ctx, mb, col, band.Value); notes.Add("bandstand"); }
            var boats = ctx.Poi("Mallital Boat Point");
            if (boats.HasValue) { BoatGhat(ctx, parent, mb, col, boats.Value); notes.Add("ghat"); }
            MallitalEntry(ctx, parent, mb, col, font);
            ctx.MeshObject("Mallital (built)", parent, mb, ctx.mats.atlas, col);

            void Marker(string name, Vector2 at, Vector2 look, float lookUp)
            {
                var m = new GameObject(name);
                m.transform.SetParent(parent, false);
                Vector3 p = V(at, ctx.H(at) + 1.7f);
                m.transform.SetPositionAndRotation(p, Quaternion.LookRotation(V(look, ctx.H(look) + lookUp) - p));
            }
            if (cap.HasValue) Marker("MallitalShotCapitol", cap.Value.c + cap.Value.front * (cap.Value.d * 0.5f + 22f), cap.Value.c, 6f);
            if (gate.HasValue && plaza.HasValue) Marker("MallitalShotGate", plaza.Value, gate.Value, 3f);
            if (plaza.HasValue && temple.HasValue)
            {
                Vector2 away = (plaza.Value - temple.Value).normalized, along = new Vector2(away.y, -away.x);
                Marker("MallitalShotPlaza", plaza.Value - away * 4f + along * 13f, plaza.Value + away * 9f - along * 8f, 1.6f);   // looking down the stall lane
            }
            var sv1 = new Vector2(87.5f, 117.2f);   // the user's Street View at the Mallital end of Mall Road, heading 195 deg
            Marker("MallitalShotMallEnd", sv1, sv1 + new Vector2(Mathf.Sin(194.7f * Mathf.Deg2Rad), Mathf.Cos(194.7f * Mathf.Deg2Rad)) * 40f, 2f);
            if (boats.HasValue) Marker("MallitalShotGhat", boats.Value + new Vector2(8f, 14f), boats.Value - OutwardAt(ctx, boats.Value) * 10f, 0f);
            BuildMallitalLakefront(ctx);
            Debug.Log("[Nainital] Mallital: " + string.Join(", ", notes));
        }

        // ------------------------------------------------------------------ Capitol Cinema
        // CapitolCinema and VideoGamesHall: WorldBuilder.Capitol.cs

        static void TyrePark(WorldCtx ctx, MeshBuilder mb, MeshBuilder col, Vector2 p)
        {
            var dirt = new MeshBuilder();
            const float R = 13f;
            for (int i = 0; i < 24; i++)
            {
                float a0 = i * Mathf.PI / 12f, a1 = (i + 1) * Mathf.PI / 12f;
                for (int ring = 0; ring < 3; ring++)
                {
                    float r0 = ring * R / 3f, r1 = (ring + 1) * R / 3f;
                    Vector2 q00 = p + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r0, q01 = p + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r0;
                    Vector2 q10 = p + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r1, q11 = p + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1;
                    if (ctx.InLake(q10)) continue;
                    dirt.Quad(V(q00, ctx.H(q00) + 0.05f), V(q10, ctx.H(q10) + 0.05f), V(q11, ctx.H(q11) + 0.05f), V(q01, ctx.H(q01) + 0.05f), new Rect(q00.x / 4f, q00.y / 4f, (r1 - r0) / 4f, (r1 - r0) / 4f), Vector3.up);
                }
            }
            PaintDirtPath(dirt);   // trodden earth in the terrain's own dirt layer (2026-11-16: the ground is one surface)
            for (float z = -R; z <= R; z += 4f) { float hx = Mathf.Sqrt(Mathf.Max(0f, R * R - z * z)); if (hx > 1f) noGrassExtra.Add((new[] { p + new Vector2(-hx, z), p + new Vector2(hx, z) }, 2.7f)); }
            var rnd = new System.Random(808);
            int[] cols = { Pal.JacketRed, Pal.BoatYellow, Pal.BoatGreen, Pal.BoatBlue, Pal.SareeOrange };
            for (int i = 0; i < 9; i++)
            {
                Vector2 q = p + new Vector2((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 16f;
                float y = ctx.H(q);
                var rot = Quaternion.Euler(0, (float)rnd.NextDouble() * 180f, 90f);   // a half-buried tyre standing on edge
                using (PropKit.At(mb, V(q, y - 0.15f), rot)) mb.Cylinder(new Vector3(0, -0.12f, 0), 0.45f, 0.45f, 0.24f, 12, Atlas.SolidUV(cols[rnd.Next(cols.Length)]), true);
                col.Box(V(q, y + 0.25f), new Vector3(0.9f, 0.6f, 0.3f), rot, Vector2.zero);
            }
        }

        // ------------------------------------------------------------------ Naina Devi gate and the Tibetan market plaza
        // TempleGate (the red twin-arched gate) and MargGate (the sandstone gate by the police booth): WorldBuilder.Gates.cs

        static void TibetanPlaza(WorldCtx ctx, Transform parent, Vector2 c, Vector2 temple)
        {
            // grey flagstones with a ring pattern, and a row of stalls under blue tarps along the side away from the temple
            var cobble = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(Gen + "/M_MallCobble.mat");
            var pave = new MeshBuilder();
            const float R = 12f;
            int segs = 32;
            for (int i = 0; i < segs; i++)
                for (int ring = 0; ring < 4; ring++)
                {
                    float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs, r0 = ring * R / 4f, r1 = (ring + 1) * R / 4f;
                    Vector2 p00 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r0, p01 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r0;
                    Vector2 p10 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r1, p11 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1;
                    if (ctx.InLake(p10)) continue;
                    var uv = new Rect((p00.x - c.x) / 2.5f, (p00.y - c.y) / 2.5f, (r1 - r0) / 2.5f, (r1 - r0) / 2.5f);
                    pave.Quad(V(p00, ctx.H(p00) + 0.08f), V(p10, ctx.H(p10) + 0.08f), V(p11, ctx.H(p11) + 0.08f), V(p01, ctx.H(p01) + 0.08f), uv, Vector3.up);
                }
            PaintRoadSurface(pave, ChGreyPaver);   // into the ground (2026-11-16)
            var ring2 = new MeshBuilder();
            Vector2 dark = Atlas.SolidUV(Pal.StoneGrey);
            for (int k = 1; k <= 3; k++)
            {
                float rr = k * 1.6f;
                for (int i = 0; i < 24; i++)
                {
                    float a0 = i * Mathf.PI / 12f, a1 = (i + 1) * Mathf.PI / 12f;
                    Vector2 p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * rr, p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * rr;
                    Vector2 q0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * (rr + 0.25f), q1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * (rr + 0.25f);
                    ring2.Quad(V(p0, ctx.H(p0) + 0.02f), V(q0, ctx.H(q0) + 0.02f), V(q1, ctx.H(q1) + 0.02f), V(p1, ctx.H(p1) + 0.02f), dark, Vector3.up);   // on the painted flagstones
                }
            }
            ctx.MeshObject("Tibetan market ring", parent, ring2, ctx.mats.atlas, null, false);
            noGrassExtra.Add((new[] { c - Vector2.right * R, c + Vector2.right * R }, R));

            // the stall row: a long run of small shops, tarps sloping towards the plaza
            Vector2 away = (c - temple).normalized, along = new Vector2(away.y, -away.x);
            var stalls = new MeshBuilder();
            var stallCol = new MeshBuilder();
            var rnd = new System.Random(919);
            int[] goods = { Pal.SareeOrange, Pal.JacketBlue, Pal.ShirtWhite, Pal.SareePink, Pal.JacketYellow, Pal.Jeans, Pal.TempleRed, Pal.BoatGreen, Pal.JacketRed };
            Vector2 wood = Atlas.MaterialUV(Atlas.MatWood), tarp = Atlas.SolidUV(Pal.BoatBlue), pole = Atlas.SolidUV(Pal.IronBlack);
            // two facing rows with a lane between, as in the photo sphere: one on the plaza's far edge, one beyond it
            var slots = new List<(Vector2 at, Vector2 facing)>();
            for (int i = -5; i <= 5; i++) slots.Add((c + away * (R - 2f) + along * (i * 3.2f), -away));
            for (int i = -4; i <= 4; i++) slots.Add((c + away * (R + 4.5f) + along * (i * 3.2f + 1.6f), away));
            foreach (var (s2, facing) in slots)
            {
                if (ctx.InLake(s2)) continue;
                float y = ctx.H(s2);
                var rot = Quaternion.LookRotation(Flat(facing));   // open towards the lane / the plaza
                Vector3 s = V(s2, y), fwd = Flat(facing), side = Flat(along);
                stalls.Box(s + Vector3.up * 1.2f - fwd * 0.9f, new Vector3(3.1f, 2.4f, 0.1f), rot, wood);                // back board
                stalls.Box(s + Vector3.up * 0.45f, new Vector3(2.9f, 0.9f, 1.4f), rot, wood);                             // counter
                for (int k = 0; k < 6; k++)
                    stalls.Box(s + Vector3.up * (0.95f + (float)rnd.NextDouble() * 0.15f) + side * ((k - 2.5f) * 0.45f), new Vector3(0.38f, 0.12f + (float)rnd.NextDouble() * 0.15f, 1.1f), rot, Atlas.SolidUV(goods[rnd.Next(goods.Length)]));
                for (int k = 0; k < 6; k++)   // shawls and jackets hanging on the back board
                    stalls.QuadDouble(s - fwd * 0.82f + side * ((k - 2.5f) * 0.5f - 0.15f) + Vector3.up * 1.2f, s - fwd * 0.82f + side * ((k - 2.5f) * 0.5f + 0.15f) + Vector3.up * 1.2f,
                                      s - fwd * 0.82f + side * ((k - 2.5f) * 0.5f + 0.15f) + Vector3.up * 2.2f, s - fwd * 0.82f + side * ((k - 2.5f) * 0.5f - 0.15f) + Vector3.up * 2.2f,
                                      new Rect(Atlas.SolidUV(goods[rnd.Next(goods.Length)]), Vector2.zero), fwd);
                Vector3 t0 = s + Vector3.up * 2.6f - fwd * 0.9f, t1 = s + Vector3.up * 2.1f + fwd * 1.6f;
                stalls.QuadDouble(t0 - side * 1.6f, t0 + side * 1.6f, t1 + side * 1.6f, t1 - side * 1.6f, new Rect(tarp, Vector2.zero), Vector3.up);
                foreach (float sd in new[] { -1f, 1f }) stalls.Limb(t1 + side * (sd * 1.5f), t1 + side * (sd * 1.5f) - Vector3.up * 2.1f, 0.025f, 0.025f, 4, pole, false);
                stallCol.Box(s + Vector3.up * 0.5f, new Vector3(2.9f, 1f, 1.4f), rot, Vector2.zero);
            }
            ctx.MeshObject("Tibetan market stalls", parent, stalls, ctx.mats.atlas, stallCol);
            ctx.Zone("Tibetan Market", V(c, ctx.H(c)), 30f);
            ctx.landmarkSites["Tibetan Market"] = V(c, ctx.H(c));
        }

        // ------------------------------------------------------------------ lakeside: police booth, bandstand, boat ghat
        static void PoliceBooth(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 p, Vector2? facing = null)
        {
            float y = ctx.H(p);
            var near = NearestRoadPoint(ctx, p, 30f, r => !IsMinor(r.kind), out var rp, out _) ? rp : p + Vector2.up;
            Vector2 f2 = facing ?? ((near - p).sqrMagnitude > 0.01f ? (near - p).normalized : Vector2.up);
            var rot = Quaternion.LookRotation(Flat(f2));
            Vector3 c = V(p, y), front = Flat(f2);
            mb.Box(c + Vector3.up * 0.5f, new Vector3(2.6f, 1f, 2.2f), rot, Atlas.SolidUV(Pal.TempleRed));
            mb.Box(c + Vector3.up * 1.7f, new Vector3(2.4f, 1.4f, 2.0f), rot, Atlas.SolidUV(Pal.ShirtWhite));
            mb.Box(c + Vector3.up * 1.75f + front * 1.01f, new Vector3(1.6f, 0.8f, 0.04f), rot, Atlas.SolidUV(Pal.IronBlack));
            mb.Box(c + Vector3.up * 2.55f, new Vector3(2.9f, 0.18f, 2.5f), rot, Atlas.SolidUV(Pal.TempleRed));
            mb.Box(c + Vector3.up * 3.3f + front * 0.6f, new Vector3(2.8f, 1.1f, 0.08f), rot, Atlas.SolidUV(Pal.BoatBlue));
            col.Box(c + Vector3.up * 1.3f, new Vector3(2.6f, 2.6f, 2.2f), rot, Vector2.zero);
            var holder = new GameObject("Police booth").transform;
            holder.SetParent(parent, false);
            Label(holder, font, "POLICE SAHAYATA KENDRA", c + Vector3.up * 3.45f + front * 0.65f, front, 0.2f, Color.white);
            Label(holder, font, "NAINITAL", c + Vector3.up * 3.05f + front * 0.65f, front, 0.24f, new Color(1f, 0.9f, 0.4f));
        }

        static void Bandstand(WorldCtx ctx, MeshBuilder mb, MeshBuilder col, Vector2 p)
        {
            p = ctx.PushFromLake(p, 5.5f);
            float y = ctx.H(p);
            Vector3 c = V(p, y);
            // 2025 video: an octagon on a stone plinth, dark timber posts, a white balustrade, a maroon roof
            Vector2 white = Atlas.SolidUV(Pal.ShirtWhite), timber = Atlas.SolidUV(Pal.WoodDark), maroon = Atlas.SolidUV(Pal.BoatRed), stone = Atlas.MaterialUV(Atlas.MatStone);
            mb.Cylinder(c, 4.4f, 4.4f, 0.9f, 8, stone, true);
            col.Cylinder(c, 4.4f, 4.4f, 0.9f, 8, Vector2.zero, true);
            for (int k = 0; k < 8; k++)
            {
                Vector3 q = c + Quaternion.Euler(0, 22.5f + k * 45f, 0) * Vector3.forward * 3.9f;
                mb.Cylinder(q + Vector3.up * 0.9f, 0.15f, 0.15f, 3.2f, 8, timber, true);
                mb.Box(q + Vector3.up * 3.95f, new Vector3(0.4f, 0.3f, 0.4f), timber);
                Vector3 q2 = c + Quaternion.Euler(0, 22.5f + (k + 1) * 45f, 0) * Vector3.forward * 3.9f;
                if (k == 0) continue;   // the steps come up here
                mb.Limb(q + Vector3.up * 1.75f, q2 + Vector3.up * 1.75f, 0.06f, 0.06f, 4, white, false);   // balustrade rail
                for (int b = 1; b < 7; b++) mb.Cylinder(Vector3.Lerp(q, q2, b / 7f) + Vector3.up * 0.9f, 0.05f, 0.04f, 0.85f, 5, white, true);
            }
            mb.Cylinder(c + Vector3.up * 4.1f, 4.6f, 4.6f, 0.3f, 8, timber, true);
            mb.Pyramid(c + Vector3.up * 4.4f, 9.2f, 9.2f, 2.0f, maroon);
            mb.Cylinder(c + Vector3.up * 6.3f, 0.12f, 0.05f, 0.9f, 6, timber, true);
        }

        static void BoatGhat(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Vector2 p)
        {
            // stone steps down to the water at the nearest shore, and rowing boats moored side by side
            float bd = float.MaxValue;
            int k = 0;
            for (int i = 0; i < ctx.lake.Length; i++) { float d = (ctx.lake[i] - p).sqrMagnitude; if (d < bd) { bd = d; k = i; } }
            Vector2 shore = ctx.lake[k], o = Outward(ctx.lake, k), along = new Vector2(o.y, -o.x);
            float top = ctx.H(shore + o * 2.5f), water = ctx.lakeY;
            int steps = Mathf.Clamp(Mathf.RoundToInt((top - water) / 0.3f), 2, 10);
            var rot = Quaternion.LookRotation(Flat(-o));
            Rect stone = Atlas.Material(Atlas.MatStone);
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;
                Vector2 q = shore + o * (2.2f - s * 0.45f);
                float h = Mathf.Lerp(top, water - 0.3f, t);
                TexBox(mb, V(q, (h + water - 0.6f) * 0.5f), new Vector3(12f, h - (water - 0.6f), 0.45f), rot, stone);
            }
            col.Box(V(shore + o * 1f, (top + water) * 0.5f), new Vector3(12f, top - water, 2.6f), rot, Vector2.zero);
            // the white balustrade along the top of the bank either side of the steps (user's video, 0:12 and 8:52):
            // turned balusters between square posts, a moulded rail on top, on a low plinth
            Vector2 white = Atlas.SolidUV(Pal.TempleWhite), cream = Atlas.SolidUV(Pal.ShirtWhite);
            foreach (float side in new[] { -1f, 1f })
            {
                for (float s = 6.4f; s <= 20f; s += 3f)
                {
                    float s1 = Mathf.Min(s + 3f, 20.4f);
                    Vector2 a = shore + o * 2.6f + along * (side * s), b = shore + o * 2.6f + along * (side * s1);
                    if (ctx.InLake(a + o * 0.6f) || ctx.InLake(b + o * 0.6f)) break;   // the shore turns away: stop at the water
                    float ya = ctx.H(a), yb = ctx.H(b), yl = Mathf.Max(ya, yb);
                    Vector2 m2 = (a + b) * 0.5f;
                    var rr = Quaternion.LookRotation(Flat((b - a).normalized));
                    float len = Vector2.Distance(a, b);
                    mb.Box(V(m2, yl + 0.12f), new Vector3(0.42f, 0.24f, len), rr, cream);              // plinth
                    mb.Box(V(m2, yl + 0.9f), new Vector3(0.34f, 0.1f, len + 0.04f), rr, white);         // rail
                    mb.Box(V(m2, yl + 0.96f), new Vector3(0.26f, 0.05f, len + 0.04f), rr, cream);
                    for (float t = 0.25f; t < len - 0.15f; t += 0.26f)
                    {
                        Vector3 q = V(Vector2.Lerp(a, b, t / len), yl + 0.24f);
                        mb.Cylinder(q, 0.07f, 0.045f, 0.3f, 6, white, false);
                        mb.Cylinder(q + Vector3.up * 0.3f, 0.045f, 0.075f, 0.31f, 6, white, false);
                    }
                    mb.Box(V(a, yl + 0.5f), new Vector3(0.36f, 1.0f, 0.36f), rr, white);                // post
                    col.Box(V(m2, yl + 0.5f), new Vector3(0.34f, 1.0f, len), rr, Vector2.zero);
                }
            }
            int[] hulls = { Pal.BoatYellow, Pal.BoatGreen, Pal.BoatBlue, Pal.TempleRed, Pal.BoatYellow, Pal.BoatGreen, Pal.BoatBlue };
            var boats = new MeshBuilder();
            for (int b = 0; b < 7; b++)
            {
                Vector2 q = shore - o * 3.4f + along * ((b - 3) * 1.55f);
                using (PropKit.At(boats, V(q, water + 0.05f), Quaternion.LookRotation(Flat(-o)) * Quaternion.Euler(0, (b % 2) * 6f - 3f, 0)))
                    PropKit.RowBoat(boats, hulls[b], b % 3 == 0);
            }
            ctx.MeshObject("Mallital boat ghat boats", parent, boats, ctx.mats.atlas);
        }
    }
}

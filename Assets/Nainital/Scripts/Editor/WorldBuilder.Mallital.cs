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
                float width = frontIsU ? bd : bw, depth = frontIsU ? bw : bd;
                return (c, front, Mathf.Min(width, 30f), Mathf.Min(depth, 22f));
            }
            return null;
        }

        static void PlanMallital(WorldCtx ctx)
        {
            var cap = CapitolBox(ctx);
            if (cap.HasValue) ctx.keepClear.Add((cap.Value.c, Mathf.Max(cap.Value.w, cap.Value.d) * 0.5f + 1f));
            foreach (var (name, r) in new[] { ("Tibetan Market", 18f), ("Naina Devi Gate", 4f), ("Mallital Police Booth", 3f), ("Bandstand", 6f), ("Children's Park (Capitol)", 10f) })
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
                CapitolLakeShops(ctx, parent, mb, font, cap.Value.c, cap.Value.front, cap.Value.w, cap.Value.d);
                notes.Add("Capitol");
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
            var booth = ctx.Poi("Mallital Police Booth");
            if (booth.HasValue) { PoliceBooth(ctx, parent, mb, col, font, booth.Value); notes.Add("police booth"); }
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
        static void CapitolCinema(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 c2, Vector2 f2, float W, float D)
        {
            Vector2 r2 = new Vector2(f2.y, -f2.x);
            float y = ctx.H(c2 + f2 * (D * 0.5f)) - 0.2f, yBase = Mathf.Min(y, ctx.H(c2 - f2 * (D * 0.5f))) - 0.5f;
            var rot = Quaternion.LookRotation(Flat(f2));
            Vector3 c = V(c2, y), front = Flat(f2), right = Flat(r2);
            Vector2 white = Atlas.WallSolid(0), red = Atlas.SolidUV(Pal.TempleRed), glass = Atlas.SolidUV(Pal.IronBlack), conc = Atlas.MaterialUV(Atlas.MatConcrete);
            const float H = 8.4f;
            // body: white walls on a stone base
            mb.Box(c + Vector3.up * ((H + y - yBase) * 0.5f - (y - yBase)), new Vector3(W, H + (y - yBase), D), rot, white);
            col.Box(c + Vector3.up * (H * 0.5f), new Vector3(W, H, D), rot, Vector2.zero);
            mb.Box(c + Vector3.up * (H + 0.15f), new Vector3(W + 0.4f, 0.3f, D + 0.4f), rot, red);       // red cornice
            mb.Box(c + Vector3.up * 3.6f + front * (D * 0.5f + 0.05f), new Vector3(W, 0.25f, 0.2f), rot, red);   // band over the shops
            // the central bay: raised parapet with the CAPITOL sign band
            Vector3 bay = c + front * (D * 0.5f + 0.4f);
            mb.Box(bay + Vector3.up * (H * 0.5f + 0.6f), new Vector3(W * 0.42f, H + 1.2f, 0.8f), rot, white);
            mb.Box(bay + Vector3.up * (H + 0.75f) + front * 0.42f, new Vector3(W * 0.4f, 1.2f, 0.08f), rot, red);
            mb.Box(bay + Vector3.up * (H - 0.35f) + front * 0.42f, new Vector3(W * 0.16f, 0.45f, 0.06f), rot, Atlas.SolidUV(Pal.ShirtWhite));
            var holder = new GameObject("Capitol Cinema sign").transform;
            holder.SetParent(parent, false);
            Label(holder, font, "CAPITOL", bay + Vector3.up * (H + 0.75f) + front * 0.47f, front, 0.85f, Color.white);
            Label(holder, font, "CINEMA", bay + Vector3.up * (H - 0.35f) + front * 0.5f, front, 0.3f, new Color(0.7f, 0.08f, 0.08f));
            // first floor: tall red-framed windows between red pilasters
            int bays = Mathf.Max(5, Mathf.RoundToInt(W / 3f));
            for (int i = 0; i < bays; i++)
            {
                float s = (i + 0.5f) / bays - 0.5f;
                Vector3 wpos = c + right * (s * W) + front * (D * 0.5f + (Mathf.Abs(s) < 0.21f ? 0.82f : 0.04f)) + Vector3.up * 5.6f;
                mb.Box(wpos, new Vector3(1.4f, 2.2f, 0.1f), rot, red);
                mb.Box(wpos + front * 0.04f, new Vector3(1.0f, 1.8f, 0.08f), rot, glass);
                mb.Box(c + right * ((s + 0.5f / bays) * W) + front * (D * 0.5f + 0.1f) + Vector3.up * (H * 0.5f), new Vector3(0.35f, H, 0.2f), rot, red);
            }
            // ground floor: shop openings with boards
            var shopRnd = new System.Random(77);
            string[] boards = { "THE PAHARI STORE", "GALAXY", "CAPITOL CAFE", "WOOLLENS", "TICKETS" };
            for (int i = 0; i < 5; i++)
            {
                float s = (i + 0.5f) / 5f - 0.5f;
                Vector3 shop = c + right * (s * W) + front * (D * 0.5f + 0.06f) + Vector3.up * 1.4f;
                mb.Box(shop, new Vector3(W / 5f - 1.0f, 2.6f, 0.08f), rot, glass);
                mb.Box(shop + Vector3.up * 1.7f + front * 0.04f, new Vector3(W / 5f - 0.8f, 0.55f, 0.06f), rot, i % 2 == 0 ? red : Atlas.SolidUV(Pal.BoatBlue));
                Label(holder, font, boards[i], shop + Vector3.up * 1.7f + front * 0.09f, front, 0.22f, Color.white);
            }
            // the film billboard on the roof
            Vector3 bb = c + right * (-W * 0.18f) + front * (D * 0.5f - 2f) + Vector3.up * (H + 2.6f);
            mb.Box(bb, new Vector3(6.5f, 3.2f, 0.12f), rot, Atlas.SolidUV(Pal.JacketBlue));
            mb.Box(bb + front * 0.07f + right * 1.4f, new Vector3(2.4f, 2.6f, 0.04f), rot, Atlas.SolidUV(Pal.SareeOrange));
            mb.Box(bb + front * 0.07f - right * 1.6f + Vector3.up * 0.5f, new Vector3(2.2f, 1.2f, 0.04f), rot, Atlas.SolidUV(Pal.ShirtWhite));
            foreach (float s in new[] { -1f, 1f }) mb.Box(bb + right * (s * 2.8f) + Vector3.down * 1.8f, new Vector3(0.12f, 2.2f, 0.12f), rot, glass);
            ctx.InfoSign("Capitol Cinema", "1930s ka purana cinema hall, Mallital mein Flats ke paas. Angrezon ke zamane se yahan filmein chalti aa rahi hain.",
                         V(c2 + f2 * (D * 0.5f + 6f) + r2 * (W * 0.5f + 1f), ctx.H(c2 + f2 * (D * 0.5f + 6f) + r2 * (W * 0.5f + 1f))), rot, parent);
        }

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
            ctx.MeshObject("Children's park ground", ctx.worldRoot, dirt, ctx.mats.dirtPath, null, false);
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
        static void TempleGate(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 at, Vector2 face2)
        {
            Vector3 face = Flat(face2), right = Vector3.Cross(Vector3.up, face);
            var rot = Quaternion.LookRotation(face);
            float y = ctx.H(at);
            Vector2 lavender = Atlas.SolidUV(Pal.SareePink), gold = Atlas.SolidUV(Pal.Saffron), red = Atlas.SolidUV(Pal.TempleRed), white = Atlas.SolidUV(Pal.ShirtWhite);
            const float span = 4.4f, H = 4.2f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 p = V(at, y) + right * (s * (span * 0.5f + 0.35f));
                mb.Box(p + Vector3.up * 0.25f, new Vector3(1.0f, 0.5f, 1.0f), rot, white);
                mb.Box(p + Vector3.up * (H * 0.5f + 0.25f), new Vector3(0.62f, H - 0.5f, 0.62f), rot, lavender);
                mb.Box(p + Vector3.up * (H + 0.05f), new Vector3(0.85f, 0.25f, 0.85f), rot, white);
                col.Box(p + Vector3.up * (H * 0.5f), new Vector3(0.7f, H, 0.7f), rot, Vector2.zero);
            }
            // the golden arched top, curling up at both ends
            Vector3 last = default;
            for (int k = 0; k <= 16; k++)
            {
                float a = k / 16f;
                float lift = Mathf.Sin(a * Mathf.PI) * 0.9f + (Mathf.Pow(Mathf.Abs(a - 0.5f) * 2f, 6f)) * 0.7f;
                Vector3 q = V(at, y) + right * ((a - 0.5f) * (span + 1.6f)) + Vector3.up * (H + 0.3f + lift);
                if (k > 0)
                {
                    mb.Limb(last, q, 0.3f, 0.3f, 8, gold);                      // the golden arch: a thick curved bar
                    mb.Limb(last + Vector3.up * 0.32f, q + Vector3.up * 0.32f, 0.12f, 0.12f, 6, gold, false);   // a thin rail over it
                }
                last = q;
            }
            // bells hanging under the arch, and a red board
            for (int k = -2; k <= 2; k++) mb.Sphere(V(at, y) + right * (k * 0.7f) + Vector3.up * (H - 0.2f), new Vector3(0.12f, 0.16f, 0.12f), 6, 4, gold);
            mb.Box(V(at, y) + Vector3.up * (H + 1.55f) + face * 0.3f, new Vector3(2.6f, 0.5f, 0.06f), rot, red);
            var holder = new GameObject("Naina Devi gate").transform;
            holder.SetParent(parent, false);
            Label(holder, font, "SHRI MAA NAINA DEVI MANDIR", V(at, y) + Vector3.up * (H + 1.55f) + face * 0.35f, face, 0.17f, new Color(1f, 0.9f, 0.5f));
        }

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
            ctx.MeshObject("Tibetan market plaza", parent, pave, cobble ? cobble : ctx.mats.paver, null, false);
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
                    ring2.Quad(V(p0, ctx.H(p0) + 0.1f), V(q0, ctx.H(q0) + 0.1f), V(q1, ctx.H(q1) + 0.1f), V(p1, ctx.H(p1) + 0.1f), dark, Vector3.up);
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
        static void PoliceBooth(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 p)
        {
            float y = ctx.H(p);
            var near = NearestRoadPoint(ctx, p, 30f, r => !IsMinor(r.kind), out var rp, out _) ? rp : p + Vector2.up;
            Vector2 f2 = (near - p).sqrMagnitude > 0.01f ? (near - p).normalized : Vector2.up;
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

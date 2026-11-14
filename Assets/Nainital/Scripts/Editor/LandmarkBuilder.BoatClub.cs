using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Boat House Club, Nainital (user's Google Maps photos and satellite, 2026-11-14: "Nainital Boat House recreate karo").
    /// On its OpenStreetMap footprint, a C of three wings round a dark wooden deck that opens onto the lake: white walls with
    /// big gridded windows behind iron grilles, red tin gable roofs with little gables over the windows, a red lean-to
    /// verandah roof with a green valance along the street, white scalloped bargeboards; at the lake corner the white
    /// lantern tower with arched openings under a steep rust-red spire; yellow wainscot on the lake side; the restaurant
    /// deck of wooden boards with tables, chairs and big beige umbrellas, a white mesh railing, lamp posts; a jetty to the
    /// south-east with the club's green-hulled sail boats; the "BOAT HOUSE CLUB NAINITAL" board over the street roof.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        // the three wings as centre lines (game metres) and their widths, fitted to the OSM footprint
        static readonly (Vector2 a, Vector2 b, float w)[] ClubWings =
        {
            (new Vector2(111f, 85f), new Vector2(151f, 70f), 10f),   // the street wing, along the Mall
            (new Vector2(101f, 59f), new Vector2(111f, 82f), 8f),    // the west wing down to the lake corner
            (new Vector2(150f, 69f), new Vector2(146f, 49f), 9f),    // the east wing down to the water
        };
        static readonly Vector2 ClubTower = new Vector2(101f, 56f), ClubDeck = new Vector2(126f, 56f), ClubJetty = new Vector2(138f, 40f);

        internal static IEnumerable<(Vector2 c, float r)> BoatClubSpots()
        {
            foreach (var (a, b, w) in ClubWings) yield return ((a + b) * 0.5f, Vector2.Distance(a, b) * 0.5f + w * 0.5f);
            var f = ClubDeckFrame(Vector2.zero);
            yield return (f.c, Mathf.Max(f.W, f.D) * 0.5f);
        }

        /// <summary>The deck between the wings: square to the street wing, from its inner face out past the side wings' lake
        /// ends, from the west wing's inner face to the east wing's. rot's forward points to the street (north).</summary>
        static (Vector2 c, Quaternion rot, float W, float D) ClubDeckFrame(Vector2 streetShift)
        {
            var (a0, b0, w0) = ClubWings[0];
            Vector2 a = a0 + streetShift, b = b0 + streetShift, mid = (a + b) * 0.5f;
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
            if (n.y < 0f) n = -n;   // the street is north of the club, the lake south
            float left = -Vector2.Distance(a, b) * 0.5f, right = -left, far = 0f;
            for (int i = 1; i < ClubWings.Length; i++)
            {
                var (sa, sb, sw) = ClubWings[i];
                float along = Vector2.Dot((sa + sb) * 0.5f - mid, d);
                if (along < 0f) left = Mathf.Max(left, along + sw * 0.5f);
                else right = Mathf.Min(right, along - sw * 0.5f);
                far = Mathf.Max(far, Mathf.Max(Vector2.Dot(sa - mid, -n), Vector2.Dot(sb - mid, -n)));
            }
            float W = Mathf.Max(10f, right - left), D = Mathf.Clamp(far + 3f - w0 * 0.5f, 10f, 32f);
            Vector2 c = mid + d * ((left + right) * 0.5f) - n * (w0 * 0.5f + D * 0.5f);
            return (c, Quaternion.LookRotation(new Vector3(n.x, 0f, n.y)), W, D);
        }

        static void BoatHouseClub(WorldCtx ctx, Transform parent)
        {
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var words = new GameObject("Boat House Club board").transform;
            words.SetParent(parent, false);
            Vector2 white = S(Pal.TempleWhite), red = S(Pal.TempleRed), green = S(Pal.BoatGreen), grille = S(Pal.IronBlack), glass = M(Atlas.MatGlass),
                    yellow = S(Pal.BoatYellow), wood = M(Atlas.MatWood), rust = S(Pal.ShoeBrown), cream = S(Pal.ShirtWhite), conc = M(Atlas.MatConcrete);
            float floor = ctx.lakeY + 1.4f;   // the club stands just above the water
            const float H = 6.4f;
            var redR = new Rect(red, Vector2.zero);

            void Wing(Vector2 a, Vector2 b, float w, bool street)
            {
                Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
                float len = Vector2.Distance(a, b);
                Vector2 mid = (a + b) * 0.5f;
                var rot = Quaternion.LookRotation(new Vector3(d.x, 0f, d.y));
                Vector3 m3 = new Vector3(mid.x, floor, mid.y), D3 = new Vector3(d.x, 0f, d.y), N3 = new Vector3(n.x, 0f, n.y);
                // the ground under it down to the floor, the walls, the yellow wainscot
                LandmarkBuilder.CutGround(ctx, mid, n, len * 0.5f + 1f, w * 0.5f + 1f, floor - 0.2f);
                mb.Box(m3 + Vector3.up * (H * 0.5f - 1f), new Vector3(w, H + 2f, len), rot, white);
                col.Box(m3 + Vector3.up * (H * 0.5f), new Vector3(w, H, len), rot, Vector2.zero);
                mb.Box(m3 + Vector3.up * 0.55f, new Vector3(w + 0.06f, 1.1f, len + 0.06f), rot, street ? white : yellow);
                // the roof: red tin gable along the wing
                Vector3 ridgeA = m3 - D3 * (len * 0.5f + 0.5f) + Vector3.up * (H + w * 0.32f), ridgeB = m3 + D3 * (len * 0.5f + 0.5f) + Vector3.up * (H + w * 0.32f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 e = N3 * (s * (w * 0.5f + 0.6f)) + Vector3.up * (H - 0.15f);
                    mb.QuadDouble(m3 - D3 * (len * 0.5f + 0.5f) + e, m3 + D3 * (len * 0.5f + 0.5f) + e, ridgeB, ridgeA, redR, N3 * s + Vector3.up);
                    // white scalloped bargeboard along the eave
                    for (float t = -len * 0.5f; t < len * 0.5f; t += 0.45f)
                        mb.Box(m3 + D3 * t + N3 * (s * (w * 0.5f + 0.62f)) + Vector3.up * (H - 0.3f), new Vector3(0.06f, 0.3f, 0.36f), rot, cream);
                }
                foreach (float s in new[] { -1f, 1f })   // the gable ends, white
                    mb.Tri(m3 + D3 * (s * len * 0.5f) + N3 * (w * 0.5f) + Vector3.up * H, m3 + D3 * (s * len * 0.5f) - N3 * (w * 0.5f) + Vector3.up * H,
                           m3 + D3 * (s * len * 0.5f) + Vector3.up * (H + w * 0.32f), white, D3 * s);
                // windows down both long sides: big gridded panes behind a grille; little gables over them on the outer side
                for (float t = -len * 0.5f + 3f; t < len * 0.5f - 2f; t += 5.5f)
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 wc = m3 + D3 * t + N3 * (s * (w * 0.5f + 0.03f)) + Vector3.up * 3.6f;
                        var wr = Quaternion.LookRotation(N3 * s);
                        mb.Box(wc, new Vector3(4.2f, 1.7f, 0.04f), wr, glass);
                        for (float g = -2f; g <= 2.01f; g += 0.42f) mb.Box(wc + D3 * g + N3 * (s * 0.04f), new Vector3(0.04f, 1.75f, 0.03f), wr, grille);
                        for (float g = -0.8f; g <= 0.81f; g += 0.4f) mb.Box(wc + Vector3.up * g + N3 * (s * 0.04f), new Vector3(4.25f, 0.04f, 0.03f), wr, grille);
                        mb.Box(wc + Vector3.up * 1.05f, new Vector3(4.6f, 0.4f, 0.06f), wr, white);
                        if (street && s > 0f)   // the small gable over the window, out of the roof
                        {
                            Vector3 gb = m3 + D3 * t + N3 * (w * 0.5f + 0.2f) + Vector3.up * (H - 0.1f);
                            mb.Tri(gb - D3 * 2.4f, gb + D3 * 2.4f, gb + Vector3.up * 1.5f, white, N3);
                            mb.QuadDouble(gb - D3 * 2.6f + N3 * 0.3f, gb + Vector3.up * 1.6f + N3 * 0.3f, gb + Vector3.up * 1.6f - N3 * 2.2f, gb - D3 * 2.6f - N3 * 2.2f, redR, Vector3.up - D3);
                            mb.QuadDouble(gb + Vector3.up * 1.6f + N3 * 0.3f, gb + D3 * 2.6f + N3 * 0.3f, gb + D3 * 2.6f - N3 * 2.2f, gb + Vector3.up * 1.6f - N3 * 2.2f, redR, Vector3.up + D3);
                            for (float g = -1.8f; g <= 1.81f; g += 0.6f) mb.Box(gb + D3 * g + Vector3.up * 0.55f + N3 * 0.02f, new Vector3(0.5f, 0.45f, 0.03f), wr, glass);
                        }
                    }
                if (street)   // the verandah along the street: a red lean-to roof on posts with a green valance, grilles below
                {
                    Vector3 v0 = m3 + N3 * (w * 0.5f) + Vector3.up * 2.7f;
                    mb.QuadDouble(v0 - D3 * (len * 0.5f), v0 + D3 * (len * 0.5f), v0 + D3 * (len * 0.5f) + N3 * 2.4f + Vector3.down * 0.5f, v0 - D3 * (len * 0.5f) + N3 * 2.4f + Vector3.down * 0.5f, redR, N3 + Vector3.up * 3f);
                    mb.Box(v0 + N3 * 2.42f + Vector3.down * 0.7f, new Vector3(0.05f, 0.4f, len), rot, green);
                    for (float t = -len * 0.5f; t <= len * 0.5f; t += 3f) mb.Box(m3 + D3 * t + N3 * (w * 0.5f + 2.3f) + Vector3.up * 1.1f, new Vector3(0.12f, 2.2f, 0.12f), rot, white);
                    for (float t = -len * 0.5f + 0.2f; t < len * 0.5f; t += 0.22f) mb.Box(m3 + D3 * t + N3 * (w * 0.5f + 0.05f) + Vector3.up * 1.2f, new Vector3(0.03f, 1.8f, 0.03f), rot, cream);
                }
            }
            // The street wing (and its 2.4 m verandah) must stay off the road (user, 2026-11-14 screenshot: "boat house clipping
            // with road"). The OSM outline runs to the kerb; find how far the street face and the verandah reach into any road
            // and move the wing back towards the deck by that much.
            Vector2 streetShift = Vector2.zero;
            {
                var (a, b, w) = ClubWings[0];
                Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
                if (ctx.InLake((a + b) * 0.5f + n * 20f)) n = -n;   // n: towards the street
                float worst = 0f;
                foreach (var r in ctx.world.roads)
                {
                    if (r.pts == null || r.pts.Length < 4) continue;
                    var line = Geo.ToPoints(r.pts);
                    for (float t = 0f; t <= 1.001f; t += 0.1f)
                    {
                        Vector2 face = Vector2.Lerp(a, b, t) + n * (w * 0.5f + 2.6f);   // the verandah's outer edge
                        float clear = Geo.DistanceToPolyline(line, face, false) - (r.width * 0.5f + 0.6f);
                        // only roads on the street side count (a road behind the wing is not hit by the verandah)
                        Vector2 q = Vector2.Lerp(a, b, t);
                        if (Geo.DistanceToPolyline(line, q, false) < Geo.DistanceToPolyline(line, q - n * 4f, false) && clear < -worst) worst = -clear;
                    }
                }
                streetShift = -n * Mathf.Min(worst, 6f);
                if (worst > 0f) Debug.Log($"[Nainital] Boat House Club: street wing moved {worst:F1} m off the road");
            }
            for (int wi = 0; wi < ClubWings.Length; wi++)
            {
                var (a, b, w) = ClubWings[wi];
                if (wi == 0) Wing(a + streetShift, b + streetShift, w, true);
                else
                {
                    // the side wings end at the street wing: pull their street ends back with it so nothing pokes through
                    Vector2 ea = a, eb = b;
                    var street = new[] { ClubWings[0].a, ClubWings[0].b };
                    if (Geo.DistanceToPolyline(street, a, false) < Geo.DistanceToPolyline(street, b, false)) ea += streetShift;
                    else eb += streetShift;
                    Wing(ea, eb, w, false);
                }
            }

            // the board on the street roof
            {
                var (a, b, w) = ClubWings[0];
                a += streetShift; b += streetShift;
                Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
                if (ctx.InLake((a + b) * 0.5f + n * 20f)) n = -n;   // the board faces the street
                Vector3 bc = new Vector3((a.x + b.x) * 0.5f - d.x * 6f, floor + H + 3.2f, (a.y + b.y) * 0.5f - d.y * 6f), N3 = new Vector3(n.x, 0f, n.y);
                var br = Quaternion.LookRotation(N3);
                mb.Box(bc, new Vector3(6.5f, 1.4f, 0.1f), br, cream);
                foreach (float s in new[] { -2.6f, 2.6f }) mb.Box(bc + new Vector3(d.x, 0f, d.y) * s + Vector3.down * 1.4f, new Vector3(0.1f, 1.6f, 0.1f), br, grille);
                WorldBuilder.Label(words, font, "BOAT HOUSE CLUB", bc + N3 * 0.07f + Vector3.up * 0.25f, N3, 0.45f, new Color(0.12f, 0.2f, 0.55f));
                WorldBuilder.Label(words, font, "NAINITAL", bc + N3 * 0.07f + Vector3.down * 0.35f, N3, 0.35f, new Color(0.12f, 0.2f, 0.55f));
            }

            // the lantern tower at the lake corner
            {
                Vector3 t = new Vector3(ClubTower.x, floor, ClubTower.y);
                mb.Box(t + Vector3.up * 4.5f, new Vector3(2.6f, 9f, 2.6f), white);
                col.Box(t + Vector3.up * 4.5f, new Vector3(2.6f, 9f, 2.6f), Vector2.zero);
                foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    for (int k = -1; k <= 1; k++)
                        ArchPane(mb, t + Vector3.up * 8f + Vector3.Cross(Vector3.up, n) * (k * 0.75f) + n * 1.32f, 0.5f, 1.0f, 0.3f, S(Pal.IronBlack), n);
                    mb.Box(t + Vector3.up * 9.6f + n * 1.32f, new Vector3(2.2f, 0.5f, 0.05f), Quaternion.LookRotation(n), cream);
                }
                mb.Box(t + Vector3.up * 9.1f, new Vector3(3.1f, 0.2f, 3.1f), cream);
                mb.Cylinder(t + Vector3.up * 9.9f, 2.4f, 2.4f, 0.12f, 8, rust, true, true, 1f, Mathf.PI / 8f);
                mb.Cylinder(t + Vector3.up * 10f, 2.3f, 0f, 4.2f, 8, rust, false, false, 1f, Mathf.PI / 8f);
                mb.Cylinder(t + Vector3.up * 14.1f, 0.05f, 0f, 0.9f, 5, cream, false);
            }

            // the restaurant deck between the wings, out over the water: boards, tables, chairs, umbrellas, rails, lamps
            // (user, 2026-11-14: "boat path waala part alag kyun hai") the deck is now square to the wings and fills the court
            // from the street wing's inner face to past the shore, wall to wall between the side wings; the jetty starts from
            // its lake edge.
            var (deckC, deckRot, deckW, deckD) = ClubDeckFrame(streetShift);
            Vector3 jettyRoot;
            {
                Vector3 dc = new Vector3(deckC.x, floor + 0.05f, deckC.y);
                var drot = deckRot;
                Vector3 dx = drot * Vector3.right, dz = drot * Vector3.forward;   // dz: towards the street
                mb.Box(dc + Vector3.down * 1.2f, new Vector3(deckW, 2.4f, deckD), drot, wood);
                col.Box(dc + Vector3.down * 1.2f, new Vector3(deckW, 2.4f, deckD), drot, Vector2.zero);
                // timber piles under the part over the water
                for (float x = -deckW * 0.5f + 1f; x < deckW * 0.5f; x += 4f)
                    for (float z = -deckD * 0.5f + 1f; z < 0f; z += 4f)
                        mb.Cylinder(dc + dx * x + dz * z + Vector3.down * 4f, 0.18f, 0.18f, 2.8f, 6, S(Pal.WoodDark), false);
                float jx = deckW * 0.3f;   // where the jetty leaves the railing
                jettyRoot = dc + dx * jx - dz * (deckD * 0.5f);
                // the white mesh railing along the lake edge, with a gap for the jetty
                for (float x = -deckW * 0.5f; x < deckW * 0.5f - 0.5f; x += 2f)
                {
                    if (Mathf.Abs(x + 1f - jx) < 1.6f) continue;
                    Vector3 p = dc + dx * (x + 1f) - dz * (deckD * 0.5f - 0.05f);
                    mb.Box(p + Vector3.up * 0.55f, new Vector3(2f, 1.1f, 0.04f), drot, cream);
                    mb.Box(p + Vector3.up * 1.12f, new Vector3(2.05f, 0.08f, 0.14f), drot, white);
                }
                for (int k = 0; k < 4; k++)   // lamp posts at the railing
                {
                    Vector3 lp = dc + dx * (-deckW * 0.4f + k * deckW * 0.25f) - dz * (deckD * 0.5f - 0.3f);
                    mb.Cylinder(lp, 0.07f, 0.06f, 3.4f, 6, white, false);
                    mb.Sphere(lp + Vector3.up * 3.55f, Vector3.one * 0.22f, 8, 4, S(Pal.LampBulb));
                }
                var rnd = new System.Random(2024);
                int ti = Mathf.Max(2, Mathf.FloorToInt((deckW - 6f) / 6.5f)), tj = Mathf.Max(2, Mathf.FloorToInt((deckD - 6f) / 5f));
                for (int i = 0; i < ti; i++)
                    for (int j = 0; j < tj; j++)
                    {
                        Vector3 tp = dc + dx * ((i - (ti - 1) * 0.5f) * 6.5f) + dz * ((j - (tj - 1) * 0.5f) * 5f);
                        mb.Box(tp + Vector3.up * 0.75f, new Vector3(1.6f, 0.05f, 0.9f), drot, j == 1 ? S(Pal.BoatGreen) : wood);
                        mb.Box(tp + Vector3.up * 0.37f, new Vector3(0.1f, 0.74f, 0.1f), drot, grille);
                        foreach (float sx in new[] { -0.55f, 0.55f })
                            foreach (float sz in new[] { -0.75f, 0.75f })
                            {
                                Vector3 cp = tp + dx * sx + dz * sz;
                                mb.Box(cp + Vector3.up * 0.45f, new Vector3(0.48f, 0.06f, 0.48f), drot, S(Pal.WoodDark));
                                mb.Box(cp + Vector3.up * 0.75f + dz * (sz > 0 ? 0.22f : -0.22f), new Vector3(0.48f, 0.6f, 0.05f), drot, S(Pal.WoodDark));
                            }
                        if ((i + j) % 2 == 0)   // a big beige umbrella
                        {
                            mb.Cylinder(tp, 0.04f, 0.04f, 2.6f, 5, cream, false);
                            mb.Cylinder(tp + Vector3.up * 2.2f, 1.7f, 0.1f, 0.55f, 8, S(Pal.PantsKhaki), true, true);
                        }
                    }
                int fb = Mathf.FloorToInt((deckW - 2f) / 3f);
                for (int k = 0; k < fb; k++)   // flower boxes along the wall side
                    mb.Box(dc + dx * ((k - (fb - 1) * 0.5f) * 3f) + dz * (deckD * 0.5f - 0.6f) + Vector3.up * 0.3f, new Vector3(2.4f, 0.6f, 0.5f), drot, S(k % 2 == 0 ? Pal.JacketRed : Pal.BoatGreen));

                // the jetty, straight out from the deck's lake edge, a step down to the water; the club's green-hulled sail
                // boats tied along both sides, bows out
                Vector3 jdir = -dz;
                var jr = Quaternion.LookRotation(jdir);
                const float JL = 18f;
                Vector3 j0 = new Vector3(jettyRoot.x, ctx.lakeY + 0.55f, jettyRoot.z);
                mb.Box(new Vector3(jettyRoot.x, (floor + ctx.lakeY + 0.55f) * 0.5f, jettyRoot.z) + jdir * 0.6f, new Vector3(2.2f, 0.2f, 1.6f), Quaternion.LookRotation(jdir, Vector3.up) * Quaternion.Euler(-25f, 0f, 0f), wood);   // the step down
                mb.Box(j0 + jdir * (JL * 0.5f + 1.2f), new Vector3(2.2f, 0.25f, JL), jr, wood);
                col.Box(j0 + jdir * (JL * 0.5f + 1.2f), new Vector3(2.2f, 0.25f, JL), jr, Vector2.zero);
                for (float z = 2f; z < JL; z += 3f)
                    foreach (float s in new[] { -1f, 1f })
                        mb.Cylinder(j0 + jdir * (z + 1.2f) + dx * (s * 1.05f) + Vector3.down * 2.5f, 0.12f, 0.12f, 3.2f, 6, S(Pal.WoodDark), false);
                for (int k = 0; k < 6; k++)
                {
                    float s = k % 2 == 0 ? 1f : -1f;
                    Vector3 bp = j0 + jdir * (4f + (k / 2) * 5f) + dx * (s * 2.4f) + Vector3.down * 0.5f;
                    var brot = Quaternion.LookRotation(dx * s);   // bows out from the jetty, side by side
                    using (PropKit.At(mb, bp, brot))
                    {
                        mb.Sphere(Vector3.zero, new Vector3(0.9f, 0.45f, 2.6f), 8, 4, green, -90f, 0f);
                        mb.Box(Vector3.up * 0.05f, new Vector3(1.6f, 0.08f, 4.6f), S(Pal.ShirtWhite));
                        mb.Cylinder(Vector3.up * 0.05f, 0.06f, 0.05f, 7f, 5, cream, false);
                    }
                }
            }

            ctx.MeshObject("Boat House Club", parent, mb, ctx.mats.atlas, col);
            ctx.InfoSign("Boat House Club", Info["Boat House Club"], new Vector3(ClubWings[0].a.x - 2f, ctx.H(ClubWings[0].a + new Vector2(-2f, 8f)), ClubWings[0].a.y + 8f), Quaternion.identity, parent);
            ctx.Zone("Boat House Club", new Vector3(deckC.x, floor, deckC.y), 35f);
            ctx.landmarkSites["Boat House Club"] = new Vector3(deckC.x, floor, deckC.y);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        static readonly string[] ShopNames =
        {
            "Sharma Sweets", "Kumaon Woolens", "Lake View Cafe", "Pahadi Momos", "Naini Candle House", "Joshi General Store",
            "Bisht Medical", "Shree Ram Bhojnalaya", "Tibetan Handicrafts", "Mountain Tea Point", "Uttarakhand Emporium",
            "Rawat Book Depot", "Negi Electronics", "Bhatt Dhaba", "Himalaya Tours & Travels", "Pine Wood Restaurant",
            "Mamu Ki Chai", "Garhwali Rasoi", "Snow Peak Hotel", "Pandey Photo Studio", "Kumaoni Bal Mithai", "Deodar Guest House",
            "Lakeside Ice Cream", "Mall Road Fashion", "Naina Jewellers", "Gupta Kirana Store", "Hill Queen Bakery",
            "Bhowali Fruit Corner", "Jim Corbett Souvenirs", "Shiv Shakti Mobile", "Krishna Sweet House", "Mountain Bikes Rental",
            "Cafe Chill Out", "Pahadi Achaar Bhandar", "Lake Paradise Hotel", "Kathgodam Travels", "Tea & Maggi Point",
        };

        static float HalfSize(WorldCtx ctx) => ctx.world.meta.sizeM * 0.5f;

        static void BuildStreetDetails(WorldCtx ctx)
        {
            var parent = new GameObject("Street details").transform;
            parent.SetParent(ctx.worldRoot, false);
            var rnd = new System.Random(321);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ---- shop signboards: the board and the shop's name in solid 3D letters, font and colours by the kind of
            // shop its name says (user, 2026-11-18: "tagda 3D name, different stylish font based on their category").
            // Upper Mall's fronts get theirs per shop unit in WorldBuilder.MallFronts.cs.
            var boards = new MeshBuilder();
            var letters = new MallChunks(ctx, parent, "Shop names", ctx.mats.atlas, false, 0.1f) { cellSize = 40f };
            int made = 0, skippedMall = 0;
            var fronts = ctx.shopFronts.FindAll(f => NearMallRoad(ctx, new Vector2(f.pos.x, f.pos.z), 16f));
            int mallFronts = fronts.Count;
            fronts.AddRange(ctx.shopFronts.FindAll(f => !NearMallRoad(ctx, new Vector2(f.pos.x, f.pos.z), 16f)));
            foreach (var (pos, n, width) in fronts)
            {
                if (made >= 700) break;
                var p2 = new Vector2(pos.x, pos.z);
                if (OnUpperMall(ctx, p2, 9f)) { skippedMall++; continue; }
                bool mall = made < mallFronts;
                Vector3 at = mall ? pos + Vector3.up * 0.65f : pos;
                float bw = mall ? Mathf.Min(width * 0.96f, 8f) : Mathf.Min(width * 0.85f, 6.5f);
                float bh = mall ? 1.05f : 0.62f;
                var rot = Quaternion.LookRotation(n);
                string name = ShopNames[rnd.Next(ShopNames.Length)];
                var style = Glyph3D.StyleForName(name);
                Glyph3D.Pick(style, rnd, out int board, out Vector2 front, out Vector2 side);
                boards.Box(at, new Vector3(bw, bh, 0.08f), rot, Atlas.SolidUV(board));
                Glyph3D.Write(letters.At(p2)[0], name, style.face, at + n * 0.04f, n, bh * 0.42f, 0.05f, bw * 0.9f, front, side);
                made++;
            }
            ctx.MeshObject("Shop boards", parent, boards, ctx.mats.atlas, null, false);
            letters.Flush();
            Debug.Log($"[Nainital] shop signs: {made} boards with 3D names ({skippedMall} Upper Mall fronts left to MallFronts)");

            // ---- electricity poles and sagging wires along town roads
            var poles = new MeshBuilder();
            var wires = new MeshBuilder();
            Vector2 wireUV = Atlas.SolidUV(Pal.IronBlack);
            foreach (var r in ctx.world.roads)
            {
                if (r.kind != "residential" && r.kind != "tertiary" && r.kind != "secondary" && r.kind != "unclassified") continue;
                var pts = Resample(Geo.ToPoints(r.pts), 34f, false);
                Vector3[] prev = null;
                for (int i = 1; i < pts.Count - 1; i++)
                {
                    Vector2 p = pts[i];
                    if (ctx.InLake(p) || ctx.LakeDistance(p) < 20f) { prev = null; continue; }
                    Vector2 tan = (pts[i + 1] - pts[i - 1]).normalized;
                    // right side (lamps alternate both sides); the left where the right is another road, none if both are
                    if (!PoleSpot(ctx, p, new Vector2(tan.y, -tan.x), r.width * 0.5f + 1.1f, out var q, out _)) { prev = null; continue; }
                    var basePos = new Vector3(q.x, ctx.H(q), q.y);
                    var rot = Quaternion.LookRotation(new Vector3(tan.x, 0, tan.y)) * Quaternion.Euler(0, 90, 0);
                    Vector3[] heads;
                    using (PropKit.At(poles, basePos, rot)) heads = PropKit.ElectricPole(poles);
                    var world = new Vector3[3];
                    for (int k = 0; k < 3; k++) world[k] = basePos + rot * heads[k];
                    if (prev != null)
                        for (int k = 0; k < 3; k++)
                        {
                            Vector3 a = prev[k], b = world[k];
                            float sag = Vector3.Distance(a, b) * 0.035f;
                            Vector3 last = a;
                            for (int s = 1; s <= 6; s++)
                            {
                                float t = s / 6f;
                                Vector3 pnt = Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1 - t);
                                wires.Limb(last, pnt, 0.012f, 0.012f, 3, wireUV, false);
                                last = pnt;
                            }
                        }
                    prev = world;
                }
            }
            ctx.MeshObject("Electric poles", parent, poles, ctx.mats.atlas);
            ctx.MeshObject("Electric wires", parent, wires, ctx.mats.atlas, null, false);

            // ---- extra boats moored along the Mallital & Tallital shore
            var moored = new MeshBuilder();
            // mostly yellow (2025 videos of the Mallital boat stand: rows of yellow wooden boats at the steps), a few others
            int[] colors = { Pal.BoatYellow, Pal.BoatYellow, Pal.BoatYellow, Pal.BoatRed, Pal.BoatYellow, Pal.BoatBlue, Pal.BoatYellow, Pal.BoatGreen };
            // Tallital's row is at the boat stand by the fork, not at the chowk: the chowk's terrace covers the lake outline
            // there and the boats stood on it (owner, 24 Nov 2026: "boats terrain me ghusi hui hain"; WorldBuilder.AmbedkarPark.cs)
            Vector2 tallitalBoats = ShorePoint(ctx, TallitalBoatsAt, out var tbp, out _) ? tbp : ctx.tallitalShore;
            foreach (var (shore, count) in new[] { (ctx.mallitalShore, 16), (tallitalBoats, 10) })
            {
                // REFERENCE_REPORT (v02 3:14): the boats are tied straight out from the ghat, shoulder to shoulder, so each
                // one points along the real shore's normal where it lies, not at the lake's centre, and barely swings
                Vector2 o0 = (shore - ctx.lakeCenter).normalized;
                Vector2 along0 = new Vector2(-o0.y, o0.x);
                for (int i = 0; i < count; i++)   // shoulder to shoulder, more of them (was 12)
                {
                    Vector2 edge = shore + along0 * (i - (count - 1) * 0.5f) * 1.9f;
                    Vector2 near = NearestOnLakeEdge(ctx, edge);
                    Vector2 toLake = ctx.InLake(near + (ctx.lakeCenter - near).normalized * 2f) ? (ctx.lakeCenter - near).normalized : -o0;
                    // the shore's own direction here, from its points a little either side; the normal is the side that is water
                    Vector2 l = NearestOnLakeEdge(ctx, near + along0 * 1.5f), r = NearestOnLakeEdge(ctx, near - along0 * 1.5f);
                    Vector2 tan = (l - r).normalized;
                    Vector2 nrm = new Vector2(-tan.y, tan.x);
                    if (!ctx.InLake(near + nrm * 2f)) nrm = -nrm;
                    if (tan.sqrMagnitude < 0.5f) nrm = toLake;
                    Vector2 p = near + nrm * 3.4f;
                    for (float out2 = 3.4f; out2 <= 8.5f && ctx.H(p) > ctx.lakeY - 0.3f; out2 += 1.7f) p = near + nrm * out2;   // shallow: tied a bit further out
                    if (!ctx.InLake(p) || ctx.LakeDistance(p) < 2.5f) continue;
                    if (ctx.H(p) > ctx.lakeY - 0.3f || NearJetty(ctx, near, 5f)) continue;   // never on the ground, never across a jetty
                    using (PropKit.At(moored, new Vector3(p.x, ctx.lakeY + 0.03f, p.y), Quaternion.LookRotation(new Vector3(nrm.x, 0, nrm.y)) * Quaternion.Euler(0, (float)(rnd.NextDouble() - 0.5) * 4f, 0)))
                        PropKit.RowBoat(moored, colors[rnd.Next(colors.Length)], rnd.NextDouble() < 0.6);
                }
            }
            Bobbing(new MeshBuilder()).Append(moored);   // they rise and fall on the swell (FestiveStep emits the bob mesh)

            // ---- welcome gate on the highway into town
            foreach (var r in ctx.world.roads)
            {
                if (string.IsNullOrEmpty(r.name) || (r.name.IndexOf("Haldwani", System.StringComparison.OrdinalIgnoreCase) < 0 && r.kind != "trunk")) continue;
                var pts = Geo.ToPoints(r.pts);
                Vector2 best = pts[0];
                float bd = float.MaxValue;
                foreach (var p in pts)
                {
                    float d = Mathf.Abs((p - ctx.lakeCenter).magnitude - 1250f);
                    if (d < bd) { bd = d; best = p; }
                }
                if (bd > 300f) continue;
                int bi = System.Array.IndexOf(pts, best);
                Vector2 tan = (pts[Mathf.Min(bi + 1, pts.Length - 1)] - pts[Mathf.Max(bi - 1, 0)]).normalized;
                var gate = new MeshBuilder();
                float halfW = r.width * 0.5f + 1.2f;
                var rot = Quaternion.LookRotation(new Vector3(tan.x, 0, tan.y));
                var basePos = new Vector3(best.x, ctx.H(best), best.y);
                using (PropKit.At(gate, basePos, rot))
                {
                    foreach (float x in new[] { -halfW, halfW })
                    {
                        gate.Box(new Vector3(x, 3f, 0), new Vector3(0.9f, 6f, 0.9f), Atlas.MaterialUV(Atlas.MatStone));
                        gate.Pyramid(new Vector3(x, 6f, 0), 1.2f, 1.2f, 0.9f, Atlas.SolidUV(Pal.TempleRed));
                    }
                    gate.Box(new Vector3(0, 5.6f, 0), new Vector3(halfW * 2f + 1f, 1.2f, 0.5f), Atlas.SolidUV(Pal.Saffron));
                    gate.Box(new Vector3(0, 6.35f, 0), new Vector3(halfW * 2f + 1.4f, 0.3f, 0.7f), Atlas.SolidUV(Pal.TempleRed));
                }
                // the greeting in solid letters on both faces of the beam (both ways of the road)
                Vector3 beamFwd = rot * Vector3.forward;
                float beamW = halfW * 2f + 0.6f;
                foreach (int s in new[] { -1, 1 })
                {
                    Vector3 face = beamFwd * s, onBeam = basePos + face * 0.25f;
                    Glyph3D.Write(gate, "NAINITAL MEIN AAPKA SWAGAT HAI", Glyph3D.Face.Desi, onBeam + Vector3.up * 5.85f, face, 0.3f, 0.06f, beamW,
                                  Atlas.SolidUV(Pal.TempleRed), Atlas.SolidUV(Pal.WoodDark));
                    Glyph3D.Write(gate, "WELCOME TO NAINITAL", Glyph3D.Face.Royal, onBeam + Vector3.up * 5.35f, face, 0.34f, 0.06f, beamW,
                                  Atlas.SolidUV(Pal.TempleRed), Atlas.SolidUV(Pal.WoodDark));
                }
                ctx.MeshObject("Welcome gate", parent, gate, ctx.mats.atlas, null, true, true);
                ctx.Zone("Nainital Gate", basePos, 30f);
                break;
            }
            Debug.Log($"[Nainital] details: {made} shop signs");
        }

        // ------------------------------------------------------------------ systems for the living game
        static void BuildGameSystems(WorldCtx ctx, Transform root)
        {
            var sys = new GameObject("Game Systems").transform;
            sys.SetParent(root, false);
            sys.gameObject.AddComponent<MissionManager>();
            var weather = sys.gameObject.AddComponent<WeatherSystem>();
            weather.wetMaterials = new[] { ctx.mats.asphalt, ctx.mats.paver };
            var audio = sys.gameObject.AddComponent<AudioManager>();
            if (ctx.landmarkSites.TryGetValue("Naina Devi Mandir", out var temple)) audio.templePosition = temple;
            if (ctx.landmarkSites.TryGetValue("Jama Masjid", out var masjid)) audio.mosquePosition = masjid;
            BuildSoundZones(ctx, audio);

            var birds = new GameObject("Birds & kites").AddComponent<Birds>();
            birds.transform.SetParent(root, false);
            birds.center = new Vector3(ctx.lakeCenter.x, 0, ctx.lakeCenter.y);
            birds.baseHeight = ctx.lakeY;
            birds.material = ctx.mats.atlas;

            // ponies at the viewpoints
            var rnd = new System.Random(12);
            var ponyRoot = new GameObject("Ponies (rideable)").transform;
            ponyRoot.SetParent(root, false);
            int[] coats = { Pal.HairBrown, Pal.WoodDark, Pal.SailWhite, Pal.HairBlack, Pal.WoodLight };
            foreach (var key in LowPolyRidesAndVehicles ? new[] { "Snow View", "Tiffin Top (Dorothy's Seat)", "The Flats" } : new string[0])
            {
                if (!ctx.landmarkSites.TryGetValue(key, out var c)) continue;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 p = new Vector2(c.x, c.z) + new Vector2((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 18f;
                    if (ctx.InLake(p)) continue;
                    var go = new GameObject("Ghoda") { layer = ProjectSetup.CharacterLayer };
                    go.transform.SetParent(ponyRoot, false);
                    go.transform.SetPositionAndRotation(new Vector3(p.x, ctx.H(p) + 0.1f, p.y), Quaternion.Euler(0, rnd.Next(360), 0));
                    go.SetActive(false);
                    var cc = go.AddComponent<CharacterController>();
                    cc.height = 1.6f;
                    cc.radius = 0.45f;
                    cc.center = new Vector3(0, 0.85f, 0);
                    cc.stepOffset = 0.45f;
                    cc.slopeLimit = 50f;
                    go.AddComponent<CharacterMotor>();
                    var pony = go.AddComponent<Pony>();
                    pony.coat = coats[rnd.Next(coats.Length)];
                    pony.material = ctx.mats.atlas;
                    go.SetActive(true);
                }
            }

            var gm = Object.FindAnyObjectByType<GameManager>();
            if (gm)
            {
                gm.markerMaterial = ctx.mats.marker;
                gm.rainMaterial = ctx.mats.rain;
                UnityEditor.EditorUtility.SetDirty(gm);
            }
        }
    }
}

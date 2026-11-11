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

            // ---- shop signboards (board mesh combined, text as 3D TextMesh)
            var boards = new MeshBuilder();
            var signRoot = new GameObject("Shop signs").transform;
            signRoot.SetParent(parent, false);
            if (font)
            {
                var keep = signRoot.gameObject.AddComponent<SignText>();
                keep.font = font;
                keep.material = SignTextMaterial(font);
            }
            int[] boardCols = { Pal.Saffron, Pal.BoatBlue, Pal.Awning, Pal.JacketRed, Pal.BoatGreen, Pal.TempleRed, Pal.JacketBlack, Pal.BoatYellow };
            int made = 0;
            var fronts = ctx.shopFronts.FindAll(f => NearMallRoad(ctx, new Vector2(f.pos.x, f.pos.z), 16f));
            int mallFronts = fronts.Count;
            fronts.AddRange(ctx.shopFronts.FindAll(f => !NearMallRoad(ctx, new Vector2(f.pos.x, f.pos.z), 16f)));
            foreach (var (pos, n, width) in fronts)
            {
                if (made >= 700) break;
                bool mall = made < mallFronts;
                Vector3 at = mall ? pos + Vector3.up * 0.65f : pos;
                float bw = mall ? Mathf.Min(width * 0.96f, 8f) : Mathf.Min(width * 0.85f, 6.5f);
                var rot = Quaternion.LookRotation(n);
                int col = boardCols[rnd.Next(boardCols.Length)];
                boards.Box(at, new Vector3(bw, mall ? 1.05f : 0.62f, 0.08f), rot, Atlas.SolidUV(col));
                if (font)
                {
                    var t = new GameObject("Sign");
                    t.transform.SetParent(signRoot, false);
                    t.transform.SetPositionAndRotation(at + n * 0.05f, Quaternion.LookRotation(-n));
                    var tm = t.AddComponent<TextMesh>();
                    tm.font = font;
                    t.GetComponent<MeshRenderer>().sharedMaterial = SignTextMaterial(font);
                    string name = ShopNames[rnd.Next(ShopNames.Length)];
                    tm.text = name;
                    tm.fontSize = 48;
                    tm.characterSize = Mathf.Min(mall ? 0.095f : 0.055f, bw * 0.9f / (name.Length * 0.55f * 4.8f));
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                    tm.color = col == Pal.BoatYellow || col == Pal.Saffron ? new Color(0.1f, 0.05f, 0.05f) : new Color(1f, 0.95f, 0.8f);
                }
                made++;
            }
            ctx.MeshObject("Shop boards", parent, boards, ctx.mats.atlas, null, false);

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
                    Vector2 side = new Vector2(tan.y, -tan.x);   // right side (lamps alternate both sides)
                    Vector2 q = p + side * (r.width * 0.5f + 1.1f);
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
            int[] colors = { Pal.BoatRed, Pal.BoatBlue, Pal.BoatYellow, Pal.BoatGreen };
            foreach (var shore in new[] { ctx.mallitalShore, ctx.tallitalShore })
            {
                for (int i = 0; i < 9; i++)
                {
                    Vector2 o = (shore - ctx.lakeCenter).normalized;
                    Vector2 along = new Vector2(-o.y, o.x);
                    Vector2 p = shore - o * 7f + along * (i - 4f) * 3.2f;
                    if (!ctx.InLake(p) || ctx.LakeDistance(p) < 3f) continue;
                    using (PropKit.At(moored, new Vector3(p.x, ctx.lakeY + 0.03f, p.y), Quaternion.LookRotation(new Vector3(-o.x, 0, -o.y)) * Quaternion.Euler(0, rnd.Next(-12, 12), 0)))
                        PropKit.RowBoat(moored, colors[rnd.Next(colors.Length)], rnd.NextDouble() < 0.6);
                }
            }
            ctx.MeshObject("Moored boats", parent, moored, ctx.mats.atlas);

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
                var gateGo = ctx.MeshObject("Welcome gate", parent, gate, ctx.mats.atlas, null, true, true);
                if (font)
                    foreach (int s in new[] { -1, 1 })
                    {
                        var t = new GameObject("Gate text");
                        t.transform.SetParent(gateGo.transform, false);
                        Vector3 fwd = rot * Vector3.forward;
                        t.transform.SetPositionAndRotation(basePos + Vector3.up * 5.6f + fwd * (0.27f * s), Quaternion.LookRotation(fwd * s));
                        var tm = t.AddComponent<TextMesh>();
                        tm.font = font;
                        t.GetComponent<MeshRenderer>().sharedMaterial = SignTextMaterial(font);
                        tm.text = "NAINITAL MEIN AAPKA SWAGAT HAI\nWELCOME TO NAINITAL";
                        tm.fontSize = 48;
                        tm.characterSize = 0.045f;
                        tm.anchor = TextAnchor.MiddleCenter;
                        tm.alignment = TextAlignment.Center;
                        tm.color = new Color(0.25f, 0.05f, 0.02f);
                    }
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

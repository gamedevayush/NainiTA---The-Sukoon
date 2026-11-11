using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        static readonly HashSet<string> VehicleKinds = new HashSet<string>
            { "trunk", "trunk_link", "primary", "secondary", "tertiary", "unclassified", "residential", "living_street", "service" };

        /// <summary>Every road, lane, footpath and the lake promenade: grass must not grow on them.</summary>
        static void FillGrassMask(WorldCtx ctx, GrassRenderer grass)
        {
            var pts = new List<Vector2>();
            var starts = new List<int>();
            var half = new List<float>();
            foreach (var r in ctx.world.roads)
            {
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                starts.Add(pts.Count);
                pts.AddRange(line);
                half.Add(Mathf.Max(1f, r.width * 0.5f) + (r.name == MallUpper ? 4.2f : r.name == MallLower ? 10f : 0f));
            }
            if (ctx.promenadePath != null && ctx.promenadePath.Count > 1)
            {
                starts.Add(pts.Count);
                foreach (var p in ctx.promenadePath) pts.Add(new Vector2(p.x, p.z));
                half.Add(3.5f);
            }
            AddFlatsToGrassMask(ctx, pts, starts, half);
            grass.noGrassPts = pts.ToArray();
            grass.noGrassStarts = starts.ToArray();
            grass.noGrassHalfWidth = half.ToArray();
        }

        /// <summary>Code-built horses and vehicles (ponies, parked cars, buses, taxis, AI traffic). Off until the user's own
        /// models replace them (user, 2026-11-11: the low-poly ones look bad).</summary>
        public const bool LowPolyRidesAndVehicles = false;

        /// <summary>The user's real vehicle models (VehicleKit, 2026-11-11) bring the traffic and parked cars back.</summary>
        static VehicleLibrary RealVehicles()
        {
            var lib = AssetDatabase.LoadAssetAtPath<VehicleLibrary>("Assets/Resources/VehicleLibrary.asset");
            return lib && lib.entries != null && lib.entries.Length > 0 ? lib : null;
        }

        static void BuildLivingTown(WorldCtx ctx)
        {
            var root = new GameObject("Living Town").transform;
            root.SetParent(ctx.gameplayRoot, false);
            var net = BuildRoadNetwork(ctx);

            var traffic = new GameObject("Traffic (AI cars)").AddComponent<TrafficManager>();
            traffic.transform.SetParent(root, false);
            traffic.network = net;
            traffic.material = ctx.mats.atlas;
            var realCars = RealVehicles();
            if (!LowPolyRidesAndVehicles && !realCars) traffic.maxCars = 0;   // no models: no code-built cars either

            var peds = new GameObject("Pedestrians (AI crowd)").AddComponent<PedestrianManager>();
            peds.transform.SetParent(root, false);
            peds.network = net;
            peds.material = ctx.mats.atlas;
            FillBrowseSpots(ctx, peds);
            FillHomeSpots(ctx, peds);

            var grass = new GameObject("Grass (GPU instanced)").AddComponent<GrassRenderer>();
            grass.transform.SetParent(ctx.worldRoot, false);
            grass.terrain = ctx.terrain;
            grass.material = ctx.mats.atlas;
            FillGrassMask(ctx, grass);

            if (LowPolyRidesAndVehicles || realCars) BuildParkedVehicles(ctx, root, realCars);
            BuildLocals(ctx, root);
            BuildVendors(ctx, root);
            BuildNightGangs(ctx, root);
            BuildMonkeys(ctx, root);
            BuildGameSystems(ctx, root);

            // radar data
            var gm = Object.FindAnyObjectByType<GameManager>();
            if (gm)
            {
                gm.minimap = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Data/minimap.png");
                gm.worldSize = ctx.world.meta.sizeM;
                gm.worldCx = ctx.world.meta.mapCx;
                gm.worldCz = ctx.world.meta.mapCz;
                var names = new List<string>();
                var pos = new List<Vector3>();
                foreach (var kv in ctx.landmarkSites) { names.Add(kv.Key); pos.Add(kv.Value); }
                gm.blipNames = names.ToArray();
                gm.blipPositions = pos.ToArray();
                EditorUtility.SetDirty(gm);
            }
        }

        /// <summary>A door for every house (front middle, a step out, on the ground): where walkers go in at night.</summary>
        static void FillHomeSpots(WorldCtx ctx, PedestrianManager peds)
        {
            var doors = new List<Vector3>();
            var h = ctx.world.houses;
            for (int i = 0; i + 8 < h.Length; i += 9)
            {
                Vector2 c = new Vector2(h[i], h[i + 1]);
                float d = h[i + 3], yaw = h[i + 4];
                Vector3 f3 = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                Vector2 door = c + new Vector2(f3.x, f3.z) * (d * 0.5f + 0.5f);
                if (ctx.InLake(door)) continue;
                doors.Add(new Vector3(door.x, ctx.H(door), door.y));
            }
            foreach (var (pos, n, width) in ctx.shopFronts) doors.Add(pos - Vector3.up * 2.75f + new Vector3(n.x, 0, n.z) * 0.4f);
            peds.homeSpots = doors.ToArray();
            Debug.Log($"[Nainital] homes: {doors.Count} doors for walkers to go in at night");
        }

        /// <summary>Where shoppers stand: a step or two in front of every shop front (Mall Road, the bazaars, Tallital),
        /// looking in, and round the market stalls.</summary>
        static void FillBrowseSpots(WorldCtx ctx, PedestrianManager peds)
        {
            var spots = new List<Vector3>();
            var faces = new List<Vector3>();
            var rnd = new System.Random(4242);
            foreach (var (pos, n, width) in ctx.shopFronts)
            {
                Vector3 nf = new Vector3(n.x, 0f, n.z).normalized;
                Vector3 along = Vector3.Cross(Vector3.up, nf);
                int k = width > 5f ? 2 : 1;
                for (int j = 0; j < k; j++)
                {
                    Vector3 p = pos + nf * (0.9f + (float)rnd.NextDouble() * 0.9f) + along * ((float)rnd.NextDouble() - 0.5f) * width * 0.7f;
                    Vector2 q = new Vector2(p.x, p.z);
                    if (ctx.InLake(q)) continue;
                    float g = ctx.H(q);
                    if (Mathf.Abs(g - (pos.y - 2.75f)) > 1.2f) continue;   // a drop or a wall in front: no one stands there
                    spots.Add(new Vector3(p.x, g + 0.05f, p.z));
                    Vector3 f = -nf + along * ((float)rnd.NextDouble() - 0.5f) * 0.6f;
                    faces.Add(f.normalized);
                }
            }
            foreach (var key in new[] { "Bada Bazar", "Tibetan Market" })
            {
                if (!ctx.landmarkSites.TryGetValue(key, out var c)) continue;
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2f;
                    Vector2 q = new Vector2(c.x + Mathf.Cos(a) * 6f, c.z + Mathf.Sin(a) * 4f);
                    if (ctx.InLake(q)) continue;
                    spots.Add(new Vector3(q.x, ctx.H(q) + 0.05f, q.y));
                    faces.Add(new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a)));
                }
            }
            peds.browseSpots = spots.ToArray();
            peds.browseFacing = faces.ToArray();
            Debug.Log($"[Nainital] shoppers: {spots.Count} browse spots at {ctx.shopFronts.Count} shop fronts");
        }

        // ------------------------------------------------------------------ road + footpath graph
        static RoadNetwork BuildRoadNetwork(WorldCtx ctx)
        {
            var roads = new List<RoadNetwork.Path>();
            var walks = new List<RoadNetwork.Path>();
            foreach (var r in ctx.world.roads)
            {
                var pts2 = Geo.ToPoints(r.pts);
                if (pts2.Length < 2) continue;
                if (VehicleKinds.Contains(r.kind))
                {
                    var res = Resample(pts2, 4f, false);
                    if (res.Count >= 4)
                    {
                        var p3 = new Vector3[res.Count];
                        for (int i = 0; i < res.Count; i++) p3[i] = new Vector3(res[i].x, ctx.H(res[i]) + 0.1f, res[i].y);
                        roads.Add(new RoadNetwork.Path { name = r.name, width = r.width, pts = p3 });
                    }
                    if (r.name == MallUpper)   // the crowd walks on Upper Mall itself
                        for (float lane = -2.2f; lane <= 2.21f; lane += 0.73f)
                        {
                            var dense = Resample(pts2, 4f, false);
                            var lp = new Vector3[dense.Count];
                            for (int i = 0; i < dense.Count; i++)
                            {
                                Vector2 tan = (dense[Mathf.Min(i + 1, dense.Count - 1)] - dense[Mathf.Max(i - 1, 0)]).normalized;
                                Vector2 q = dense[i] + new Vector2(-tan.y, tan.x) * lane;
                                lp[i] = new Vector3(q.x, ctx.H(dense[i]) + 0.16f, q.y);
                            }
                            walks.Add(new RoadNetwork.Path { name = r.name + " crowd", width = 1.2f, pts = lp });
                        }
                    // sidewalks both sides (skip lake-side bits)
                    if (r.kind != "trunk" && r.kind != "service")
                        foreach (float side in new[] { -1f, 1f })
                        {
                            var sw = new List<Vector3>();
                            var res2 = Resample(pts2, 4f, false);
                            for (int i = 0; i < res2.Count; i++)
                            {
                                Vector2 tan = (res2[Mathf.Min(i + 1, res2.Count - 1)] - res2[Mathf.Max(i - 1, 0)]).normalized;
                                Vector2 q = res2[i] + new Vector2(-tan.y, tan.x) * side * (r.width * 0.5f + 1.6f);
                                bool ok = !ctx.InLake(q) && ctx.LakeDistance(q) > 4f && Mathf.Abs(ctx.H(q) - ctx.H(res2[i])) < 1.2f;
                                if (ok) sw.Add(new Vector3(q.x, ctx.H(q) + 0.05f, q.y));
                                else if (sw.Count >= 6) { walks.Add(new RoadNetwork.Path { name = r.name + " footpath", width = 1.5f, pts = sw.ToArray() }); sw = new List<Vector3>(); }
                                else sw.Clear();
                            }
                            if (sw.Count >= 6) walks.Add(new RoadNetwork.Path { name = r.name + " footpath", width = 1.5f, pts = sw.ToArray() });
                        }
                }
                else if (r.kind == "pedestrian" || r.kind == "footway" || r.kind == "steps" || r.kind == "path")
                {
                    var res = Resample(pts2, 3f, false);
                    if (res.Count < 4) continue;
                    var p3 = new Vector3[res.Count];
                    for (int i = 0; i < res.Count; i++) p3[i] = new Vector3(res[i].x, ctx.H(res[i]) + 0.05f, res[i].y);
                    walks.Add(new RoadNetwork.Path { name = r.name, width = r.width, pts = p3 });
                }
            }
            if (ctx.promenadePath != null && ctx.promenadePath.Count > 10)
                walks.Add(new RoadNetwork.Path { name = "Lake promenade", width = 4f, pts = ctx.promenadePath.ToArray() });

            string path = Gen + "/RoadNetwork.asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var net = ScriptableObject.CreateInstance<RoadNetwork>();
            net.roads = roads.ToArray();
            net.walks = walks.ToArray();
            AssetDatabase.CreateAsset(net, path);
            Debug.Log($"[Nainital] road network: {roads.Count} drivable roads, {walks.Count} walkways");
            return net;
        }

        // ------------------------------------------------------------------ parked cars, buses, taxis
        static void BuildParkedVehicles(WorldCtx ctx, Transform root, VehicleLibrary lib)
        {
            var model = new List<int>();
            Physics.SyncTransforms();
            var pos = new List<Vector3>();
            var rot = new List<Quaternion>();
            var type = new List<int>();
            var paint = new List<int>();
            var rnd = new System.Random(808);
            int mask = ~((1 << 4) | (1 << 2));

            bool Free(Vector3 p, Quaternion r, Vector3 half)
            {
                foreach (var q in pos) if ((q - p).sqrMagnitude < 36f) return false;
                foreach (var h in Physics.OverlapBox(p + Vector3.up * (half.y + 0.3f), half, r, mask, QueryTriggerInteraction.Ignore))
                    if (!(h is TerrainCollider)) return false;
                return true;
            }

            void Add(Vector3 p, Quaternion r, CarType t, int c, int m = -2)
            {
                if (m == -2) m = lib ? lib.Pick(t, (float)rnd.NextDouble()) : -1;
                pos.Add(p); rot.Add(r); type.Add((int)t); paint.Add(c); model.Add(m);
            }
            int Named(string n) { if (lib) for (int i = 0; i < lib.entries.Length; i++) if (lib.entries[i].name == n) return i; return -1; }

            Quaternion OnGround(Vector2 p, Vector2 fwd)
            {
                Vector3 f3 = new Vector3(fwd.x, 0, fwd.y);
                float hf = ctx.H(p + fwd * 1.5f), hb = ctx.H(p - fwd * 1.5f);
                f3.y = (hf - hb) / 3f;
                return Quaternion.LookRotation(f3.normalized, Vector3.up);
            }

            // along town roads, on the left shoulder
            foreach (var r in ctx.world.roads)
            {
                if (r.kind != "residential" && r.kind != "tertiary" && r.kind != "secondary" && r.kind != "unclassified" && r.kind != "primary") continue;
                var pts = Resample(Geo.ToPoints(r.pts), 30f, false);
                for (int i = 1; i < pts.Count - 1 && pos.Count < 46; i++)
                {
                    if (rnd.NextDouble() > 0.22) continue;
                    Vector2 tan = (pts[i + 1] - pts[i - 1]).normalized;
                    Vector2 left = new Vector2(-tan.y, tan.x);
                    Vector2 p = pts[i] + left * (r.width * 0.5f + 1.3f);
                    if (ctx.InLake(p) || ctx.LakeDistance(p) < 9f || Mathf.Abs(ctx.H(p) - ctx.H(pts[i])) > 0.7f) continue;
                    var q = OnGround(p, tan);
                    var gp = new Vector3(p.x, ctx.H(p) + 0.05f, p.y);
                    if (!Free(gp, q, new Vector3(1f, 0.7f, 2.3f))) continue;
                    float roll = (float)rnd.NextDouble();
                    var t = roll < 0.3f ? CarType.Hatchback : roll < 0.45f ? CarType.Sedan : roll < 0.65f ? CarType.Suv : roll < 0.88f ? CarType.Taxi : CarType.Pickup;
                    if (lib && !lib.Has(t)) t = CarType.Taxi;
                    Add(gp, q, t, t == CarType.Taxi || t == CarType.Pickup ? Px.CarWhite : VehicleFactory.CarPaints[rnd.Next(VehicleFactory.CarPaints.Length)]);
                }
            }
            // Tallital bus stand: buses + taxi rank
            if (ctx.hasBusStand)
            {
                for (int b = 0; b < 4; b++)   // nose-in under the canopy, side by side
                {
                    Vector3 p = ctx.busStand + ctx.busStandRot * new Vector3(-9f + b * 4.6f, 0, 0.5f);
                    p.y = ctx.H(new Vector2(p.x, p.z));
                    Add(p, ctx.busStandRot, CarType.Bus, b % 2 == 0 ? Px.BusWhite : Px.BusBlue, lib ? Named(b % 2 == 0 ? "TouristBus" : "TouristCoach") : -1);
                }
                for (int t = 0; t < 3; t++)
                {
                    Vector3 p = ctx.busStand + ctx.busStandRot * new Vector3(11f + t * 3f, 0, 0.5f);
                    p.y = ctx.H(new Vector2(p.x, p.z));
                    Add(p, ctx.busStandRot, CarType.Taxi, Px.CarWhite);
                }
            }
            if (pos.Count == 0) return;
            var go = new GameObject("Parked vehicles (" + pos.Count + ", drivable)");
            go.transform.SetParent(root, false);
            go.SetActive(false);
            var pv = go.AddComponent<ParkedVehicles>();
            pv.material = ctx.mats.atlas;
            pv.positions = pos.ToArray();
            pv.rotations = rot.ToArray();
            pv.types = type.ToArray();
            pv.paints = paint.ToArray();
            pv.models = model.ToArray();
            go.SetActive(true);
        }

        // ------------------------------------------------------------------ people doing things
        /// <param name="elder">true = an elderly local, false = a working-age adult, null = anyone (incl. children)</param>
        static GameObject Local(WorldCtx ctx, Transform parent, string name, Vector3 pos, Vector3 face, bool sit, bool female, System.Random rnd,
                                bool? elder = false)
        {
            int[] tops = { Pal.SareeOrange, Pal.SareePink, Pal.Shawl, Pal.JacketRed, Pal.ShirtWhite, Pal.CapWool, Pal.JacketGreen };
            var look = PickLook(female, elder.HasValue ? false : (bool?)null, elder, rnd);
            var npc = Character(ctx, name, parent, pos, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : Vector3.forward), ProjectSetup.CharacterLayer,
                                new[] { Pal.SkinLight, Pal.SkinMid, Pal.SkinDark }[rnd.Next(3)], Pal.HairBlack, tops[rnd.Next(tops.Length)],
                                female ? Pal.SareeOrange : Pal.PantsKhaki, Pal.ShoeBrown, female, !female && rnd.NextDouble() < 0.5, female, look);
            var w = npc.AddComponent<NpcWalker>();
            w.npcName = name;
            w.homeAtNight = true;   // shopkeepers, boatmen, vendors, temple-goers: gone from midnight till six
            w.sit = sit;
            w.idle = !sit;
            w.faceDirection = face;
            return npc;
        }

        static void BuildLocals(WorldCtx ctx, Transform root)
        {
            var parent = new GameObject("Locals (temple, boats, shops)").transform;
            parent.SetParent(root, false);
            var rnd = new System.Random(515);
            string[] templeLines =
            {
                "Jai Maa Naina Devi! Yahan sachche mann se jo maango, milta hai.",
                "Navratri mein yahan bahut bada mela lagta hai.",
                "Prasad le lo beta, Maa ka aashirwad hai.",
            };
            if (ctx.landmarkSites.TryGetValue("Naina Devi Mandir", out var temple))
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = temple + new Vector3(-3f + i * 2f, 0.05f, 6.5f);
                    var n = Local(ctx, parent, "Bhakt", p, (temple - p).normalized, true, i % 2 == 0, rnd, elder: i != 3);
                    n.GetComponent<NpcWalker>().lines = templeLines;
                }
            string[] boatLines =
            {
                "Naav chahiye saab? Ek ghanta, poori jheel ghuma dunga!",
                "Yacht wali sailing Boat Club se hoti hai, wo alag hai.",
                "Shaam ko jheel sabse sundar lagti hai, abhi le lo naav.",
            };
            foreach (var j in ctx.jettyRoots)
            {
                Vector2 o = (j - ctx.lakeCenter).normalized;
                Vector2 p = j + o * 3f + new Vector2(-o.y, o.x) * 2.5f;
                var n = Local(ctx, parent, "Naav wale bhaiya", new Vector3(p.x, ctx.H(p) + 0.05f, p.y), new Vector3(-o.x, 0, -o.y), false, false, rnd);
                n.GetComponent<NpcWalker>().lines = boatLines;
            }
            string[] shopLines =
            {
                "Aao ji, garam shawl dekh lo, Kashmir se aaya hai!",
                "Nainital ki special candle le jao, ghar pe sab khush ho jayenge.",
                "Sahi daam lagaunga, aap toh apne hi ho.",
            };
            foreach (var key in new[] { "Bada Bazar", "Tibetan Market" })
            {
                if (!ctx.landmarkSites.TryGetValue(key, out var c)) continue;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = c + new Vector3(-4f + i * 4f, 0.05f, (i % 2 == 0 ? 1f : -1f) * 2.2f);
                    var n = Local(ctx, parent, key == "Bada Bazar" ? "Dukaandaar" : "Tibetan dukaandaar", p, new Vector3(0, 0, i % 2 == 0 ? -1f : 1f), false, i == 1, rnd);
                    n.GetComponent<NpcWalker>().lines = shopLines;
                }
            }
            if (ctx.landmarkSites.TryGetValue("Snow View", out var sv))
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = sv + new Vector3(-2f + i * 2f, 0.05f, 3.5f);
                    Vector2 toLake = (ctx.lakeCenter - new Vector2(sv.x, sv.z)).normalized;
                    Local(ctx, parent, "Tourist (view dekh raha)", p, new Vector3(toLake.x, 0, toLake.y), false, i == 1, rnd, elder: null);
                }
        }

        static void BuildMonkeys(WorldCtx ctx, Transform root)
        {
            var parent = new GameObject("Bandar (monkeys)").transform;
            parent.SetParent(root, false);
            var rnd = new System.Random(99);
            void Make(Vector2 p)
            {
                if (ctx.InLake(p)) return;
                var go = new GameObject("Bandar") { layer = ProjectSetup.CharacterLayer };
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(new Vector3(p.x, ctx.H(p) + 0.1f, p.y), Quaternion.Euler(0, rnd.Next(360), 0));
                var cc = go.AddComponent<CharacterController>();
                cc.height = 0.9f;
                cc.radius = 0.18f;
                cc.center = new Vector3(0, 0.47f, 0);
                cc.stepOffset = 0.3f;
                cc.slopeLimit = 60f;
                go.AddComponent<CharacterMotor>();
                var rigGo = new GameObject("Rig") { layer = ProjectSetup.CharacterLayer };
                rigGo.transform.SetParent(go.transform, false);
                rigGo.SetActive(false);
                var rig = rigGo.AddComponent<HumanoidRig>();
                rig.scale = 0.48f;
                rig.skin = Pal.SkinLight;
                rig.hair = rig.top = rig.legs = rig.shoes = Pal.HairBrown;
                rig.tail = true;
                rig.material = ctx.mats.atlas;
                rigGo.SetActive(true);
                go.AddComponent<Monkey>();
            }
            void Spot(string key, int n)
            {
                if (!ctx.landmarkSites.TryGetValue(key, out var c)) return;
                for (int i = 0; i < n; i++)
                    Make(new Vector2(c.x, c.z) + new Vector2((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 14f);
            }
            Spot("Naina Devi Mandir", 4);
            Spot("Snow View", 3);
            Spot("Tiffin Top (Dorothy's Seat)", 2);
            Spot("Pashan Devi Mandir", 2);
            if (ctx.promenadePath != null && ctx.promenadePath.Count > 20)
                for (int i = 0; i < 4; i++)
                {
                    var q = ctx.promenadePath[rnd.Next(ctx.promenadePath.Count)];
                    Make(new Vector2(q.x, q.z));
                }
        }

        static void BuildNightGangs(WorldCtx ctx, Transform root)
        {
            var parent = new GameObject("Night gangs").transform;
            parent.SetParent(root, false);
            var rnd = new System.Random(66);
            int made = 0;
            foreach (var r in ctx.world.roads)
            {
                if (made >= 4) break;
                if (r.kind != "residential" || rnd.NextDouble() > 0.25) continue;
                var pts = Geo.ToPoints(r.pts);
                Vector2 p = pts[pts.Length / 2];
                if (ctx.LakeDistance(p) < 60f || ctx.LakeDistance(p) > 700f) continue;
                for (int i = 0; i < 2; i++)
                {
                    Vector2 q = p + new Vector2(i * 2f, 1.5f);
                    var e = Character(ctx, "Raat ka Gunda " + (made * 2 + i + 1), parent, GroundNear(ctx, q), Quaternion.identity, ProjectSetup.CharacterLayer,
                                      Pal.SkinDark, Pal.HairBlack, Pal.JacketBlack, Pal.PantsBlack, Pal.ShoeBrown, false, true, false);
                    var h = e.AddComponent<Health>();
                    h.max = h.current = 60f;
                    var c = e.AddComponent<Combatant>();
                    c.hitMask = 1 << ProjectSetup.PlayerLayer;
                    c.damageMultiplier = 0.9f;
                    var ai = e.AddComponent<EnemyAI>();
                    ai.displayName = "Raat ka Gunda";
                    ai.nightOnly = true;
                }
                made++;
            }
        }
    }
}

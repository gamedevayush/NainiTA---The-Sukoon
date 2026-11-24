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
            grass.material = WindAtlasMaterial(ctx, "M_GrassWind", 0.09f, 2.2f, 0.03f);   // the grass sways too (23 Nov)
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

        /// <summary>Junction islands the traffic drives round (Tallital's police island: TrafficPost adds it).</summary>
        static readonly List<RoadNetwork.Island> trafficIslands = new List<RoadNetwork.Island>();

        /// <summary>
        /// Where each drivable road's ends meet the others, as the map has it: an end on another road's line is a junction
        /// whatever the slope; an end that lies on none joins only the nearest road within 12 m at about its height.
        /// (22 Nov 2026: "everything within 12 m" joined Snowview Road to Lower Mall through the divider wall and cut
        /// corners across junctions; the bake also left out two connectors under 12 m, Kunjakharak Road's among them.)
        /// </summary>
        static RoadNetwork.Meeting[] RoadJoins(List<RoadNetwork.Path> roads, List<Vector2[]> map)
        {
            var found = new List<RoadNetwork.Meeting>();
            int onMap = 0, byNearness = 0;
            int Closest(Vector3[] q, Vector2 p)
            {
                int b = 0;
                float bd = float.MaxValue;
                for (int i = 0; i < q.Length; i++) { float d = (new Vector2(q[i].x, q[i].z) - p).sqrMagnitude; if (d < bd) { bd = d; b = i; } }
                return b;
            }
            for (int r = 0; r < roads.Count; r++)
                for (int e = 0; e < 2; e++)
                {
                    Vector2 end = e == 0 ? map[r][0] : map[r][map[r].Length - 1];
                    float y = (e == 0 ? roads[r].pts[0] : roads[r].pts[roads[r].pts.Length - 1]).y;
                    var dist = new float[roads.Count];
                    float nearest = float.MaxValue;
                    bool any = false;
                    for (int o = 0; o < roads.Count; o++)
                    {
                        dist[o] = float.MaxValue;
                        if (o == r) continue;
                        float d = Geo.DistanceToPolyline(map[o], end, false);
                        if (d <= 1f) any = true;
                        else
                        {
                            if (d > 12f) continue;
                            if (Mathf.Abs(roads[o].pts[Closest(roads[o].pts, end)].y - y) > Mathf.Max(1.2f, d * 0.3f)) continue;
                        }
                        dist[o] = d;
                        nearest = Mathf.Min(nearest, d);
                    }
                    if (nearest == float.MaxValue) continue;
                    for (int o = 0; o < roads.Count; o++)
                    {
                        if (dist[o] > (any ? 1f : nearest + 0.6f)) continue;
                        found.Add(new RoadNetwork.Meeting { road = r, end = e, other = o, idx = Closest(roads[o].pts, end) });
                        if (any) onMap++; else byNearness++;
                    }
                }
            Debug.Log($"[Nainital] road junctions: {onMap} on the map, {byNearness} by nearness (an end just short of its road)");
            return found.ToArray();
        }

        /// <summary>
        /// Each lane kept clear of what stands in it (TrafficLab.LaneAudit, 22 Nov 2026: 123 of 34301 lane points were shut -
        /// house corners at junctions, retaining walls, posts): the last step, with everything standing (the hand-placed
        /// gate and chowki too). Only the lane that is shut moves, the least that clears it, on ground at the road's level,
        /// tapered over 8 m. The road's line stays, so the junctions stay; nothing built is moved.
        /// </summary>
        const float CoachHalf = 1.35f;

        /// <summary>A board in the ground vehicles swing over round a junction island: out of it (22 Nov 2026: the Tallital
        /// Boat Stand's, moved off the road's line into the island's ring - every turn to Bhawali backed off it).</summary>
        static void MoveBoardsOffIslands(WorldCtx ctx, RoadNetwork net)
        {
            if (net.islands == null) return;
            var roads = Clearance(ctx);
            foreach (var board in UnityEngine.Object.FindObjectsByType<InfoSign>(FindObjectsSortMode.None))
                foreach (var island in net.islands)
                {
                    float keep = island.radius + 10f;
                    var at = new Vector2(board.transform.position.x, board.transform.position.z);
                    var c = new Vector2(island.centre.x, island.centre.z);
                    if ((at - c).sqrMagnitude >= keep * keep) continue;
                    float ground = ctx.H(at), over = board.transform.position.y - ground;
                    bool done = false;
                    for (float r = 1f; r <= 22f && !done; r += 0.5f)
                        for (int k = 0; k < 36 && !done; k++)
                        {
                            float a = k * Mathf.PI / 18f;
                            Vector2 q = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                            if ((q - c).sqrMagnitude < keep * keep || roads.Blocked(q, 1.2f) || ctx.InLake(q)) continue;
                            float h = ctx.H(q);
                            if (Mathf.Abs(h - ground) > 1.5f) continue;
                            bool taken = false;
                            foreach (var o in Physics.OverlapBox(new Vector3(q.x, h + 1.1f, q.y), new Vector3(0.8f, 0.75f, 0.8f)))
                                if (!(o is TerrainCollider) && !o.isTrigger) { taken = true; break; }
                            if (taken) continue;
                            board.transform.position = new Vector3(q.x, h + over, q.y);
                            EditorUtility.SetDirty(board.transform);
                            Physics.SyncTransforms();
                            Debug.Log($"[Nainital] board '{board.title}' out of the island's ring: ({at.x:F1}, {at.y:F1}) -> ({q.x:F1}, {q.y:F1})");
                            done = true;
                        }
                    if (!done) Debug.LogWarning($"[Nainital] board '{board.title}' stands in a junction island's ring and found no clear place");
                }
        }

        static void ClearLanes(WorldCtx ctx)
        {
            var tm = UnityEngine.Object.FindAnyObjectByType<TrafficManager>();
            var net = tm ? tm.network : null;
            if (!net) return;
            Physics.SyncTransforms();
            int mask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer));
            var buf = new Collider[16];
            MoveBoardsOffIslands(ctx, net);
            Physics.SyncTransforms();
            int shut = 0, moved = 0;
            var left0 = new List<string>();
            for (int r = 0; r < net.roads.Length; r++)
            {
                var road = net.roads[r];
                var q = road.pts;
                float off0 = road.oneWay != 0 && !road.keepLeft ? 0f : Mathf.Max(1.1f, road.width * 0.25f);
                road.shiftFwd = road.shiftBack = new float[0];
                foreach (int dir in road.oneWay != 0 ? new[] { road.oneWay } : new[] { 1, -1 })
                {
                    float[] sh = null;
                    bool Shut(int i, float delta, out bool offRoad, float halfWide = 0.9f)
                    {
                        offRoad = false;
                        Vector3 tan = (q[Mathf.Min(i + 1, q.Length - 1)] - q[Mathf.Max(i - 1, 0)]) * dir;
                        Vector3 flat = new Vector3(tan.x, 0f, tan.z);
                        if (flat.sqrMagnitude < 1e-4f) return false;
                        Vector3 p = q[i] + Vector3.Cross(flat.normalized, Vector3.up) * (off0 + delta);
                        var p2 = new Vector2(p.x, p.z);
                        if (road.oneWay == 0)   // (the Mall's lanes are its decks')
                        {
                            float g = ctx.H(p2) + 0.1f;
                            offRoad = ctx.InLake(p2) || Mathf.Abs(g - q[i].y) > 0.9f;
                            p.y = Mathf.Max(g, q[i].y - 0.9f);
                        }
                        int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 1.1f, new Vector3(halfWide, 0.65f, 2f), buf, Quaternion.LookRotation(tan.normalized), mask, QueryTriggerInteraction.Ignore);
                        for (int k = 0; k < n; k++) if (!(buf[k] is TerrainCollider)) return true;
                        return false;
                    }
                    for (int i = 0; i < q.Length; i++)
                    {
                        if (net.NearIsland(q[i], 3f)) continue;   // driven round, not shifted
                        bool narrowShut = Shut(i, 0f, out _), coach = road.width >= 5.9f;
                        if (!narrowShut && !(coach && Shut(i, 0f, out _, CoachHalf))) continue;
                        float hi = road.width * 0.5f + 1f - off0, lo = road.oneWay != 0 ? -hi : -0.6f - off0;
                        bool ok = false;
                        // room for a coach first (2.7 m: the bus clipped the parapet's end at Tallital), else for a car
                        for (int size = coach ? 0 : 1; size < 2 && !ok; size++)
                        {
                            if (size == 1 && !narrowShut) break;   // tight for a coach, but a car gets by: left as it is
                            float halfWide = size == 0 ? CoachHalf : 0.9f;
                            for (int k = 1; k <= (size == 0 ? 6 : 24) && !ok; k++)   // (for a coach at most 1.5 m over)
                                foreach (float delta in new[] { -0.25f * k, 0.25f * k })
                                {
                                    if (delta < lo || delta > hi) continue;
                                    if (Shut(i, delta, out bool bad, halfWide) || bad) continue;
                                    (sh ??= new float[q.Length])[i] = delta;
                                    ok = true;
                                    moved++;
                                    break;
                                }
                        }
                        if (narrowShut) shut++;
                        if (narrowShut && !ok) left0.Add($"#{r} pt {i} ({q[i].x:F0},{q[i].z:F0})");
                    }
                    if (sh == null) continue;
                    var eased = (float[])sh.Clone();
                    for (int i = 0; i < q.Length; i++)
                    {
                        if (sh[i] != 0f) continue;
                        float best = 0f;
                        for (int k = -2; k <= 2; k++)
                        {
                            if (i + k < 0 || i + k >= q.Length) continue;
                            float v = sh[i + k] * (1f - Mathf.Abs(k) / 3f);
                            if (Mathf.Abs(v) > Mathf.Abs(best)) best = v;
                        }
                        if (best != 0f && !Shut(i, best, out bool bad) && !bad) eased[i] = best;
                    }
                    if (dir > 0) road.shiftFwd = eased; else road.shiftBack = eased;
                }
            }
            EditorUtility.SetDirty(net);
            Debug.Log($"[Nainital] traffic lanes: {shut} lane points shut, {moved} moved clear, {left0.Count} with no room: {string.Join("; ", left0)}");
        }

        /// <summary>
        /// The walkways cut where something built stands on them (owner, 23 Nov 2026, the walk to Naina Devi: "ye saare log
        /// kahan jaana cahhre hai? isme brain nhi hai? road ki wajaye boundary me ghus ke chlre hain ..aage bhi nhi bhdpaare"):
        /// a footpath line drawn beside a road or from the map ran through a parapet, a railing or a fence built after it, and
        /// the walkers pressed into it for ever. Each point is tested with a walker-sized capsule against everything but the
        /// ground, people, vehicles and triggers; a walkway is split at the blocked points, pieces shorter than 4 points dropped.
        /// </summary>
        static RoadNetwork.Path[] ClearWalks(List<RoadNetwork.Path> walks)
        {
            Physics.SyncTransforms();
            int mask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer));
            var hits = new Collider[8];
            bool Blocked(Vector3 p)
            {
                int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.3f, hits, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++) if (!(hits[i] is TerrainCollider)) return true;
                return false;
            }
            var outList = new List<RoadNetwork.Path>();
            int cut = 0, dropped = 0;
            foreach (var w in walks)
            {
                var run = new List<Vector3>();
                void Flush()
                {
                    if (run.Count >= 4) outList.Add(new RoadNetwork.Path { name = w.name, width = w.width, pts = run.ToArray() });
                    else if (run.Count > 0) dropped++;
                    run = new List<Vector3>();
                }
                foreach (var p in w.pts)
                {
                    if (Blocked(p)) { cut++; Flush(); continue; }
                    run.Add(p);
                }
                Flush();
            }
            Debug.Log($"[Nainital] walkways: {walks.Count} -> {outList.Count} after cutting {cut} blocked points ({dropped} short pieces dropped)");
            return outList.ToArray();
        }

        // ------------------------------------------------------------------ one-way roads
        // Mallital's one-way (owner, 24 Nov 2026: "DSA ke aage one way hoga at 29.391984, 79.454147 ..wahan jaake left waale
        // Jama Masjid se hote huye jayenge or aane waale direct bada bazaar road se ..ye one way SBM toilet tirahe se union
        // bank tk hota hai"; and the bus line's stops "DSA / Naina Devi gate, Jama Masjid, SBM toilet tiraha, Eco Cave" put
        // the SBM tiraha PAST Jama Masjid: the tiraha at (-150, 20) where the Kaladhungi road comes in). Two map roads leave
        // the tiraha at the Mall's Mallital end (-57, 156; his pin is 27 m off it) and meet again up at (-530, 534):
        // - the one past Jama Masjid (-152, 52): one-way OUT from the Mall end to the SBM tiraha only, two-way beyond it;
        // - the one past Bada Bazar (-166, 207): one-way BACK, down to the Mall end (all of it).
        // Found by their ends and what they pass, so a re-run of the map data keeps it.
        static readonly Vector2 OneWayTiraha = new Vector2(-57f, 156f), OneWayTop = new Vector2(-530f, 534f);
        static readonly Vector2 JamaMasjidAt = new Vector2(-152.5f, 51.5f), BadaBazarAt = new Vector2(-166f, 207f);
        static readonly Vector2 SbmTiraha = new Vector2(-150f, 20f);

        /// <summary>The Mallital pair's pieces of `line` (map metres, in its own order): each piece's points and its way
        /// (+1 along, -1 against, 0 both). Null for every other road (then it is one piece, both ways).</summary>
        static List<(Vector2[] line, int dir)> OneWayPieces(Vector2[] line)
        {
            if (line.Length < 2) return null;
            Vector2 a = line[0], b = line[line.Length - 1];
            bool fromTiraha = (a - OneWayTiraha).magnitude < 15f && (b - OneWayTop).magnitude < 15f;
            bool fromTop = (a - OneWayTop).magnitude < 15f && (b - OneWayTiraha).magnitude < 15f;
            if (!fromTiraha && !fromTop) return null;
            bool masjid = Geo.DistanceToPolyline(line, JamaMasjidAt, false) < 40f;
            bool bazar = Geo.DistanceToPolyline(line, BadaBazarAt, false) < 25f;
            var outList = new List<(Vector2[] line, int dir)>();
            if (bazar && !masjid)
            {
                outList.Add((line, fromTop ? 1 : -1));   // back: down to the Mall end
                Debug.Log($"[Nainital] one-way: the road past Bada Bazar (back), {line.Length} points, all of it");
                return outList;
            }
            if (!masjid || bazar) return null;
            // split at the point nearest the SBM tiraha: the Mall end's side one-way (away from the Mall end), the rest both ways
            int k = 0;
            float best = float.MaxValue;
            for (int i = 0; i < line.Length; i++) { float d = (line[i] - SbmTiraha).sqrMagnitude; if (d < best) { best = d; k = i; } }
            k = Mathf.Clamp(k, 1, line.Length - 2);
            var first = new Vector2[k + 1];
            System.Array.Copy(line, 0, first, 0, k + 1);
            var rest = new Vector2[line.Length - k];
            System.Array.Copy(line, k, rest, 0, line.Length - k);
            if (fromTiraha) { outList.Add((first, 1)); outList.Add((rest, 0)); }       // Mall end first: out = along
            else { outList.Add((first, 0)); outList.Add((rest, -1)); }                 // top first: the Mall end's piece is last, out = against
            Debug.Log($"[Nainital] one-way: the road past Jama Masjid split at ({line[k].x:F0}, {line[k].y:F0}) {Mathf.Sqrt(best):F1} m from the SBM tiraha: Mall end -> tiraha one-way, beyond both ways");
            return outList;
        }

        // ------------------------------------------------------------------ road + footpath graph
        static RoadNetwork BuildRoadNetwork(WorldCtx ctx)
        {
            var onMap = new List<Vector2[]>();   // each drivable road's line in the map (RoadJoins)
            var roads = new List<RoadNetwork.Path>();
            var walks = new List<RoadNetwork.Path>();
            foreach (var r in ctx.world.roads)
            {
                var pts2 = Geo.ToPoints(r.pts);
                if (pts2.Length < 2) continue;
                if (VehicleKinds.Contains(r.kind))
                {
                    var res = Resample(pts2, 4f, false);
                    if (res.Count < 4)   // a connector between two roads (5-8 m): left out, it cut Kunjakharak Road off the town
                    {
                        float length = 0f;
                        for (int i = 0; i + 1 < pts2.Length; i++) length += Vector2.Distance(pts2[i], pts2[i + 1]);
                        if (length > 1f) res = Resample(pts2, length / 3.01f, false);
                    }
                    // Mall Road is one-way (owner, 24 Nov 2026: "lower mall road is for left line (bus stand to DSA side direction)
                    // and upper is reverse of that"; until then it was the other way round): Lower Mall towards Mallital,
                    // Upper Mall towards Tallital,
                    // each driven down the middle of its real deck so cars keep clear of the divider and its posts
                    var deck = r.name == MallUpper ? ctx.mallUpperDrive : r.name == MallLower ? ctx.mallLowerDrive : null;
                    if (deck != null && deck.Count >= 4)
                    {
                        var dpts = new List<Vector3>();
                        for (int i = 0; i < deck.Count; i += 2) dpts.Add(deck[i]);
                        // the way along the points that goes north (to Mallital): the points run whichever way the map has them
                        // (22 Nov 2026: Upper Mall's ran north-to-south, so "+1" sent both roads to Tallital and no car could
                        // drive up the Mall - the route from the bus stand to the DSA ground went round the far side of the lake)
                        int north = dpts[dpts.Count - 1].z > dpts[0].z ? 1 : -1;
                        roads.Add(new RoadNetwork.Path { name = r.name, width = r.width, pts = dpts.ToArray(), oneWay = r.name == MallLower ? north : -north });
                        onMap.Add(pts2);
                    }
                    else if (res.Count >= 4)
                    {
                        var pieces = OneWayPieces(res.ToArray());
                        if (pieces == null)
                        {
                            var p3 = new Vector3[res.Count];
                            for (int i = 0; i < res.Count; i++) p3[i] = new Vector3(res[i].x, ctx.H(res[i]) + 0.1f, res[i].y);
                            roads.Add(new RoadNetwork.Path { name = r.name, width = r.width, pts = p3 });
                            onMap.Add(pts2);
                        }
                        else
                            foreach (var (pl, dir) in pieces)   // the Mallital one-way: its pieces, each its own road
                            {
                                var p3 = new Vector3[pl.Length];
                                for (int i = 0; i < pl.Length; i++) p3[i] = new Vector3(pl[i].x, ctx.H(pl[i]) + 0.1f, pl[i].y);
                                roads.Add(new RoadNetwork.Path { name = r.name, width = r.width, pts = p3, oneWay = dir, keepLeft = dir != 0 });
                                onMap.Add(pl);
                            }
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
            net.walks = ClearWalks(walks);
            net.meetings = RoadJoins(roads, onMap);
            net.islands = trafficIslands.ToArray();
            AssetDatabase.CreateAsset(net, path);
            Debug.Log($"[Nainital] road network: {roads.Count} drivable roads, {walks.Count} walkways; {net.islands.Length} junction islands");
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
                if (!string.IsNullOrEmpty(r.name) && r.name.Contains("Mall")) continue;   // no parking along the Mall: poles, railings, crowds
                var pts = Resample(Geo.ToPoints(r.pts), 30f, false);
                for (int i = 1; i < pts.Count - 1 && pos.Count < 46; i++)
                {
                    if (rnd.NextDouble() > 0.22) continue;
                    Vector2 tan = (pts[i + 1] - pts[i - 1]).normalized;
                    Vector2 left = new Vector2(-tan.y, tan.x);
                    Vector2 p = pts[i] + left * (r.width * 0.5f + 1.3f);
                    if (ctx.InLake(p) || ctx.LakeDistance(p) < 9f || Mathf.Abs(ctx.H(p) - ctx.H(pts[i])) > 0.7f) continue;
                    bool byPole = false;   // lamp posts have no collider on every road: keep cars 2.2 m clear of each lamp
                    foreach (var lh in ctx.lampHeads) if ((new Vector2(lh.x, lh.z) - p).sqrMagnitude < 2.2f * 2.2f + 1.8f * 1.8f) { byPole = true; break; }
                    if (byPole) continue;
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
                for (int b = 0; b < 4; b++)   // nose-in under the canopy, side by side, centred in the yard (25 Nov)
                {
                    Vector3 p = ctx.busStand + ctx.busStandRot * new Vector3((b - 1.5f) * 4.6f, 0, 0.5f);
                    p.y = ctx.H(new Vector2(p.x, p.z));
                    Add(p, ctx.busStandRot, CarType.Bus, b % 2 == 0 ? Px.BusWhite : Px.BusBlue, lib ? Named(b % 2 == 0 ? "bus_roadways" : "bus_tourist") : -1);
                }
                // the taxi rank either side of the buses, only where the yard has room (they stood on the road, 25 Nov)
                float[] rank = { 9.7f, -9.7f, 12.5f, -12.5f, 15.3f, -15.3f };
                int taxis = 0;
                foreach (float x in rank)
                {
                    if (taxis >= 3 || Mathf.Abs(x) + 1.2f > ctx.busStandHalf) continue;
                    taxis++;
                    Vector3 p = ctx.busStand + ctx.busStandRot * new Vector3(x, 0, 0.5f);
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

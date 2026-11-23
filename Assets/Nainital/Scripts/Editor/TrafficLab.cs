using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Research tools for the AI traffic (owner, 22 Nov 2026: "AI vehicle ko perfect kro ... ek gaadi ko bus stand se DSA tk
    /// bhejo, mall road pe rukjayegi ... glt mudne pe back krke same direction me phirse chli jaati hai ... gaadiyan aar paar
    /// jaari hain"). Each writes to the log and to Bridge/traffic_lab.txt.
    /// - LinkAudit (edit or Play): every road end's links (RoadNetwork.Links joins a road's end to the nearest point of any
    ///   road within 12 m): the height between them, a wall / railing / building across, the turn it asks for.
    /// - RouteDrive (Play; Bridge/arg.txt "x,z;x,z" or empty = the New Bus Station to the DSA ground): plans the roads, sends
    ///   one traffic taxi (TrafficCar.FollowRoute), the camera and the player (jetpack) with it, logs it every second and
    ///   every stop (what is in front), captures each stop; ends at the goal or after 8 minutes.
    /// - Overlaps (Play; arg = seconds, default 60): every vehicle pair whose bodies overlap (aar-paar): how deep, whether
    ///   their colliders touch, what each was doing; every moving vehicle inside a wall / a person.
    /// - TurnTest (Play; about 6 min): a real traffic car through every turn the traffic may take in the town, and cars
    ///   left pointing the wrong way on its roads (do they come back to their lane?). Pass / back-off / ram / fail.
    /// - LaneAudit, DeckAll, DeckProfile (edit or Play): what stands in the lanes; the Upper Mall's surface.
    /// </summary>
    public static class TrafficLab
    {
        static string Arg() { try { return File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { return ""; } }
        static string LogPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/traffic_lab.txt"));
        static void Out(string s) { Debug.Log("[Nainital] lab " + s); try { File.AppendAllText(LogPath, s + "\n"); } catch { } }

        static TrafficManager Manager()
        {
            var tm = Object.FindAnyObjectByType<TrafficManager>();
            if (!tm && !Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Nainital/Scenes/Nainital.unity");
                tm = Object.FindAnyObjectByType<TrafficManager>();
            }
            return tm;
        }

        static readonly int StaticMask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer));

        // ------------------------------------------------------------------ round an island
        /// <summary>The ground round each junction island as a car meets it (owner, 22 Nov 2026: "gaadi fsgyi hai", on the
        /// north of the Tallital island): every static thing within 16 m between 0.35 and 1.7 m up; a map (0.5 m, north up);
        /// for every 10 degrees the free band between the island and the first thing outside it; the roads that meet there.</summary>
        public static void IslandScan()
        {
            var tm = Manager();
            var net = tm ? tm.network : null;
            var terrain = Object.FindAnyObjectByType<Terrain>();
            if (!net || net.islands == null || !terrain) { Out("island scan: nothing to scan"); return; }
            Physics.SyncTransforms();
            var buf = new Collider[16];
            float Y(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
            Collider At(Vector3 p, float r)
            {
                p.y = Y(p) + 1.025f;
                int n = Physics.OverlapBoxNonAlloc(p, new Vector3(r, 0.675f, r), buf, Quaternion.identity, StaticMask, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < n; k++) if (!(buf[k] is TerrainCollider)) return buf[k];
                return null;
            }
            foreach (var isl in net.islands)
            {
                var c = isl.centre;
                var sb = new StringBuilder($"island scan ({c.x:F1},{c.z:F1}) r {isl.radius}\n");
                var letter = new Dictionary<Collider, char>();
                var map = new StringBuilder();
                const string marks = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                for (float z = 16f; z >= -16f; z -= 0.5f)
                {
                    map.Append($"{c.z + z,8:F1} ");
                    for (float x = -16f; x <= 16f; x += 0.5f)
                    {
                        var hit = At(c + new Vector3(x, 0f, z), 0.25f);
                        char m = x == 0f && z == 0f ? '+' : '.';
                        if (hit)
                        {
                            if (!letter.TryGetValue(hit, out m)) letter[hit] = m = marks[letter.Count % marks.Length];
                        }
                        map.Append(m);
                    }
                    map.Append('\n');
                }
                foreach (var kv in letter) sb.Append($"  {kv.Value} = {kv.Key.name}, middle ({kv.Key.bounds.center.x:F1},{kv.Key.bounds.center.z:F1}), size {kv.Key.bounds.size:F1}\n");
                sb.Append(map);
                for (int deg = 0; deg < 360; deg += 10)
                {
                    Vector3 o = Quaternion.AngleAxis(deg, Vector3.up) * Vector3.forward;
                    float from = 0f, to = 16f;
                    Collider outer = null;
                    for (float r = 0.5f; r <= 5f; r += 0.125f) if (At(c + o * r, 0.1f)) from = r;
                    for (float r = from + 0.125f; r <= 16f; r += 0.125f) { var h = At(c + o * r, 0.1f); if (h) { to = r; outer = h; break; } }
                    Vector3 edge = c + o * Mathf.Min(to, 12f);
                    sb.Append($"  {deg,3} deg: free {from:F2} .. {to:F2} = {to - from:F2} m {(outer ? outer.name : "open")}, ground there {Y(edge) - c.y:+0.00;-0.00}\n");
                }
                for (int r = 0; r < net.roads.Length; r++)
                    for (int e = 0; e < 2; e++)
                    {
                        var q = net.roads[r].pts;
                        int end = e == 0 ? 0 : q.Length - 1, inward = e == 0 ? Mathf.Min(3, q.Length - 1) : Mathf.Max(0, q.Length - 4);
                        Vector3 d = q[inward] - q[end];
                        if ((new Vector2(q[end].x - c.x, q[end].z - c.z)).magnitude > 6f) continue;
                        sb.Append($"  arm: #{r} {net.roads[r].name} w {net.roads[r].width} bears {Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f):F0} deg\n");
                    }
                Out(sb.ToString());
            }
        }

        // ------------------------------------------------------------------ links
        public static void LinkAudit()
        {
            var tm = Manager();
            var net = tm ? tm.network : null;
            if (!net) { Out("link audit: no road network"); return; }
            var rows = new List<(float score, string s)>();
            int total = 0, high = 0, walled = 0, earth = 0, hairpin = 0, far = 0, dead = 0, ends = 0, offered = 0, offeredSharp = 0;
            for (int r = 0; r < net.roads.Length; r++)
            {
                var pts = net.roads[r].pts;
                if (pts.Length < 2) continue;
                for (int e = 0; e < 2; e++)
                {
                    int ow = net.roads[r].oneWay;
                    if ((ow > 0 && e == 0) || (ow < 0 && e == 1)) continue;   // nobody arrives at this end
                    ends++;
                    int end = e == 0 ? 0 : pts.Length - 1, before = e == 0 ? Mathf.Min(2, pts.Length - 1) : Mathf.Max(0, pts.Length - 3);
                    Vector3 p = pts[end], hin = p - pts[before];
                    hin.y = 0f;
                    var links = net.Links(r, e);
                    var ok = RoadChoice.Onward(net, r, end);
                    if (ok.Count == 0) dead++;
                    foreach (var way in ok)
                    {
                        offered++;
                        var w = net.roads[way.r].pts;
                        Vector3 o2 = w[Mathf.Clamp(way.i + 3 * way.d, 0, w.Length - 1)] - w[way.i];
                        o2.y = 0f;
                        if (Vector3.Angle(hin, o2) > RoadChoice.HairpinAngle) offeredSharp++;
                    }
                    foreach (var (o, i) in links)
                    {
                        total++;
                        var q = net.roads[o].pts;
                        Vector3 b = q[i];
                        float d = new Vector2(b.x - p.x, b.z - p.z).magnitude, dh = b.y - p.y;
                        float bestAng = 999f;
                        int oow = net.roads[o].oneWay;
                        foreach (int dd in new[] { 1, -1 })
                        {
                            if (oow != 0 && dd != oow) continue;
                            int j = Mathf.Clamp(i + dd * 2, 0, q.Length - 1);
                            if (j == i) continue;
                            Vector3 hout = q[j] - q[i];
                            hout.y = 0f;
                            if (hout.sqrMagnitude < 0.01f || hin.sqrMagnitude < 0.01f) continue;
                            bestAng = Mathf.Min(bestAng, Vector3.Angle(hin, hout));
                        }
                        bool wall = Physics.Linecast(p + Vector3.up * 1.1f, b + Vector3.up * 1.1f, out var wh, StaticMask, QueryTriggerInteraction.Ignore) && !(wh.collider is TerrainCollider);
                        bool ground = !wall && Physics.Linecast(p + Vector3.up * 0.6f, b + Vector3.up * 0.6f, out var gh, StaticMask, QueryTriggerInteraction.Ignore) && gh.collider is TerrainCollider;
                        bool isHigh = Mathf.Abs(dh) > 1.2f, isPin = bestAng > 115f && bestAng < 900f, isFar = d > 7f;
                        if (isHigh) high++;
                        if (wall) walled++;
                        if (ground) earth++;
                        if (isPin) hairpin++;
                        if (isFar) far++;
                        float score = Mathf.Abs(dh) * 2f + (wall ? 6f : 0f) + (ground ? 3f : 0f) + (isPin ? 3f : 0f) + d * 0.3f;
                        if (isHigh || wall || ground || isPin || isFar)
                            rows.Add((score, $"  {net.roads[r].name} #{r} end {e} ({p.x:F0},{p.z:F0}) -> {net.roads[o].name} #{o} pt {i}/{q.Length} ({b.x:F0},{b.z:F0}): {d:F1} m, dh {dh:+0.0;-0.0}, turn {(bestAng < 900f ? bestAng.ToString("F0") : "none allowed")}" +
                                               $"{(wall ? " WALL " + wh.collider.name : "")}{(ground ? " EARTH" : "")}"));
                    }
                }
            }
            rows.Sort((x, y) => y.score.CompareTo(x.score));
            Out($"turns the traffic may take: {offered}, of them hairpins (no gentler way there) {offeredSharp}; junction islands {net.islands?.Length ?? 0}");
            var sb = new StringBuilder($"link audit: {net.roads.Length} roads, {ends} road ends driven into, {total} links; dead ends (no onward road) {dead}; suspicious: height > 1.2 m {high}, wall across {walled}, ground across {earth}, hairpin > 115 deg {hairpin}, > 7 m apart {far}\n");
            for (int k = 0; k < Mathf.Min(45, rows.Count); k++) sb.Append(rows[k].s).Append('\n');
            Out(sb.ToString());
        }

        // ------------------------------------------------------------------ the Upper Mall's surface
        /// <summary>Upper Mall's driving surface near a point (Bridge/arg.txt "x,z", default: where Zoo Road starts): every
        /// 1 m along the lane and 2 m to either side, what a ray from above meets first and how high; flags a bump (the
        /// height off the straight line through its neighbours by > 6 cm), a step (> 8 cm between 1 m samples) and the
        /// surface changing (deck / terrain / something else).</summary>
        public static void DeckProfile()
        {
            var tm = Manager();
            var net = tm ? tm.network : null;
            if (!net) { Out("deck: no road network"); return; }
            Vector2 at = new Vector2(922.35f, -888.13f);
            var ps = Arg().Split(',');
            if (ps.Length == 2 && float.TryParse(ps[0], out var ax) && float.TryParse(ps[1], out var az)) at = new Vector2(ax, az);
            int ur = -1;
            for (int r = 0; r < net.roads.Length; r++) if (net.roads[r].name == "Upper Mall Road") ur = r;
            if (ur < 0) { Out("deck: no Upper Mall Road"); return; }
            var q = net.roads[ur].pts;
            int ci = 0; float cd = float.MaxValue;
            for (int i = 0; i < q.Length; i++) { float d = new Vector2(q[i].x - at.x, q[i].z - at.y).sqrMagnitude; if (d < cd) { cd = d; ci = i; } }
            // a 1 m polyline along the lane, 60 m either way
            var line = new List<Vector3>();
            int i0 = Mathf.Max(0, ci - 40), i1 = Mathf.Min(q.Length - 1, ci + 40);
            for (int i = i0; i < i1; i++)
            {
                float len = Vector3.Distance(q[i], q[i + 1]);
                for (float s = 0f; s < len; s += 1f) line.Add(Vector3.Lerp(q[i], q[i + 1], s / len));
            }
            var sb = new StringBuilder($"deck: Upper Mall #{ur} near ({at.x:F0},{at.y:F0}): nearest lane point {ci} ({q[ci].x:F0},{q[ci].z:F0}) {Mathf.Sqrt(cd):F1} m away; {line.Count} samples\n");
            int mask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer));
            int bumps = 0, steps = 0, changes = 0;
            foreach (float side in new[] { 0f, -2f, 2f })
            {
                var ys = new float[line.Count];
                var names = new string[line.Count];
                for (int k = 0; k < line.Count; k++)
                {
                    Vector3 tan = line[Mathf.Min(k + 1, line.Count - 1)] - line[Mathf.Max(k - 1, 0)];
                    tan.y = 0f;
                    Vector3 left = Vector3.Cross(tan.normalized, Vector3.up);
                    Vector3 p = line[k] + left * side;
                    if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var h, 12f, mask, QueryTriggerInteraction.Ignore))
                    { ys[k] = h.point.y; names[k] = h.collider is TerrainCollider ? "terrain" : h.collider.name; }
                    else { ys[k] = float.NaN; names[k] = "NOTHING"; }
                }
                for (int k = 1; k + 1 < line.Count; k++)
                {
                    Vector3 p = line[k];
                    float bump = ys[k] - (ys[k - 1] + ys[k + 1]) * 0.5f, step = ys[k] - ys[k - 1];
                    bool changed = names[k] != names[k - 1];
                    if (Mathf.Abs(bump) > 0.06f) bumps++;
                    if (Mathf.Abs(step) > 0.08f) steps++;
                    if (changed) changes++;
                    if (Mathf.Abs(bump) > 0.06f || Mathf.Abs(step) > 0.08f || changed)
                        sb.Append($"  side {side:+0;-0;0} m, ({p.x:F1},{p.z:F1}) lane y {p.y:F2}: ground {ys[k]:F2} ({names[k]}), step {step:+0.00;-0.00}, bump {bump:+0.00;-0.00}{(changed ? $"  [{names[k - 1]} -> {names[k]}]" : "")}\n");
                }
            }
            sb.Insert(0, $"deck summary: bumps {bumps}, steps {steps}, surface changes {changes}\n");
            Out(sb.ToString());
        }

        // ------------------------------------------------------------------ what stands in the lanes
        /// <summary>Every drivable road, every point, each lane as TrafficCar drives it: what static thing a car's box
        /// (1.8 x 4 m, 0.45-1.75 m over the lane, pitched with the road) meets, grouped by what it is and by road; and the
        /// lanes whose ground is 0.9 m off the road's middle (over an edge).</summary>
        public static void LaneAudit()
        {
            var tm = Manager();
            var net = tm ? tm.network : null;
            if (!net) { Out("lane audit: no road network"); return; }
            var terrain = Object.FindAnyObjectByType<Terrain>();
            Physics.SyncTransforms();
            var buf = new Collider[24];
            var byName = new Dictionary<string, (int n, string first)>();
            var byRoad = new SortedDictionary<int, (int n, string what)>();
            int lanes = 0, blocked = 0, edge = 0;
            for (int r = 0; r < net.roads.Length; r++)
            {
                var road = net.roads[r];
                var q = road.pts;
                for (int i = 0; i < q.Length; i++)
                {
                    Vector3 tan = q[Mathf.Min(i + 1, q.Length - 1)] - q[Mathf.Max(i - 1, 0)];
                    Vector3 flat = new Vector3(tan.x, 0f, tan.z);
                    if (flat.sqrMagnitude < 1e-4f) continue;
                    Vector3 left = Vector3.Cross(flat.normalized, Vector3.up);
                    float off = road.oneWay != 0 ? 0f : Mathf.Max(1.1f, road.width * 0.25f);
                    if (net.NearIsland(q[i], 3f)) continue;   // driven round it (the ring is checked below)
                    foreach (int d in road.oneWay != 0 ? new[] { road.oneWay } : new[] { 1, -1 })
                    {
                        lanes++;
                        Vector3 p = q[i] + left * (d * (off + road.Shift(d, i)));
                        if (road.oneWay == 0 && terrain)   // the Mall's lanes are its deck's; the others lie on the ground
                        {
                            float g = terrain.SampleHeight(p) + terrain.transform.position.y + 0.1f;
                            if (Mathf.Abs(g - q[i].y) > 0.9f) edge++;
                            p.y = Mathf.Max(g, q[i].y - 0.9f);
                        }
                        int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 1.1f, new Vector3(0.9f, 0.65f, 2f), buf, Quaternion.LookRotation(tan.normalized), StaticMask, QueryTriggerInteraction.Ignore);
                        string hit = null;
                        for (int k = 0; k < n; k++)
                        {
                            if (buf[k] is TerrainCollider) continue;
                            string name = buf[k].name;
                            byName.TryGetValue(name, out var v);
                            byName[name] = (v.n + 1, v.first ?? $"#{r} pt {i} ({p.x:F0},{p.z:F0})");
                            hit = hit ?? name;
                        }
                        if (hit == null) continue;
                        blocked++;
                        byRoad.TryGetValue(r, out var w);
                        byRoad[r] = (w.n + 1, w.what ?? $"first pt {i} ({p.x:F0},{p.z:F0}) {hit}");
                    }
                }
            }
            if (net.islands != null && terrain)
                foreach (var isl in net.islands)
                {
                    var ring = new StringBuilder($"island ({isl.centre.x:F0},{isl.centre.z:F0}) r {isl.radius}: ");
                    int shutRing = 0;
                    for (int k = 0; k < 24; k++)
                    {
                        Vector3 o = Quaternion.AngleAxis(k * 15f, Vector3.up) * Vector3.forward;
                        Vector3 p = isl.centre + o * (isl.radius + 0.9f + 0.9f);
                        p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.1f;
                        int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 1.1f, new Vector3(0.9f, 0.65f, 1.2f), buf, Quaternion.LookRotation(Vector3.Cross(Vector3.up, o)), StaticMask, QueryTriggerInteraction.Ignore);
                        for (int j = 0; j < n; j++)
                        {
                            if (buf[j] is TerrainCollider) continue;
                            shutRing++;
                            ring.Append($"[{k * 15} deg {buf[j].name}] ");
                            break;
                        }
                    }
                    Out(ring.Append($"-> {shutRing} of 24 places on its ring shut").ToString());
                }
            var sb = new StringBuilder($"lane audit: {lanes} lane points, blocked {blocked} ({100f * blocked / Mathf.Max(1, lanes):F1} %), over an edge {edge}\n");
            var names = new List<KeyValuePair<string, (int n, string first)>>(byName);
            names.Sort((a, b) => b.Value.n.CompareTo(a.Value.n));
            sb.Append(" by what stands there:\n");
            for (int k = 0; k < Mathf.Min(50, names.Count); k++) sb.Append($"  {names[k].Value.n,5}  {names[k].Key}  {names[k].Value.first}\n");
            sb.Append(" by road:\n");
            foreach (var kv in byRoad) sb.Append($"  #{kv.Key} '{net.roads[kv.Key].name}' w{net.roads[kv.Key].width}: {kv.Value.n} of {net.roads[kv.Key].pts.Length}, {kv.Value.what}\n");
            Out(sb.ToString());
        }

        /// <summary>The whole Upper Mall's driving surface every 1 m along three lines (the middle, 2 m either side): the
        /// bumps (off the line through the neighbours by > 6 cm) and the largest, per 100 m, and where the ground comes up
        /// through the deck.</summary>
        public static void DeckAll()
        {
            var tm = Manager();
            var net = tm ? tm.network : null;
            if (!net) { Out("deck all: no road network"); return; }
            Vector3[] q = null;
            foreach (var r in net.roads) if (r.name == "Upper Mall Road") q = r.pts;
            if (q == null) { Out("deck all: no Upper Mall Road"); return; }
            Physics.SyncTransforms();
            var line = new List<Vector3>();
            for (int i = 0; i + 1 < q.Length; i++)
            {
                float len = Vector3.Distance(q[i], q[i + 1]);
                for (float s = 0f; s < len - 0.5f; s += 1f) line.Add(Vector3.Lerp(q[i], q[i + 1], s / len));
            }
            int bins = line.Count / 100 + 1, all = 0, samples = 0, through = 0;
            var count = new int[bins];
            var max = new float[bins];
            float worst = 0f;
            int poked = 0;   // ground over the slab anywhere across it
            for (int k = 0; k < line.Count; k++)
            {
                Vector3 tan = line[Mathf.Min(k + 1, line.Count - 1)] - line[Mathf.Max(k - 1, 0)];
                tan.y = 0f;
                Vector3 across = Vector3.Cross(tan.normalized, Vector3.up);
                foreach (float o in new[] { -2.7f, -2.2f, -1.5f, 1.5f, 2.2f, 2.7f })
                    if (Physics.Raycast(line[k] + across * o + Vector3.up * 3f, Vector3.down, out var eh, 8f, StaticMask, QueryTriggerInteraction.Ignore) && eh.collider is TerrainCollider) { poked++; break; }
            }
            Out($"deck edges: ground through the slab at {poked} of {line.Count} m");
            foreach (float side in new[] { 0f, -2f, 2f })
            {
                var ys = new float[line.Count];
                for (int k = 0; k < line.Count; k++)
                {
                    Vector3 tan = line[Mathf.Min(k + 1, line.Count - 1)] - line[Mathf.Max(k - 1, 0)];
                    tan.y = 0f;
                    Vector3 p = line[k] + Vector3.Cross(tan.normalized, Vector3.up) * side;
                    ys[k] = Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var h, 8f, StaticMask, QueryTriggerInteraction.Ignore) ? h.point.y : float.NaN;
                    if (side == 0f && !float.IsNaN(ys[k]) && h.collider is TerrainCollider) through++;
                }
                if (side == 0f)   // the middle line for trying a smoothing offline: metres along, x, z, the lane's y, the surface's y
                {
                    var csv = new StringBuilder("m,x,z,lane,surface\n");
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    for (int k = 0; k < line.Count; k++) csv.AppendFormat(inv, "{0},{1:F3},{2:F3},{3:F3},{4:F3}\n", k, line[k].x, line[k].z, line[k].y, ys[k]);
                    File.WriteAllText(Path.Combine(Application.dataPath, "../Bridge/deck_profile.csv"), csv.ToString());
                }
                for (int k = 1; k + 1 < line.Count; k++)
                {
                    float b = Mathf.Abs(ys[k] - (ys[k - 1] + ys[k + 1]) * 0.5f);
                    if (float.IsNaN(b)) continue;
                    samples++;
                    int bin = k / 100;
                    max[bin] = Mathf.Max(max[bin], b);
                    worst = Mathf.Max(worst, b);
                    if (b > 0.06f) { all++; count[bin]++; }
                }
            }
            var sb = new StringBuilder($"deck all: {line.Count} m, {samples} samples, bumps > 6 cm {all}, worst {worst:F2} m, ground first (middle line) {through}\n");
            for (int b = 0; b < bins; b++)
            {
                int k = Mathf.Min(b * 100 + 50, line.Count - 1);
                sb.Append($"  {b * 100,5} m ({line[k].x:F0},{line[k].z:F0}): bumps {count[b]}, worst {max[b]:F2}\n");
            }
            Out(sb.ToString());
        }

        // ------------------------------------------------------------------ a route
        static (int r, int i) NearestPoint(RoadNetwork net, Vector2 p)
        {
            int br = -1, bi = 0;
            float bd = float.MaxValue;
            for (int r = 0; r < net.roads.Length; r++)
            {
                var q = net.roads[r].pts;
                for (int i = 0; i < q.Length; i++)
                {
                    float d = new Vector2(q[i].x - p.x, q[i].z - p.y).sqrMagnitude;
                    if (d < bd) { bd = d; br = r; bi = i; }
                }
            }
            return (br, bi);
        }

        static float Run(Vector3[] q, int from, int to)
        {
            float s = 0f;
            int st = to >= from ? 1 : -1;
            for (int k = from; k != to; k += st) s += Vector3.Distance(q[k], q[k + st]);
            return s;
        }

        /// <summary>The roads to drive (road, way) from (sr, si) to (gr, gi): Dijkstra over (road, way, entry point), moving
        /// only the way a one-way road allows, from a road's end onto the roads RoadChoice.Onward offers there.</summary>
        public static List<(int r, int d)> Plan(RoadNetwork net, int sr, int si, int gr, int gi, int startDir = 0) =>
            RoadPlanner.Plan(net, sr, si, gr, gi, startDir);   // in the game now (the menu's bus ride: BusTour)

        static TrafficCar testCar;
        static double nextTick, startT;
        static float stoppedFor, lastLogT;
        static int stops, captures;
        static Vector3 lastPos;
        static float travelled;
        static readonly List<string> stopLog = new List<string>();
        static List<(int r, int d)> testSteps;
        static int offRoute;
        static readonly List<(string name, float secs, float metres, int stops)> legs = new List<(string, float, float, int)>();
        static int legRoad;
        static int[] routeBase;

        public static void RouteDrive()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("enter Play first (PlayTest.Begin)");
            var tm = Manager();
            var net = tm.network;
            Vector2 a = new Vector2(874.6f, -1183.6f), g = new Vector2(-59.6f, 50.2f);   // New Bus Station -> Nainital Stadium (the Flats / DSA ground)
            string arg = Arg();
            if (arg.Contains(";"))
            {
                var ps = arg.Split(';');
                var p0 = ps[0].Split(','); var p1 = ps[1].Split(',');
                a = new Vector2(float.Parse(p0[0]), float.Parse(p0[1]));
                g = new Vector2(float.Parse(p1[0]), float.Parse(p1[1]));
            }
            var (sr, si) = NearestPoint(net, a);
            var (gr, gi) = NearestPoint(net, g);
            var steps = Plan(net, sr, si, gr, gi);
            File.WriteAllText(LogPath, "");
            if (steps == null) { Out($"route: NO ROUTE from {net.roads[sr].name} #{sr} pt {si} to {net.roads[gr].name} #{gr} pt {gi}"); return; }
            var sb = new StringBuilder($"route {a} -> {g}: {steps.Count} roads:");
            foreach (var s in steps) sb.Append($" [{net.roads[s.r].name} #{s.r} {(s.d > 0 ? "+" : "-")}]");
            Out(sb.ToString());

            if (testCar) Object.Destroy(testCar.gameObject);
            var lib = VehicleLibrary.Instance;
            int model = lib ? lib.Pick(CarType.Taxi, 0.3f) : -1;
            var go = VehicleFactory.Create(CarType.Taxi, Px.CarWhite, tm.material, true, "RouteTest Taxi", model);
            go.transform.SetParent(tm.transform, true);
            var t = go.AddComponent<TrafficCar>();
            t.type = CarType.Taxi;
            t.cruise = 11f;
            t.Begin(net, sr, si, steps[0].d);
            Vector3 goal = net.roads[gr].pts[gi];
            t.FollowRoute(steps, goal);
            testSteps = steps;
            offRoute = 0;
            legs.Clear();
            legRoad = -1;
            routeBase = new[] { TrafficCar.BackOffs, TrafficCar.Hits, TrafficCar.Hops, TrafficCar.Dodges, TrafficCar.Manoeuvres, TrafficCar.Astrays };
            testCar = t;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(t.transform, 9f);
            if (!Jetpack.Flying) Jetpack.Toggle(PlayerController.Instance);
            startT = EditorApplication.timeSinceStartup;
            nextTick = startT + 1.0;
            slowFor = slowTotal = slowLongest = 0f;
            stoppedFor = 0f; stops = 0; captures = 0; travelled = 0f; lastPos = t.transform.position; lastLogT = 0f;
            stopLog.Clear();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>Fed and rested for a test (PlayTest.Begin puts the player's own save back afterwards).</summary>
        public static void Feed2() => Feed();   // (readiness after the per-case log)

        public static void Feed()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("enter Play first (PlayTest.Begin)");
            PlayerStats.DebugHunger(1f);
            PlayerStats.DebugEnergy(1f);
            Out("fed for the test");
        }

        public static void RouteStop()
        {
            EditorApplication.update -= Tick;
            if (testCar) Object.Destroy(testCar.gameObject);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(null, 0f);
            Out("route test stopped");
        }

        internal static string InFront(TrafficCar t)
        {
            var info = t.GetComponent<VehicleInfo>();
            float half = info ? info.length * 0.5f : 2f;
            Vector3 f = new Vector3(t.transform.forward.x, 0f, t.transform.forward.z).normalized;
            var hits = Physics.SphereCastAll(t.transform.position + Vector3.up * 0.9f + f * (half + 0.2f), 0.7f, f, 9f, ~((1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(t.transform)) continue;
                return $"{h.collider.name}{(h.collider.transform.parent ? "<" + h.collider.transform.parent.name : "")} (layer {h.collider.gameObject.layer}) at {h.distance:F1} m";
            }
            return "nothing within 9 m";
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || !testCar) { EditorApplication.update -= Tick; if (!testCar) Out("route test: the car is gone (destroyed / left)"); return; }
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick += 1.0;
            float el = (float)(EditorApplication.timeSinceStartup - startT);
            var t = testCar;
            Vector3 pos = t.transform.position;
            travelled += Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(lastPos.x, 0f, lastPos.z));
            float step = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(lastPos.x, 0f, lastPos.z));
            if (t.Road != legRoad) { legRoad = t.Road; legs.Add(($"#{t.Road} {t.net.roads[t.Road].name}", 0f, 0f, 0)); }
            var leg = legs[legs.Count - 1];
            legs[legs.Count - 1] = (leg.name, leg.secs + 1f, leg.metres + step, leg.stops + (stoppedFor == 2f && t.Speed < 0.5f && !t.Arrived ? 1 : 0));
            if (!testSteps.Contains((t.Road, t.Dir))) offRoute++;
            lastPos = pos;
            // the player hangs over the car on the jetpack, so the town's traffic and people stay round it
            var pc = PlayerController.Instance;
            if (pc && Jetpack.Flying) pc.GetComponent<CharacterMotor>().Teleport(pos + Vector3.up * 22f - t.transform.forward * 6f);
            float sp = t.Speed;
            if (sp < 0.5f && !t.Arrived) stoppedFor += 1f; else { if (stoppedFor >= 3f) stopLog.Add($"    (moving again after {stoppedFor:F0} s)"); stoppedFor = 0f; }
            if (stoppedFor == 3f)
            {
                stops++;
                string s = $"  STOP {stops} at {el:F0}s ({pos.x:F1},{pos.y:F1},{pos.z:F1}) {t.RouteInfo} | {t.State} | in front: {InFront(t)}";
                stopLog.Add(s);
                Out(s);
                if (captures < 12) { captures++; ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, $"../Shots/play_route_stop{stops}.png"))); }
            }
            if (sp < 2f && !t.Arrived) { slowFor += 1f; slowTotal += 1f; slowLongest = Mathf.Max(slowLongest, slowFor); } else slowFor = 0f;
            if (slowFor == 10f) Out($"  SLOW at {el:F0}s ({pos.x:F1},{pos.z:F1}) {sp:F1} m/s for 10 s, behind someone on foot {t.BehindWalker:F0} s | in front: {InFront(t)} | last hit {t.LastHit}");
            if (el - lastLogT >= 5f) { lastLogT = el; Out($"  {el:F0}s ({pos.x:F0},{pos.y:F1},{pos.z:F0}) {sp:F1} m/s | {t.RouteInfo} | {t.State} | {travelled:F0} m"); }
            if (t.Arrived || el > 480f)
            {
                EditorApplication.update -= Tick;
                foreach (var l in legs) Out($"  leg {l.name}: {l.secs:F0} s, {l.metres:F0} m, {l.metres / Mathf.Max(1f, l.secs) * 3.6f:F0} km/h, stops {l.stops}");
                Out($"  off route {offRoute} s; during it, all traffic: back-offs {TrafficCar.BackOffs - routeBase[0]}, hits {TrafficCar.Hits - routeBase[1]}, hops {TrafficCar.Hops - routeBase[2]}, dodges {TrafficCar.Dodges - routeBase[3]}, manoeuvres {TrafficCar.Manoeuvres - routeBase[4]}, gave up a turn {TrafficCar.Astrays - routeBase[5]}");
                Out($"  under 2 m/s: {slowTotal:F0} s in all, longest {slowLongest:F0} s; last hit {t.LastHit}");
                Out($"route test {(t.Arrived ? "ARRIVED" : "TIMED OUT")} after {el:F0} s, {travelled:F0} m driven, {stops} stops of 3 s+, most tries {t.MostTries}, hits here {t.HitsHere}, passes {t.PassesHere}; all traffic so far: back-offs {TrafficCar.BackOffs}, hits {TrafficCar.Hits}, hops {TrafficCar.Hops}");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/play_route_end.png")));
            }
        }

        // ------------------------------------------------------------------ every turn
        // held to a walking pace is a jam as well (22 Nov 2026: 1.1 m/s for six minutes, counted as "0 stops")
        static float slowFor, slowTotal, slowLongest;

        /// <summary>Answers once this assembly is the one loaded (bridge readiness after a recompile).</summary>
        /// <summary>A junction as a vehicle meets it (Bridge/arg.txt "x,z"): 48 x 40 m every metre, north up - '#' a thing
        /// 0.45-1.6 m over the drivable surface, a digit = that surface's height over the terrain in decimetres ('.' = the
        /// terrain itself), and the names of what stands there -> Bridge/junction_map.txt.</summary>
        public static void JunctionScan()
        {
            Manager();   // (opens the scene)
            var ps = Arg().Split(',');
            float cx = float.Parse(ps[0]), cz = float.Parse(ps[1]);
            var terrain = Object.FindAnyObjectByType<Terrain>();
            Physics.SyncTransforms();
            var names = new SortedDictionary<string, string>();
            var buf = new Collider[12];
            var sb = new StringBuilder($"junction ({cx},{cz}): left = x {cx - 24}, top = z {cz + 20}, 1 m a character\n");
            for (float z = cz + 20f; z >= cz - 20f; z -= 1f)
            {
                sb.Append($"{z,6:F0} ");
                for (float x = cx - 24f; x <= cx + 24f; x += 1f)
                {
                    var p = new Vector3(x, 0f, z);
                    float ground = terrain.SampleHeight(p) + terrain.transform.position.y, top = ground;
                    if (Physics.Raycast(new Vector3(x, ground + 1.2f, z), Vector3.down, out var down, 2f, StaticMask, QueryTriggerInteraction.Ignore)) top = down.point.y;
                    char c = top - ground < 0.06f ? '.' : (char)('0' + Mathf.Clamp(Mathf.RoundToInt((top - ground) * 10f), 1, 9));
                    if (c != '.' && !names.ContainsKey("surface " + down.collider.name)) names["surface " + down.collider.name] = $"{x:F0},{z:F0}";
                    int n = Physics.OverlapBoxNonAlloc(new Vector3(x, top + 1.025f, z), new Vector3(0.45f, 0.575f, 0.45f), buf, Quaternion.identity, StaticMask, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < n; k++)
                    {
                        c = buf[k] is TerrainCollider ? '^' : '#';
                        string name = buf[k].name + (buf[k].transform.parent ? "<" + buf[k].transform.parent.name : "");
                        if (!names.ContainsKey(name)) names[name] = $"{x:F0},{z:F0}";
                    }
                    sb.Append(c);
                }
                sb.Append('\n');
            }
            foreach (var kv in names) sb.Append($"  {kv.Key} first at {kv.Value}\n");
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/junction_map.txt")), sb.ToString());
        }

        sealed class Trial
        {
            public int r, start, d, o = -1, entry, od;
            public float yaw;
            public Vector3 at, from;
            public TrafficCar car;
            public double t0;
            public bool wasOn;
        }

        static readonly List<Trial> waiting = new List<Trial>(), running = new List<Trial>();
        static readonly List<string> bad = new List<string>();
        static int turnsClean, turnsBacked, turnsForced, turnsFailed, recClean, recOther, recFailed, trialCount;
        static double trialNext, trialT0;

        public static void TurnTest()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("enter Play first (PlayTest.Begin)");
            var net = Manager().network;
            waiting.Clear(); running.Clear(); bad.Clear();
            turnsClean = turnsBacked = turnsForced = turnsFailed = recClean = recOther = recFailed = 0;
            for (int r = 0; r < net.roads.Length; r++)
            {
                var p = net.roads[r].pts;
                int ow = net.roads[r].oneWay;
                foreach (int d in new[] { 1, -1 })
                {
                    if (ow != 0 && d != ow) continue;
                    int end = d > 0 ? p.Length - 1 : 0, start = Mathf.Clamp(end - d * 6, 0, p.Length - 1);
                    foreach (var w in RoadChoice.Onward(net, r, end))
                        waiting.Add(new Trial { r = r, start = start, d = d, o = w.r, entry = w.i, od = w.d, at = p[end] });
                }
            }
            bool islandOnly = Arg() == "island";   // only the island's turns, twice each (one at a time: 3-4 min)
            if (islandOnly)
            {
                waiting.RemoveAll(w => !net.NearIsland(w.at, 4f));
                var once = waiting.ToArray();
                for (int k = 1; k < 2; k++)
                    foreach (var w in once) waiting.Add(new Trial { r = w.r, start = w.start, d = w.d, o = w.o, entry = w.entry, od = w.od, at = w.at });
            }
            int turns = waiting.Count;
            float[] yaws = { 90f, -90f, 140f, 180f };   // left pointing wrong: across the road, back the way it came
            int n = 0;
            for (int r = 0; !islandOnly && r < net.roads.Length && n < 40; r++)
            {
                var p = net.roads[r].pts;
                if (p.Length < 30 || net.roads[r].oneWay != 0) continue;
                waiting.Add(new Trial { r = r, start = p.Length / 2, d = n % 2 == 0 ? 1 : -1, yaw = yaws[n % 4], at = p[p.Length / 2] });
                n++;
            }
            trialCount = waiting.Count;
            trialT0 = EditorApplication.timeSinceStartup;
            trialNext = 0;
            Out($"turn test: {turns} turns, {n} cars left pointing the wrong way");
            EditorApplication.update -= TrialTick;
            EditorApplication.update += TrialTick;
        }

        static void StartTrial(Trial t, TrafficManager tm)
        {
            var lib = VehicleLibrary.Instance;
            var go = VehicleFactory.Create(CarType.Taxi, Px.CarWhite, tm.material, true, "TurnTest Taxi", lib ? lib.Pick(CarType.Taxi, 0.3f) : -1);
            go.transform.SetParent(tm.transform, true);
            var c = go.AddComponent<TrafficCar>();
            c.type = CarType.Taxi;
            c.cruise = 10f;
            c.Begin(tm.network, t.r, t.start, t.d);
            if (t.o >= 0)
            {
                var q = tm.network.roads[t.o].pts;
                c.FollowRoute(new List<(int r, int d)> { (t.r, t.d), (t.o, t.od) }, q[t.od > 0 ? q.Length - 1 : 0]);
            }
            else
            {
                var body = go.GetComponent<Rigidbody>();
                go.transform.rotation = Quaternion.AngleAxis(t.yaw, Vector3.up) * go.transform.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();
            }
            t.car = c;
            t.from = go.transform.position;
            t.t0 = EditorApplication.timeSinceStartup;
            running.Add(t);
        }

        static void TrialTick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= TrialTick; return; }
            double now = EditorApplication.timeSinceStartup;
            if (now < trialNext) return;
            trialNext = now + 0.2;
            var tm = Manager();
            var net = tm.network;
            for (int k = running.Count - 1; k >= 0; k--)
            {
                var t = running[k];
                float el = (float)(now - t.t0);
                bool turn = t.o >= 0, over = el > 40f || !t.car, ok = false;
                if (t.car)
                {
                    var c = t.car;
                    c.LaneAhead(3f, out var way);
                    Vector3 f = c.transform.forward;
                    f.y = 0f;
                    bool facing = Vector3.Angle(f, way) < 50f;
                    if (turn)
                    {
                        if (c.Road == t.o && c.Dir == t.od) t.wasOn = true;
                        ok = t.wasOn && (c.Arrived || c.Road != t.o || (facing && (c.Idx - t.entry) * t.od >= 3));
                    }
                    else ok = facing && el > 1.5f && c.Speed > 1.5f && (c.transform.position - t.from).sqrMagnitude > 100f;
                }
                if (!ok && !over) continue;
                string where = turn
                    ? $"turn #{t.r} {net.roads[t.r].name} -> #{t.o} {net.roads[t.o].name} ({(t.od > 0 ? "+" : "-")}) at ({t.at.x:F0},{t.at.z:F0})"
                    : $"wrong way {t.yaw} deg on #{t.r} {net.roads[t.r].name} at ({t.at.x:F0},{t.at.z:F0})";
                if (!ok)
                {
                    if (turn) turnsFailed++; else recFailed++;
                    bad.Add($"  FAIL {where}: {(t.car ? $"({t.car.transform.position.x:F1},{t.car.transform.position.z:F1}) {t.car.State} | in front: {InFront(t.car)}" : "the car is gone")}");
                }
                else if (!turn)
                {
                    if (t.car.Road == t.r && t.car.Dir != t.d) { recOther++; bad.Add($"  OTHER WAY {where}: drove on the way it faced after {el:F0} s"); }
                    else recClean++;
                }
                else if (t.car.HopsHere > 0) { turnsForced++; bad.Add($"  FORCED {where}: hits {t.car.HitsHere}, hops {t.car.HopsHere}, {el:F0} s; last hit {t.car.LastHit}"); }
                else if (t.car.MostTries > 0) { turnsBacked++; bad.Add($"  BACKED {where}: {t.car.MostTries} back-offs, {el:F0} s; hit {t.car.LastHit}"); }
                else turnsClean++;
                Out($"  case {trialCount - waiting.Count - running.Count + 1}/{trialCount} {(ok ? "ok" : "FAIL")} {el:F0} s, back-offs {(t.car ? t.car.MostTries : -1)}, hits {(t.car ? t.car.HitsHere : -1)}, hops {(t.car ? t.car.HopsHere : -1)}; planned ways asked {DrivePath.Asked} found {DrivePath.Found} driven {TrafficCar.PathsDone} dropped {TrafficCar.PathsDropped} | {where}" + (ok || !t.car ? "" : $" | at ({t.car.transform.position.x:F1},{t.car.transform.position.z:F1}) {t.car.State} | in front: {InFront(t.car)}"));
                if (t.car) Object.Destroy(t.car.gameObject);
                running.RemoveAt(k);
            }
            while (running.Count < 8)
            {
                int pick = -1;
                for (int i = 0; i < waiting.Count && pick < 0; i++)
                {
                    bool apart = true;
                    foreach (var r in running)
                        if ((r.at - waiting[i].at).sqrMagnitude < 90f * 90f || (r.car && (r.car.transform.position - waiting[i].at).sqrMagnitude < 50f * 50f)) { apart = false; break; }
                    if (apart) pick = i;
                }
                if (pick < 0) break;
                var next = waiting[pick];
                waiting.RemoveAt(pick);
                StartTrial(next, tm);
            }
            if (running.Count > 0 || waiting.Count > 0) return;
            EditorApplication.update -= TrialTick;
            var sb = new StringBuilder();
            foreach (var b in bad) sb.Append(b).Append('\n');
            sb.Append($"turn test done: {trialCount} cases in {now - trialT0:F0} s | turns: clean {turnsClean}, with back-offs {turnsBacked}, put on its lane unseen (hop) {turnsForced}, FAILED {turnsFailed}" +
                      $" | pointing wrong: back on the lane {recClean}, took the road the other way {recOther}, FAILED {recFailed}");
            Out(sb.ToString());
        }

        // ------------------------------------------------------------------ aar-paar
        sealed class Veh { public Rigidbody rb; public Transform t; public Vector3 c, half; public Collider[] cols; public string kind; }
        static readonly List<Veh> vehs = new List<Veh>();
        static readonly Dictionary<long, double> seenPair = new Dictionary<long, double>();
        static double overEnd, overNext;
        static int overEvents, overSamples, colliderMiss, wallEvents, personEvents;
        static readonly Dictionary<Rigidbody, Veh> known = new Dictionary<Rigidbody, Veh>();
        static readonly Collider[] insideBuf = new Collider[24];

        /// <summary>A moving vehicle's middle (70 % of its width, 80 % of its length, clear of the ground) inside a static
        /// thing or a person.</summary>
        static void Inside(Veh v, double now)
        {
            if (v.rb.isKinematic ? !v.rb.GetComponent<TrafficBike>() : v.rb.linearVelocity.sqrMagnitude < 1f) return;
            Vector3 c = v.t.TransformPoint(v.c) + v.t.up * (v.half.y * 0.2f);
            int n = Physics.OverlapBoxNonAlloc(c, new Vector3(v.half.x * 0.7f, v.half.y * 0.35f, v.half.z * 0.8f), insideBuf, v.t.rotation, ~((1 << 2) | (1 << 4) | (1 << VehicleFactory.VehicleLayer)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var o = insideBuf[i];
                if (o is TerrainCollider || o.transform.IsChildOf(v.t)) continue;
                bool person = o.gameObject.layer == 8 || o.gameObject.layer == 9;
                if (person && !(o is CharacterController)) continue;
                long key = ((long)v.rb.GetInstanceID() << 32) ^ (uint)o.GetInstanceID();
                if (seenPair.TryGetValue(key, out var last) && now - last < 4.0) continue;
                seenPair[key] = now;
                if (person) personEvents++; else wallEvents++;
                var ride = v.rb.GetComponent<BikeRide>();
                Out($"  INSIDE {(person ? "person" : "static")} {o.name} at ({c.x:F0},{c.y:F1},{c.z:F0}): {What(v)}" +
                    (ride ? $" | box {(c.y - v.t.position.y - v.half.y * 0.35f):F2}..{(c.y - v.t.position.y + v.half.y * 0.35f):F2} m up, width {v.half.x * 1.4f:F2}, its own wall test says {(ride.InStaticNow ? "in" : "clear")}" : ""));
            }
        }

        public static void Overlaps()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("enter Play first (PlayTest.Begin)");
            float secs = 60f;
            float.TryParse(Arg(), out secs);
            if (secs <= 0f) secs = 60f;
            overEnd = EditorApplication.timeSinceStartup + secs;
            overNext = 0;
            overEvents = overSamples = colliderMiss = wallEvents = personEvents = 0;
            known.Clear();
            seenPair.Clear();
            Out($"overlaps: watching every vehicle pair for {secs:F0} s");
            EditorApplication.update -= OverTick;
            EditorApplication.update += OverTick;
        }

        static Veh Make(Rigidbody rb)
        {
            var v = new Veh { rb = rb, t = rb.transform };
            Bounds b = default; bool first = true;
            foreach (var r in rb.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.layer == 9 || r.gameObject.layer == 8) continue;   // the people in it
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 l = v.t.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (first) { b = new Bounds(l, Vector3.zero); first = false; } else b.Encapsulate(l);
                }
            }
            v.c = b.center;
            v.half = b.extents * 0.92f;   // a little inside the body: mirrors, the odd overhang
            var list = new List<Collider>();
            foreach (var c in rb.GetComponentsInChildren<Collider>()) if (c && !c.isTrigger && c.gameObject.layer != 8 && c.gameObject.layer != 9 && c.attachedRigidbody == rb) list.Add(c);
            v.cols = list.ToArray();
            v.kind = rb.GetComponent<TrafficCar>() ? "traffic" : rb.GetComponent<TrafficBike>() ? "bike" : rb.GetComponent<RouteDriver>() ? "police/108" : rb.GetComponent<CarController>() && CarController.Driving == rb.GetComponent<CarController>() ? "PLAYER" : rb.isKinematic ? "kinematic" : "other";
            return v;
        }

        /// <summary>How deep two oriented boxes overlap (separating axes on the ground plane + the vertical), 0 = apart.</summary>
        static float BoxOverlap(Veh a, Veh b)
        {
            Vector3 ca = a.t.TransformPoint(a.c), cb = b.t.TransformPoint(b.c);
            float ya = Mathf.Abs(ca.y - cb.y) - (a.half.y + b.half.y);
            if (ya > 0f) return 0f;
            Vector3[] axes = { a.t.right, a.t.forward, b.t.right, b.t.forward };
            float min = float.MaxValue;
            foreach (var ax0 in axes)
            {
                Vector3 ax = new Vector3(ax0.x, 0f, ax0.z);
                if (ax.sqrMagnitude < 1e-4f) continue;
                ax.Normalize();
                float ra = Mathf.Abs(Vector3.Dot(a.t.right, ax)) * a.half.x + Mathf.Abs(Vector3.Dot(a.t.forward, ax)) * a.half.z;
                float rb = Mathf.Abs(Vector3.Dot(b.t.right, ax)) * b.half.x + Mathf.Abs(Vector3.Dot(b.t.forward, ax)) * b.half.z;
                float sep = Mathf.Abs(Vector3.Dot(cb - ca, ax)) - (ra + rb);
                if (sep > 0f) return 0f;
                min = Mathf.Min(min, -sep);
            }
            return min;
        }

        static float ColliderDepth(Veh a, Veh b)
        {
            float best = 0f;
            foreach (var x in a.cols)
                foreach (var y in b.cols)
                    if (x && y && Physics.ComputePenetration(x, x.transform.position, x.transform.rotation, y, y.transform.position, y.transform.rotation, out _, out float d))
                        best = Mathf.Max(best, d);
            return best;
        }

        static string What(Veh v)
        {
            var tc = v.rb.GetComponent<TrafficCar>();
            float sp = v.rb.linearVelocity.magnitude;
            return $"{v.t.name} [{v.kind}] {sp:F1} m/s{(tc ? " " + tc.State : "")}";
        }

        static void OverTick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= OverTick; return; }
            double now = EditorApplication.timeSinceStartup;
            if (now < overNext) return;
            overNext = now + 0.25;
            if (now > overEnd)
            {
                EditorApplication.update -= OverTick;
                Out($"overlaps done: {overSamples} samples, {overEvents} pairs seen overlapping (> 0.25 m into each other), of them colliders not touching {colliderMiss}; vehicles inside a static thing {wallEvents}, inside a person {personEvents}");
                return;
            }
            overSamples++;
            vehs.Clear();
            foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                if (rb && rb.gameObject.layer == VehicleFactory.VehicleLayer)
                {
                    if (!known.TryGetValue(rb, out var v)) known[rb] = v = Make(rb);
                    vehs.Add(v);
                    Inside(v, now);
                }
            for (int i = 0; i < vehs.Count; i++)
                for (int j = i + 1; j < vehs.Count; j++)
                {
                    var a = vehs[i]; var b = vehs[j];
                    if ((a.t.position - b.t.position).sqrMagnitude > 15f * 15f) continue;
                    float o = BoxOverlap(a, b);
                    if (o < 0.25f) continue;
                    long key = ((long)Mathf.Min(a.rb.GetInstanceID(), b.rb.GetInstanceID()) << 32) ^ (uint)Mathf.Max(a.rb.GetInstanceID(), b.rb.GetInstanceID());
                    if (seenPair.TryGetValue(key, out var last) && now - last < 4.0) continue;
                    seenPair[key] = now;
                    overEvents++;
                    float cd = ColliderDepth(a, b);
                    if (cd < 0.02f) colliderMiss++;
                    Out($"  AAR-PAAR {o:F2} m (colliders {(cd < 0.02f ? "NOT touching" : $"in {cd:F2} m")}) at ({a.t.position.x:F0},{a.t.position.y:F1},{a.t.position.z:F0}): {What(a)}  <->  {What(b)}");
                    if (overEvents <= 6) ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, $"../Shots/play_overlap{overEvents}.png")));
                }
        }
    }
}

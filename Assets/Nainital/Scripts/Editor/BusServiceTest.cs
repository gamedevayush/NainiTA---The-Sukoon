using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The bus timetable's routes on the built road network, no Play (24 Nov 2026): "call BusServiceTest.Routes" (batch or
    /// the bridge). Plans every BusLines line as BusService does (stops snapped to a bus-wide road, RoadPlanner keeping the
    /// one-ways) and writes Logs/busroutes.txt: per line its km, roads, minutes at the ghost's pace, each stop's distance
    /// from its road, and the roads it drives (so the Mall's and Mallital's one-ways can be read off). In Play: "call
    /// BusServiceTest.Report" lists the trips (ghost / real).
    /// </summary>
    public static class BusServiceTest
    {
        public static void Routes()
        {
            if (!Application.isPlaying && !Object.FindAnyObjectByType<TrafficManager>())
                EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            var tm = Object.FindAnyObjectByType<TrafficManager>();
            var net = tm ? tm.network : null;
            var sb = new StringBuilder();
            if (!net) { sb.AppendLine("no road network"); Write(sb); return; }
            foreach (var l in BusLines.All)
            {
                sb.Append($"{l.id}: ");
                int n = l.stops.Length;
                var rs = new int[n]; var ix = new int[n];
                var far = new float[n];
                for (int k = 0; k < n; k++)
                {
                    float best = float.MaxValue;
                    for (int ri = 0; ri < net.roads.Length; ri++)
                    {
                        var road = net.roads[ri];
                        if (road.width < 4.5f || road.pts.Length < 3) continue;
                        if (l.stops[k].road != null && road.name != l.stops[k].road) continue;
                        for (int i = 1; i < road.pts.Length - 1; i++)
                        {
                            float d = new Vector2(road.pts[i].x - l.stops[k].at.x, road.pts[i].z - l.stops[k].at.y).sqrMagnitude;
                            if (d < best) { best = d; rs[k] = ri; ix[k] = i; }
                        }
                    }
                    far[k] = Mathf.Sqrt(best);
                }
                float km = 0f;
                int roads = 0, way = 0;
                bool ok = true;
                var names = new StringBuilder();
                for (int k = 0; k + 1 < n && ok; k++)
                {
                    var leg = RoadPlanner.PlanEntries(net, rs[k], ix[k], rs[k + 1], ix[k + 1], way);
                    if (leg == null && way != 0) { leg = RoadPlanner.PlanEntries(net, rs[k], ix[k], rs[k + 1], ix[k + 1], 0); if (leg != null) sb.Append($"(turns round at {l.stops[k].name}) "); }
                    if (leg == null) { sb.Append($"NO WAY {l.stops[k].name} -> {l.stops[k + 1].name}; "); ok = false; break; }
                    roads += leg.Count;
                    way = leg[leg.Count - 1].d;
                    for (int s = 0; s < leg.Count; s++)
                    {
                        var (r, d, i) = leg[s];
                        var q = net.roads[r].pts;
                        int to = s + 1 < leg.Count ? Nearest(q, net.roads[leg[s + 1].r].pts[leg[s + 1].i], i, d) : ix[k + 1];
                        km += RoadPlanner.Run(q, i, to) / 1000f;
                        string nm = string.IsNullOrEmpty(net.roads[r].name) ? "#" + r : net.roads[r].name;
                        if (net.roads[r].oneWay != 0) nm += "(1-way)";
                        names.Append(nm).Append(d > 0 ? "+" : "-").Append(' ');
                    }
                    names.Append("| ");
                }
                if (ok) sb.Append($"{km:F1} km, {roads} roads, ~{km * 1000f / 7f / 60f:F0} min driving");
                sb.Append("\n  stops: ");
                for (int k = 0; k < n; k++) sb.Append($"{l.stops[k].name} ({far[k]:F0} m off){(k + 1 < n ? ", " : "")}");
                sb.Append("\n  roads: ").Append(names).Append('\n');
            }
            Write(sb);
        }

        static int Nearest(Vector3[] q, Vector3 p, int from, int d)
        {
            int b = from; float bd = float.MaxValue;
            for (int i = from; i >= 0 && i < q.Length; i += d) { float x = (q[i] - p).sqrMagnitude; if (x < bd) { bd = x; b = i; } }
            return b;
        }

        static void Write(StringBuilder sb)
        {
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/busroutes.txt", sb.ToString());
            Debug.Log("[Nainital] bus routes\n" + sb);
        }

        public static void Report()
        {
            var s = BusService.Instance;
            Debug.Log("[Nainital] " + (s ? s.Report() : "no bus service"));
            System.IO.File.WriteAllText("Logs/bustrips.txt", s ? s.Report() : "no bus service");
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// How dark the town is at night, place by place (owner, 21 Nov 2026: "raat ki overall lighting pe kaam krna pdega...
    /// abhi bhi bahut area blackout ya dimmed hain"). In play mode: street-level viewpoints on every kind of road wherever
    /// houses stand (one per 120 m cell), the camera at eye height looking along the road at 21:00, the real pooled lamp
    /// lights given time to move there; each view is saved small (Shots/night/NN.png) and measured: the share of the
    /// lower two thirds (street, walls) that is near black, and its mean brightness. The report (Shots/night/report.csv
    /// and the log) lists the darkest places with the lamps / shop lights near them.
    /// Bridge: call PlayTest.Begin, wait, call NightCheck.Run (arg: hour, default 21; "max N" views); NightCheck.Report.
    /// </summary>
    public static class NightCheck
    {
        class View { public Vector3 pos; public Vector3 dir; public string road, kind; public int houses; public float dark, mean; public int lamps, shops; }

        static List<View> views;
        static int at;
        static double waitUntil;
        static Camera cam;
        static ThirdPersonCamera follow;
        static RenderTexture rt;
        static Texture2D read;
        static string dir;
        static float hourWas;
        static bool pausedWas;

        static string Arg() { try { return File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { return ""; } }

        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("play mode first (PlayTest.Begin)");
            var parts = Arg().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            float hour = 21f;
            int max = 400;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "max" && i + 1 < parts.Length) int.TryParse(parts[++i], out max);
                else float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out hour);
            }
            var w = JsonUtility.FromJson<WorldJson>(File.ReadAllText(Path.Combine(Application.dataPath, "Nainital/Data/world.json")));
            // houses on a 20 m grid for counting
            var grid = new Dictionary<long, int>();
            long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
            for (int i = 0; i + 8 < w.houses.Length; i += 9)
            {
                var k = Key(Mathf.FloorToInt(w.houses[i] / 20f), Mathf.FloorToInt(w.houses[i + 1] / 20f));
                grid[k] = grid.TryGetValue(k, out var c) ? c + 1 : 1;
            }
            int HousesNear(Vector2 p)
            {
                int n = 0, gx = Mathf.FloorToInt(p.x / 20f), gz = Mathf.FloorToInt(p.y / 20f);
                for (int dx = -2; dx <= 2; dx++) for (int dz = -2; dz <= 2; dz++) n += grid.TryGetValue(Key(gx + dx, gz + dz), out var c) ? c : 0;
                return n;
            }
            var cells = new Dictionary<long, View>();
            var lake = LakeArea.Instance;
            foreach (var r in w.roads)
            {
                var pts = Geo.ToPoints(r.pts);
                float carry = 0f;
                for (int i = 1; i < pts.Length; i++)
                {
                    Vector2 a = pts[i - 1], b = pts[i];
                    float len = Vector2.Distance(a, b);
                    for (float s = carry; s < len; s += 30f)
                    {
                        Vector2 p = Vector2.Lerp(a, b, s / Mathf.Max(len, 0.01f));
                        if (lake && LakeArea.Contains(new Vector3(p.x, 0f, p.y))) continue;
                        int h = HousesNear(p);
                        if (h < 8) continue;   // the town, not the forest roads
                        long ck = Key(Mathf.FloorToInt(p.x / 120f), Mathf.FloorToInt(p.y / 120f));
                        if (cells.TryGetValue(ck, out var old) && old.houses >= h) continue;
                        Vector2 t = (b - a).normalized;
                        cells[ck] = new View { pos = new Vector3(p.x, 0f, p.y), dir = new Vector3(t.x, 0f, t.y), road = r.name, kind = r.kind, houses = h };
                    }
                    carry = Mathf.Repeat(carry - len, 30f);
                }
            }
            views = new List<View>(cells.Values);
            views.Sort((x, y) => (x.pos.x + x.pos.z * 0.001f).CompareTo(y.pos.x + y.pos.z * 0.001f));
            if (views.Count > max) views.RemoveRange(max, views.Count - max);
            // ground height
            foreach (var v in views)
            {
                var t = Terrain.activeTerrain;
                float y = t ? t.SampleHeight(v.pos) + t.transform.position.y : 750f;
                if (Physics.Raycast(new Vector3(v.pos.x, y + 30f, v.pos.z), Vector3.down, out var hit, 60f, ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore) && hit.point.y < y + 6f)
                    y = Mathf.Max(y, hit.point.y);
                v.pos.y = y + 1.75f;
            }
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/night"));
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            var dn = DayNightCycle.Instance;
            if (dn) { hourWas = dn.timeOfDay; pausedWas = dn.paused; dn.timeOfDay = hour; dn.paused = true; dn.Apply(); }
            cam = Camera.main;
            follow = cam ? cam.GetComponent<ThirdPersonCamera>() : null;
            if (follow) follow.enabled = false;
            rt = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            read = new Texture2D(480, 270, TextureFormat.RGB24, false);
            at = -1;
            waitUntil = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log($"[Nainital] night check: {views.Count} views at {hour:F1} h -> {dir}");
        }

        static void Place(View v)
        {
            cam.transform.SetPositionAndRotation(v.pos, Quaternion.LookRotation(Quaternion.AngleAxis(6f, Vector3.Cross(v.dir, Vector3.up) * -1f) * v.dir, Vector3.up));
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || views == null || !cam) { Stop(); return; }
            if (EditorApplication.timeSinceStartup < waitUntil) { if (at >= 0) Place(views[at]); return; }
            if (at >= 0) Measure(views[at], at);
            at++;
            if (at >= views.Count) { Finish(); return; }
            Place(views[at]);
            waitUntil = EditorApplication.timeSinceStartup + 0.7;   // the pooled lamp lights move every 0.3 s
        }

        static void Measure(View v, int i)
        {
            var was = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = was;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            var px = read.GetPixels32();
            int dark = 0, n = 0;
            double sum = 0;
            int rows = rt.height * 2 / 3;   // the lower two thirds: street and walls (ReadPixels: row 0 = bottom)
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < rt.width; x++)
                {
                    var c = px[y * rt.width + x];
                    float l = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
                    sum += l;
                    if (l < 0.035f) dark++;
                    n++;
                }
            v.dark = dark / (float)n;
            v.mean = (float)(sum / n);
            var lm = Object.FindAnyObjectByType<LampManager>();
            if (lm)
            {
                foreach (var h in lm.lampHeads) if ((new Vector2(h.x - v.pos.x, h.z - v.pos.z)).sqrMagnitude < 30f * 30f) v.lamps++;
                if (lm.shopLights != null) foreach (var h in lm.shopLights) if ((new Vector2(h.x - v.pos.x, h.z - v.pos.z)).sqrMagnitude < 20f * 20f) v.shops++;
                if (lm.porchLights != null) foreach (var h in lm.porchLights) if ((new Vector2(h.x - v.pos.x, h.z - v.pos.z)).sqrMagnitude < 20f * 20f) v.shops++;   // shops + porch bulbs within 20 m
            }
            File.WriteAllBytes(Path.Combine(dir, $"{i:000}.png"), read.EncodeToPNG());
        }

        static void Finish()
        {
            var lines = new List<string> { "i,x,y,z,road,kind,houses,lamps30,shops20,dark,mean" };
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                lines.Add($"{i},{v.pos.x:F0},{v.pos.y:F0},{v.pos.z:F0},\"{v.road}\",{v.kind},{v.houses},{v.lamps},{v.shops},{v.dark:F3},{v.mean:F3}");
            }
            File.WriteAllLines(Path.Combine(dir, "report.csv"), lines);
            Debug.Log("[Nainital] night check done: " + Summary());
            Stop();
        }

        static string Summary()
        {
            if (views == null || views.Count == 0) return "no views";
            var s = new List<View>(views);
            float avgDark = 0f, avgMean = 0f;
            int black = 0, noLamp = 0;
            foreach (var v in s) { avgDark += v.dark; avgMean += v.mean; if (v.dark > 0.7f) black++; if (v.lamps == 0) noLamp++; }
            avgDark /= s.Count; avgMean /= s.Count;
            s.Sort((a, b) => b.dark.CompareTo(a.dark));
            var worst = new List<string>();
            for (int i = 0; i < Mathf.Min(12, s.Count); i++)
                worst.Add($"#{views.IndexOf(s[i])} ({s[i].pos.x:F0},{s[i].pos.z:F0}) {s[i].kind} '{s[i].road}' dark {s[i].dark:F2} lamps {s[i].lamps}");
            var kinds = new Dictionary<string, (int n, float d, int nl)>();
            foreach (var v in s) { kinds.TryGetValue(v.kind ?? "-", out var e); kinds[v.kind ?? "-"] = (e.n + 1, e.d + v.dark, e.nl + (v.lamps == 0 ? 1 : 0)); }
            var ks = new List<string>();
            foreach (var kv in kinds) ks.Add($"{kv.Key} {kv.Value.n} (dark {kv.Value.d / kv.Value.n:F2}, no lamp {kv.Value.nl})");
            return $"{s.Count} views, mean dark share {avgDark:F2}, mean brightness {avgMean:F3}, {black} over 70 % black, {noLamp} with no lamp within 30 m | by road kind: {string.Join("; ", ks)} | darkest: {string.Join("; ", worst)}";
        }

        public static void Report() => Debug.Log("[Nainital] night check: " + Summary());

        static void Stop()
        {
            EditorApplication.update -= Tick;
            if (follow) follow.enabled = true;
            var dn = DayNightCycle.Instance;
            if (dn) { dn.timeOfDay = hourWas; dn.paused = pausedWas; }
            if (rt) { rt.Release(); Object.DestroyImmediate(rt); rt = null; }
        }
    }
}

using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Nainital.EditorTools
{
    /// <summary>What the fake interiors cost (user, 2026-11-17: "phle check kro ki heavy to nhi pdega"). At street views
    /// with shops and windows: the share of the screen that runs the room code (pixels that change when it is switched
    /// off), and the time a frame takes with the rooms on and off, at 1080p and at 4K (4K is GPU-bound on this PC, so
    /// the difference there is the shader's own cost). On / off pictures go to Shots/int_*.png.
    /// Bridge: "call InteriorCost.Run". The switch is the global _NainitalInteriorOff (AtlasForwardPass.hlsl).</summary>
    public static class InteriorCost
    {
        static readonly string[] Markers = { "TallitalShotStreetView", "TallitalShotChowk", "MallitalShotPlaza", "MallShotLane", "MallShotA" };
        static readonly int OffId = Shader.PropertyToID("_NainitalInteriorOff");

        /// <summary>Bridge / batch "call InteriorCost.GlassDebug": the shop glass in flat colours per room tag for the next
        /// shots, and a log of the glass quads near the Nanak / Bata stretch (207, 55): tag, uv span, and any other glass
        /// quad lying in the same plane over it.</summary>
        public static void GlassDebug()
        {
            if (EditorSceneManager.GetActiveScene().path != WorldBuilder.ScenePath) EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            Shader.SetGlobalFloat("_NtGlassDebug", 1f);
            var quads = new System.Collections.Generic.List<(Vector3 c, Vector3 n, float hw, int tag, Vector2 u, string mesh)>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (!mf.name.StartsWith("Mall shop glass") || !mf.sharedMesh) continue;
                var m = mf.sharedMesh;
                var v = m.vertices; var nn = m.normals; var uv = m.uv; var col = m.colors32;
                for (int i = 0; i + 3 < v.Length; i += 4)
                {
                    Vector3 c = mf.transform.TransformPoint((v[i] + v[i + 1] + v[i + 2] + v[i + 3]) * 0.25f);
                    if (new Vector2(c.x - 207f, c.z - 55f).magnitude > 30f) continue;
                    bool same = col[i].r == col[i + 1].r && col[i].r == col[i + 2].r && col[i].r == col[i + 3].r;
                    quads.Add((c, nn[i], Vector3.Distance(v[i], v[i + 1]) * 0.5f, same ? col[i].r : -1, new Vector2(Mathf.Min(uv[i].x, uv[i + 1].x), Mathf.Max(uv[i].x, uv[i + 1].x)), mf.name));
                }
            }
            foreach (var q in quads)
            {
                int fights = 0;
                foreach (var o in quads)
                {
                    if (o.Equals(q)) continue;
                    Vector3 d = o.c - q.c;
                    if (Vector3.Dot(o.n, q.n) > 0.9f && Mathf.Abs(Vector3.Dot(d, q.n)) < 0.08f && new Vector2(d.x, d.z).magnitude < q.hw + o.hw) fights++;
                }
                Debug.Log($"[Nainital] glass quad at ({q.c.x:F1},{q.c.y:F1},{q.c.z:F1}) n ({q.n.x:F2},{q.n.z:F2}) half {q.hw:F2} tag {q.tag} u {q.u.x:F2}..{q.u.y:F2} in-plane overlaps {fights} [{q.mesh}]");
            }
        }

        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != WorldBuilder.ScenePath) EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            string dir = Path.GetFullPath("Shots");
            Directory.CreateDirectory(dir);
            var dn = Object.FindAnyObjectByType<DayNightCycle>();
            var camGo = new GameObject("InteriorCostCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 7200f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.dithering = false;   // the pixel count compares two frames: no per-frame noise
            try
            {
                foreach (string marker in Markers)
                {
                    var m = GameObject.Find(marker);
                    if (!m) { Debug.Log("[Nainital] interior cost: no " + marker); continue; }
                    cam.transform.position = m.transform.position;
                    cam.transform.rotation = Quaternion.LookRotation(m.transform.forward);
                    foreach (float hour in new[] { 11f, 20f })
                    {
                        if (dn) { dn.timeOfDay = hour; dn.Apply(); }
                        Shader.SetGlobalFloat("_NtLampGlow", dn ? Mathf.Clamp01((dn.NightFactor - 0.2f) / 0.4f) : 0f);
                        string tag = marker + (hour < 12f ? "_day" : "_night");
                        // pictures and the share of the screen
                        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };   // no MSAA: the GPU readback that drains the queue needs a plain texture
                        cam.targetTexture = rt;
                        var on = Grab(cam, rt, false);
                        var off = Grab(cam, rt, true);
                        int changed = 0;
                        var a = on.GetPixels32();
                        var b = off.GetPixels32();
                        for (int i = 0; i < a.Length; i++)
                            if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 6) changed++;
                        File.WriteAllBytes(Path.Combine(dir, "int_" + tag + "_on.png"), on.EncodeToPNG());
                        File.WriteAllBytes(Path.Combine(dir, "int_" + tag + "_off.png"), off.EncodeToPNG());
                        Object.DestroyImmediate(on);
                        Object.DestroyImmediate(off);
                        float share = 100f * changed / a.Length;
                        float on1080 = 0f, off1080 = 0f, on4k = 0f, off4k = 0f;
                        if (hour < 12f)   // timing once per view, by day
                        {
                            Time(cam, rt, out on1080, out off1080);
                            cam.targetTexture = null;
                            rt.Release();
                            rt = new RenderTexture(3840, 2160, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };   // no MSAA: the GPU readback that drains the queue needs a plain texture
                            cam.targetTexture = rt;
                            Time(cam, rt, out on4k, out off4k);
                        }
                        cam.targetTexture = null;
                        rt.Release();
                        Object.DestroyImmediate(rt);
                        if (hour < 12f)
                            Debug.Log($"[Nainital] interior cost {tag}: rooms on {share:F1}% of the screen; 1080p {off1080:F2} -> {on1080:F2} ms ({on1080 - off1080:+0.00;-0.00} ms), 4K {off4k:F2} -> {on4k:F2} ms ({on4k - off4k:+0.00;-0.00} ms, {100f * (on4k - off4k) / Mathf.Max(off4k, 0.01f):+0.0;-0.0}%)");
                        else
                            Debug.Log($"[Nainital] interior cost {tag}: rooms on {share:F1}% of the screen");
                    }
                }
            }
            finally
            {
                Shader.SetGlobalFloat(OffId, 0f);
                Object.DestroyImmediate(camGo);
            }
        }

        static Texture2D Grab(Camera cam, RenderTexture rt, bool off)
        {
            Shader.SetGlobalFloat(OffId, off ? 1f : 0f);
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Shader.SetGlobalFloat(OffId, 0f);
            return tex;
        }

        /// <summary>Median ms a frame with the rooms on and off, alternating 5 rounds of 20 frames each, the GPU drained
        /// before and after every round.</summary>
        static void Time(Camera cam, RenderTexture rt, out float onMs, out float offMs)
        {
            for (int i = 0; i < 6; i++) cam.Render();   // warm: culling caches, shader variants, textures
            Sync(rt);
            var on = new float[5];
            var off = new float[5];
            var sw = new Stopwatch();
            for (int r = 0; r < 5; r++)
                for (int pass = 0; pass < 2; pass++)
                {
                    bool roomsOff = ((r + pass) & 1) == 1;   // alternate which goes first
                    Shader.SetGlobalFloat(OffId, roomsOff ? 1f : 0f);
                    cam.Render();
                    Sync(rt);
                    sw.Restart();
                    for (int f = 0; f < 20; f++) cam.Render();
                    Sync(rt);
                    float ms = (float)sw.Elapsed.TotalMilliseconds / 20f;
                    if (roomsOff) off[r] = ms; else on[r] = ms;
                }
            Shader.SetGlobalFloat(OffId, 0f);
            System.Array.Sort(on);
            System.Array.Sort(off);
            onMs = on[2];
            offMs = off[2];
        }

        static void Sync(RenderTexture rt) =>
            AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1, TextureFormat.RGBA32).WaitForCompletion();
    }
}

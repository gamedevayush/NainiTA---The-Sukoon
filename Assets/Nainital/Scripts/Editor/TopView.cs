using System.Globalization;
using System.Linq;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nainital.EditorTools
{
    /// <summary>
    /// A north-up orthographic top view of the town, to lay over satellite imagery (user, 2026-11-15: "google map se har
    /// building ke shape check krke apne waale top view se compare kro"). Midday, no trees, no fog, no post-processing,
    /// 0.5 m a pixel, in 512 m tiles: Shots/topview/tile_{ix}_{iz}.png plus topview.txt with the frame (local metres:
    /// x0 z0 x1 z1, metres per pixel, tile size). Batch: "call TopView.Run".
    /// </summary>
    public static class TopView
    {
        public const float X0 = -700f, Z0 = -1450f, X1 = 1100f, Z1 = 750f, Mpp = 0.5f, Tile = 512f;

        public static void Run()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != WorldBuilder.ScenePath)
                EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            string dir = Path.GetFullPath("Shots/topview");
            Directory.CreateDirectory(dir);

            var dn = Object.FindAnyObjectByType<DayNightCycle>();
            float hourWas = dn ? dn.timeOfDay : 12f;
            if (dn) { dn.timeOfDay = 12.3f; dn.Apply(); }
            bool fogWas = RenderSettings.fog;
            RenderSettings.fog = false;
            Shader.SetGlobalFloat("_HazeDensity", 0f);
            var trees = Object.FindObjectsByType<TreeRenderer>(FindObjectsSortMode.None);
            foreach (var t in trees) t.enabled = false;
            var grass = Object.FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None);
            foreach (var g in grass) g.enabled = false;

            var go = new GameObject("TopViewCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Tile * 0.5f;
            cam.aspect = 1f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 3000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            int px = Mathf.RoundToInt(Tile / Mpp);
            var rt = new RenderTexture(px, px, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // looking down, north (+z) up in the image

            int nx = Mathf.CeilToInt((X1 - X0) / Tile), nz = Mathf.CeilToInt((Z1 - Z0) / Tile);
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float cx = X0 + (ix + 0.5f) * Tile, cz = Z0 + (iz + 0.5f) * Tile;
                    go.transform.position = new Vector3(cx, 2400f, cz);
                    cam.Render();
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(px, px, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, px, px), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, $"tile_{ix}_{iz}.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
            var inv = CultureInfo.InvariantCulture;
            File.WriteAllText(Path.Combine(dir, "topview.txt"),
                string.Join(" ", new[] { X0, Z0, X1, Z1, Mpp, Tile }.Select(v => v.ToString(inv))) + $" {nx} {nz}\n");

            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            rt.Release();
            foreach (var t in trees) t.enabled = true;
            foreach (var g in grass) g.enabled = true;
            RenderSettings.fog = fogWas;
            if (dn) { dn.timeOfDay = hourWas; dn.Apply(); }
            Debug.Log($"[Nainital] top view: {nx} x {nz} tiles of {px} px -> {dir}");
        }
    }
}

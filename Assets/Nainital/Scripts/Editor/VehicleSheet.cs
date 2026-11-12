using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Bridge: every vehicle in the library rendered from the front-left and rear-right, in plain daylight, on
    /// one sheet (Shots/vehicle_sheet.png): a quick check that every model has its textures and materials.</summary>
    public static class VehicleSheet
    {
        public static void Run()
        {
            var lib = Resources.Load<VehicleLibrary>("VehicleLibrary");
            if (!lib) throw new System.Exception("no VehicleLibrary in Resources");
            int n = lib.entries.Length, tw = 480, th = 300;
            var sheet = new Texture2D(tw * 2, th * n, TextureFormat.RGB24, false);
            var root = new GameObject("VehicleSheet (temp)") { hideFlags = HideFlags.DontSave };
            root.transform.position = new Vector3(0, 6000, 0);
            var lg = new GameObject("Sun") { hideFlags = HideFlags.DontSave };
            lg.transform.SetParent(root.transform, false);
            lg.transform.rotation = Quaternion.Euler(40, 30, 0);
            var sun = lg.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            var cg = new GameObject("Cam") { hideFlags = HideFlags.DontSave };
            cg.transform.SetParent(root.transform, false);
            var cam = cg.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            cam.fieldOfView = 30f;
            var rt = new RenderTexture(tw, th, 24);
            cam.targetTexture = rt;
            var amb = RenderSettings.ambientLight;
            var ambMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            var sb = new System.Text.StringBuilder("[Nainital] vehicle sheet:");
            for (int i = 0; i < n; i++)
            {
                var e = lib.entries[i];
                var go = VehicleFactory.CreateModel(e, e.type, Px.CarWhite, false, "Sheet " + e.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero;
                int missing = 0, mats = 0;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.sharedMaterials)
                    {
                        mats++;
                        if (!m || (m.HasProperty("_BaseMap") && !m.GetTexture("_BaseMap") && m.name.ToLower().Contains("body"))) missing++;
                    }
                sb.Append($"\n  {e.name}: {mats} material slots, {missing} without a texture or material");
                float L = Mathf.Max(e.size.z, 3.5f);
                for (int v = 0; v < 2; v++)
                {
                    Vector3 dir = v == 0 ? new Vector3(-0.8f, 0.35f, 1f) : new Vector3(0.9f, 0.4f, -1f);
                    cg.transform.position = root.transform.position + Vector3.up * 0.8f + dir.normalized * L * 2.3f;
                    cg.transform.LookAt(root.transform.position + Vector3.up * 0.8f);
                    cam.Render();
                    RenderTexture.active = rt;
                    var tile = new Texture2D(tw, th, TextureFormat.RGB24, false);
                    tile.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                    tile.Apply();
                    RenderTexture.active = null;
                    sheet.SetPixels(v * tw, (n - 1 - i) * th, tw, th, tile.GetPixels());
                    Object.DestroyImmediate(tile);
                }
                Object.DestroyImmediate(go);
            }
            RenderSettings.ambientLight = amb;
            RenderSettings.ambientMode = ambMode;
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(root);
            sheet.Apply();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/vehicle_sheet.png"));
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log(sb + "\n  -> " + path);
        }
    }
}

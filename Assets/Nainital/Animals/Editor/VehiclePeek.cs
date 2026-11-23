using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Close-up batch photo of a few new-set vehicle prefabs (24 Nov 2026: SheetShot showed every vehicle but the new fire
    /// truck in a camouflage of wrong texels - is it the prefabs or the far sheet camera?). "call VehiclePeek.Run" ->
    /// Shots/vehicle_peek.png: car_van, bus_roadways, truck_light, truck_fire from the front 3/4, 6 m away.
    /// </summary>
    public static class VehiclePeek
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string[] names = { "car_van", "bus_roadways", "truck_light", "truck_fire" };
            var sun = new GameObject("sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f; sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.75f, 0.78f, 0.82f);
            const int T = 480;
            var rt = new RenderTexture(T, T, 24); cam.targetTexture = rt;
            var sheet = new Texture2D(T * names.Length, T, TextureFormat.RGB24, false);
            for (int i = 0; i < names.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Nainital/Vehicles/New/Prefabs/" + names[i] + ".prefab");
                if (!prefab) { Debug.Log("[Nainital] VehiclePeek: no prefab " + names[i]); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var rs = go.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float d = Mathf.Max(b.size.x, b.size.y, b.size.z) * 1.3f;
                cam.transform.position = b.center + new Vector3(0.7f, 0.35f, 0.7f).normalized * d;
                cam.transform.LookAt(b.center);
                cam.fieldOfView = 45f;
                cam.Render();
                RenderTexture.active = rt;
                var tile = new Texture2D(T, T, TextureFormat.RGB24, false);
                tile.ReadPixels(new Rect(0, 0, T, T), 0, 0); tile.Apply();
                sheet.SetPixels(i * T, 0, T, T, tile.GetPixels());
                RenderTexture.active = null;
                Object.DestroyImmediate(go);
            }
            sheet.Apply();
            System.IO.Directory.CreateDirectory("Shots");
            System.IO.File.WriteAllBytes("Shots/vehicle_peek.png", sheet.EncodeToPNG());
        }
    }
}

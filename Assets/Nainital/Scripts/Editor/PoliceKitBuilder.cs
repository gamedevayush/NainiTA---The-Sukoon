using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Builds Resources/PoliceKit (2026-11-18, police and ambulance system): the light bar lamp materials (unlit HDR red,
    /// blue, white), the pistol's muzzle-flash material, and the solid 3D lettering for the vehicle sides, placed by
    /// measuring the models: "UTTARAKHAND POLICE" in blue (with a red line) on the police Bolero, "108 AMBULANCE" in red
    /// on the ambulance; and where each roof's top is (the light bar sits there). Run after Nainital > 7. Import vehicles.
    /// Bridge: call PoliceKitBuilder.Build
    /// </summary>
    public static class PoliceKitBuilder
    {
        const string KitPath = "Assets/Resources/PoliceKit.asset";
        const string Dir = "Assets/Nainital/Vehicles/Police";

        struct Side { public float x, y, z, len; }

        [MenuItem("Nainital/7b. Police and ambulance kit", priority = 8)]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Dir);
            var kit = AssetDatabase.LoadAssetAtPath<PoliceKit>(KitPath);
            if (!kit) { kit = ScriptableObject.CreateInstance<PoliceKit>(); AssetDatabase.CreateAsset(kit, KitPath); }
            kit.lampRed = Lamp("M_LightBarRed", new Color(7f, 0.35f, 0.25f));
            kit.lampBlue = Lamp("M_LightBarBlue", new Color(0.35f, 0.75f, 8f));
            kit.lampWhite = Lamp("M_Tracer", new Color(5f, 4.4f, 3.2f));
            kit.muzzle = Muzzle();
            // the unbranded set (21 Nov 2026): the police drive its boxy SUV and ride its 125 cc commuter once it is in the
            // library; the 108 ambulance is its white panel van (owner, 21 Nov evening: "baaki solve krdo" - the branded
            // Ambulance108 was the last old model in the game), lettered and lit by this kit
            var vlib = VehicleLibrary.Instance;
            if (vlib && System.Array.Exists(vlib.entries, e => e != null && e.prefab && e.name == "car_suv_boxy")) kit.policeCarModel = "car_suv_boxy";
            if (vlib && vlib.Bike("bike_commuter125") != null) kit.bikeModel = "bike_commuter125";
            if (vlib && System.Array.Exists(vlib.entries, e => e != null && e.prefab && e.name == "car_van")) kit.ambulanceModel = "car_van";

            string log = "";
            if (Measure(kit.policeCarModel, 0.44f, 0.58f, out var roof, out float roofW, out var side, false))
            {
                kit.policeRoof = roof;
                kit.policeRoofWidth = roofW;
                kit.policeLetters = Save(PoliceLetters(side), "PoliceLetters");
                log += $" police roof {roof} w {roofW:F2}, side x {side.x:F2} y {side.y:F2} z {side.z:F2} len {side.len:F2};";
            }
            if (Measure(kit.ambulanceModel, 0.45f, 0.62f, out roof, out roofW, out side, true))
            {
                kit.ambulanceRoof = roof;
                kit.ambulanceRoofWidth = roofW;
                kit.ambulanceLetters = Save(AmbulanceLetters(side), "AmbulanceLetters");
                log += $" ambulance roof {roof} w {roofW:F2}, side x {side.x:F2} y {side.y:F2} z {side.z:F2} len {side.len:F2};";
            }
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log("[Nainital] police kit -> " + KitPath + ":" + log);
        }

        static Material Lamp(string id, Color hdr)
        {
            string path = $"{Dir}/{id}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = id }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", hdr);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Muzzle()
        {
            string path = $"{Dir}/M_MuzzleFlash.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_MuzzleFlash" }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/VehicleFX/T_Puff.png"));
            mat.SetColor("_BaseColor", new Color(3.2f, 2.3f, 1.1f, 1f));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Mesh Save(Mesh m, string name)
        {
            string path = $"{Dir}/{name}.asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// Reads the model's full-detail mesh (prefab space: front +Z, pivot at the bottom centre). Roof: the highest
        /// vertices near the middle line (the bar sits on them, towards the front for the ambulance's cab top). Side: in
        /// the height band yLo..yHi (fractions of the height) the outermost x along the body; the flat stretch of it
        /// is where the letters go.
        /// </summary>
        static bool Measure(string model, float yLo, float yHi, out Vector3 roof, out float roofW, out Side side, bool barAtFront)
        {
            roof = Vector3.zero;
            roofW = 1f;
            side = default;
            string path = $"{VehicleKit.Dir}/Prefabs/{model}.prefab";
            var entry = VehicleLibrary.Instance ? System.Array.Find(VehicleLibrary.Instance.entries, e => e != null && e.prefab && e.name == model) : null;
            if (entry != null) path = AssetDatabase.GetAssetPath(entry.prefab);   // the unbranded set: Vehicles/New/Prefabs
            if (!File.Exists(path)) { Debug.LogWarning("[Nainital] police kit: no prefab " + path); return false; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MeshFilter mf = null;
                foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) if ((f.name.EndsWith("L0") || f.name == "Body") && f.sharedMesh) mf = f;
                if (!mf) { Debug.LogWarning("[Nainital] police kit: no L0 mesh in " + model); return false; }
                var vs = mf.sharedMesh.vertices;
                if (vs == null || vs.Length == 0) { Debug.LogWarning("[Nainital] police kit: unreadable mesh " + model); return false; }
                var m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var pts = new Vector3[vs.Length];
                Vector3 lo = Vector3.one * 1e9f, hi = -lo;
                for (int i = 0; i < vs.Length; i++)
                {
                    pts[i] = m.MultiplyPoint3x4(vs[i]);
                    lo = Vector3.Min(lo, pts[i]);
                    hi = Vector3.Max(hi, pts[i]);
                }
                float W = hi.x - lo.x, H = hi.y - lo.y, L = hi.z - lo.z;

                // roof: the top near the middle line
                float top = float.MinValue;
                foreach (var p in pts) if (Mathf.Abs(p.x) < W * 0.22f) top = Mathf.Max(top, p.y);
                float zs = 0f, zmax = float.MinValue, xw = 0f;
                int n = 0;
                foreach (var p in pts)
                    if (p.y > top - 0.07f)
                    {
                        zs += p.z; n++;
                        if (Mathf.Abs(p.x) < W * 0.22f) zmax = Mathf.Max(zmax, p.z);
                        xw = Mathf.Max(xw, Mathf.Abs(p.x));
                    }
                float zAvg = n > 0 ? zs / n : 0f;
                roof = new Vector3(0f, top - 0.01f, barAtFront ? zmax - 0.35f : zAvg + 0.2f);
                roofW = Mathf.Clamp(xw * 2f, 0.8f, W);

                // side: the outermost x in the band, sampled along the body
                float y0 = lo.y + H * yLo, y1 = lo.y + H * yHi;
                var xs = new List<(float z, float x)>();
                for (float z = lo.z + L * 0.12f; z <= hi.z - L * 0.12f; z += L * 0.04f)
                {
                    float best = float.MinValue;
                    foreach (var p in pts)
                        if (p.y >= y0 && p.y <= y1 && Mathf.Abs(p.z - z) < L * 0.025f) best = Mathf.Max(best, Mathf.Abs(p.x));
                    if (best > float.MinValue) xs.Add((z, best));
                }
                if (xs.Count < 3) { Debug.LogWarning("[Nainital] police kit: no side found on " + model); return false; }
                var sorted = new List<float>();
                foreach (var t in xs) sorted.Add(t.x);
                sorted.Sort();
                float med = sorted[sorted.Count / 2];
                // the longest run of samples within 3 cm of the median: the flat stretch
                int bs = 0, be = -1, s = -1;
                for (int i = 0; i <= xs.Count; i++)
                {
                    bool ok = i < xs.Count && Mathf.Abs(xs[i].x - med) < 0.03f;
                    if (ok && s < 0) s = i;
                    if (!ok && s >= 0) { if (i - 1 - s > be - bs) { bs = s; be = i - 1; } s = -1; }
                }
                if (be < bs) { bs = 0; be = xs.Count - 1; }
                float sx = 0f;
                for (int i = bs; i <= be; i++) sx = Mathf.Max(sx, xs[i].x);
                side = new Side { x = sx, y = (y0 + y1) * 0.5f, z = (xs[bs].z + xs[be].z) * 0.5f, len = Mathf.Max(0.6f, xs[be].z - xs[bs].z + L * 0.04f) };
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Mesh PoliceLetters(Side s)
        {
            var mb = new MeshBuilder();
            Vector2 blue = Atlas.ExtraUV(Px.CarBlue), navy = Atlas.ExtraUV(Px.BusBlue), red = Atlas.ExtraUV(Px.FlagRed);
            float len = Mathf.Min(s.len * 0.92f, 1.9f);
            foreach (float sx in new[] { 1f, -1f })
            {
                Vector3 o = new Vector3(sx, 0f, 0f);
                float x = sx * (s.x + 0.003f);
                Glyph3D.Write(mb, "POLICE", Glyph3D.Face.Block, new Vector3(x, s.y - 0.02f, s.z), o, 0.17f, 0.008f, len, blue, navy);
                Glyph3D.Write(mb, "UTTARAKHAND", Glyph3D.Face.Tall, new Vector3(x, s.y + 0.135f, s.z), o, 0.075f, 0.006f, len, blue, navy);
                mb.Box(new Vector3(sx * s.x, s.y - 0.145f, s.z), new Vector3(0.012f, 0.032f, len), red);   // a red line under it
                mb.Box(new Vector3(sx * s.x, s.y - 0.18f, s.z), new Vector3(0.012f, 0.02f, len), blue);
            }
            return mb.ToMesh("PoliceLetters");
        }

        static Mesh AmbulanceLetters(Side s)
        {
            var mb = new MeshBuilder();
            Vector2 red = Atlas.ExtraUV(Px.FlagRed), dark = Atlas.ExtraUV(Px.TailLight);
            float len = Mathf.Min(s.len * 0.9f, 2.4f);
            foreach (float sx in new[] { 1f, -1f })
            {
                Vector3 o = new Vector3(sx, 0f, 0f);
                float x = sx * (s.x + 0.003f);
                Glyph3D.Write(mb, "108", Glyph3D.Face.Block, new Vector3(x, s.y + 0.08f, s.z), o, 0.3f, 0.01f, len, red, dark);
                Glyph3D.Write(mb, "AMBULANCE", Glyph3D.Face.Tall, new Vector3(x, s.y - 0.18f, s.z), o, 0.13f, 0.008f, len, red, dark);
            }
            return mb.ToMesh("AmbulanceLetters");
        }
    }
}

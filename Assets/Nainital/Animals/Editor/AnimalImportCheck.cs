using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Batch-safe import check of the rigged street animals and the story characters (24 Nov 2026), no Play needed:
    /// "call AnimalImportCheck.Run" in Bridge/batch.txt. In a fresh scene it puts cow / dog / cat / monkey in a row the way
    /// AnimalManager does (modelYaw 180: then the animal must face +Z), samples each one's walk clip half way,
    /// and f_ladki / m_player beside them; renders a tile per object from +Z (top row: every face must look at the camera) and +X (bottom row)
    /// to Shots/animal_import.png, and writes Logs/animalcheck.txt: clips (name, length) per animal, its bounds, the
    /// characters' Humanoid avatar (valid / human).
    /// </summary>
    public static class AnimalImportCheck
    {
        static readonly string[] Kinds = { "cow", "dog", "cat", "monkey" };
        static readonly string[] People = { "f_ladki", "m_player", "f_mummy", "m_papa", "m_uncle", "m_dost", "f_dost_gf", "m_doctor", "m_landlord", "m_hotel", "m_rental", "m_rto" };

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sb = new StringBuilder();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            float x = -4.5f;
            foreach (var k in Kinds)
            {
                var prefab = Resources.Load<GameObject>("Animals/" + k);
                if (!prefab) { sb.AppendLine(k + ": MISSING"); continue; }
                var go = Object.Instantiate(prefab);
                go.name = k;
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // AnimalManager.modelYaw
                var tex = Resources.Load<Texture2D>("Animals/" + k + "_color");
                var m = new Material(lit);
                if (tex) m.SetTexture("_BaseMap", tex);
                m.SetFloat("_Smoothness", 0.12f);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
                var anim = go.GetComponentInChildren<Animation>();
                sb.Append(k).Append(": clips");
                AnimationClip walk = null;
                if (anim)
                    foreach (AnimationState st in anim)
                    {
                        sb.Append(' ').Append(st.name).Append(' ').Append(st.clip.length.ToString("F2")).Append('s');
                        if (st.name.EndsWith("walk")) walk = st.clip;
                    }
                else sb.Append(" NONE (no Animation component)");
                if (walk) walk.SampleAnimation(go, walk.length * 0.5f);
                var b = Bounds(go);
                float w = b.size.x;
                go.transform.position = new Vector3(x + w * 0.5f, 0f, 0f) - new Vector3(b.center.x, 0f, b.center.z);
                x += w + 0.5f;
                b = Bounds(go);
                sb.Append($"  size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} (x,y,z)  minY {b.min.y:F2}\n");
            }
            x += 0.4f;
            foreach (var p in People)
            {
                string path = "Assets/Nainital/Characters/Models/" + p + ".fbx";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) { sb.AppendLine(p + ": MISSING " + path); continue; }
                var go = Object.Instantiate(prefab);
                go.name = p;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nainital/Characters/Textures/" + p + ".jpg");
                var m = new Material(lit);
                if (tex) m.SetTexture("_BaseMap", tex);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.name.Contains("LOD1")) r.enabled = false;
                var animator = go.GetComponentInChildren<Animator>();
                var av = animator ? animator.avatar : null;
                go.transform.position = new Vector3(x + 0.35f, 0f, 0f);
                x += 1.1f;
                var b = Bounds(go);
                sb.Append($"{p}: avatar {(av ? av.name : "none")} valid {(av && av.isValid)} human {(av && av.isHuman)}  height {b.size.y:F2}\n");
            }
            var sun = new GameObject("sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.transform.rotation = Quaternion.Euler(40f, 200f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            // one tile per object: from +Z (its face must look at us) and from +X (its left side), framed tight
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.8f, 0.2f, 0.8f);
            cam.orthographic = true;
            const int T = 360;
            var objs = new System.Collections.Generic.List<GameObject>();
            foreach (var n in Kinds) { var g = GameObject.Find(n); if (g) objs.Add(g); }
            foreach (var n in People) { var g = GameObject.Find(n); if (g) objs.Add(g); }
            var sheet = new Texture2D(T * objs.Count, T * 2, TextureFormat.RGB24, false);
            var rt = new RenderTexture(T, T, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < objs.Count; i++)
            {
                foreach (var o in objs) o.SetActive(o == objs[i]);
                var b = Bounds(objs[i]);
                cam.orthographicSize = Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.6f;
                for (int v = 0; v < 2; v++)
                {
                    Vector3 dir = v == 0 ? Vector3.forward : Vector3.right;
                    cam.transform.position = b.center + dir * 10f;
                    cam.transform.LookAt(b.center);
                    cam.Render();
                    RenderTexture.active = rt;
                    var tile = new Texture2D(T, T, TextureFormat.RGB24, false);
                    tile.ReadPixels(new Rect(0, 0, T, T), 0, 0);
                    tile.Apply();
                    sheet.SetPixels(i * T, (1 - v) * T, T, T, tile.GetPixels());
                    RenderTexture.active = null;
                }
            }
            foreach (var o in objs) o.SetActive(true);
            sheet.Apply();
            System.IO.Directory.CreateDirectory("Shots");
            System.IO.File.WriteAllBytes("Shots/animal_import.png", sheet.EncodeToPNG());
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/animalcheck.txt", sb.ToString());
            Debug.Log("[Nainital] AnimalImportCheck\n" + sb);
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}

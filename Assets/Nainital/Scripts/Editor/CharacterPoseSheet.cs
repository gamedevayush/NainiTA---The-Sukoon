using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Every rigged character of the CharacterLibrary posed by one animation clip through its Humanoid avatar (the way
    /// the game plays them), side on and front on, in one contact sheet per clip: shows avatar problems (knees bending
    /// the wrong way, feet turned, legs crossing) without playing the game. Bridge / batch: call CharacterPoseSheet.Run
    /// (clips: Bridge/arg.txt, "path.fbx@0.3;path2.fbx@0.6"; default idle, walk and run of the locomotion animator).
    /// Writes Shots/pose_&lt;clip&gt;_&lt;time&gt;.png: per character a side view (left) and a front view (right).
    /// </summary>
    public static class CharacterPoseSheet
    {
        const int CellW = 180, CellH = 300, PerRow = 8;

        /// <summary>One clip through its cycle on a few characters, side on: arg "path.fbx|id1,id2,id3" ->
        /// Shots/cycle_&lt;clip&gt;.png, a row of 8 phases per character (game code not involved: clip + avatar only).</summary>
        public static void Cycle()
        {
            string arg = File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim();
            var parts = arg.Split('|');
            var clip = AssetDatabase.LoadAllAssetsAtPath(parts[0]).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (!clip) throw new System.Exception("no clip " + parts[0]);
            var lib = CharacterLibrary.Instance;
            var ids = parts[1].Split(',');
            const int CW = 220, CH = 320, PH = 8;
            var sheet = new Texture2D(CW * PH, CH * ids.Length, TextureFormat.RGB24, false);
            var root = new GameObject("CycleSheet");
            Vector3 stage = new Vector3(0f, 4000f, 0f);
            var lg = new GameObject("Key"); lg.transform.SetParent(root.transform, false); lg.transform.rotation = Quaternion.Euler(30f, 40f, 0f);
            var key = lg.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 2.4f;
            var camGo = new GameObject("Cam"); camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 1.0f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.6f, 0.66f);
            var rt = new RenderTexture(CW, CH, 24) { antiAliasing = 4 }; cam.targetTexture = rt; cam.aspect = CW / (float)CH;
            var cell = new Texture2D(CW, CH, TextureFormat.RGB24, false);
            try
            {
                for (int r = 0; r < ids.Length; r++)
                {
                    var e = lib.Find(ids[r].Trim());
                    if (e == null) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(e.prefab);
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetPositionAndRotation(stage, Quaternion.identity);
                    foreach (var l in go.GetComponentsInChildren<LODGroup>(true)) l.ForceLOD(0);
                    var anim = go.GetComponentInChildren<Animator>(true);
                    anim.applyRootMotion = false;
                    anim.Rebind();
                    for (int k = 0; k < PH; k++)
                    {
                        AnimationMode.StartAnimationMode();
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(anim.gameObject, clip, clip.length * k / PH);
                        AnimationMode.EndSampling();
                        camGo.transform.SetPositionAndRotation(stage + Vector3.up * 0.95f + Vector3.right * 4f, Quaternion.LookRotation(Vector3.left));
                        cam.Render();
                        RenderTexture.active = rt;
                        cell.ReadPixels(new Rect(0, 0, CW, CH), 0, 0);
                        cell.Apply();
                        RenderTexture.active = null;
                        sheet.SetPixels(k * CW, (ids.Length - 1 - r) * CH, CW, CH, cell.GetPixels());
                        AnimationMode.StopAnimationMode();
                    }
                    Object.DestroyImmediate(go);
                }
                sheet.Apply();
                string outp = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/cycle_" + clip.name + ".png"));
                File.WriteAllBytes(outp, sheet.EncodeToPNG());
                Debug.Log("[Nainital] cycle sheet " + clip.name + " (" + clip.length.ToString("F2") + " s): " + parts[1] + " -> " + outp);
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(cell); Object.DestroyImmediate(sheet); Object.DestroyImmediate(root);
            }
        }

        public static void Run()
        {
            string arg = "";
            try { arg = File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { }
            var jobs = new List<(string fbx, float t)>();
            if (arg.Contains(".fbx"))
                foreach (var part in arg.Split(';'))
                {
                    var bits = part.Split('@');
                    jobs.Add((bits[0].Trim(), bits.Length > 1 && float.TryParse(bits[1], out var tt) ? tt : 0.3f));
                }
            else
            {
                jobs.Add(("Assets/GOAN/Animations/NewAnims/Idle (3).fbx", 0.3f));
                jobs.Add(("Assets/GOAN/Animations/NewAnims/Walking.fbx", 0.25f));
                jobs.Add(("Assets/GOAN/Animations/NewAnims/Fast Run.fbx", 0.3f));
            }
            var lib = CharacterLibrary.Instance;
            if (!lib) throw new System.Exception("no CharacterLibrary");
            var entries = lib.entries.Where(e => e != null && e.prefab).ToArray();
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots"));
            Directory.CreateDirectory(outDir);

            var root = new GameObject("PoseSheet");
            Vector3 stage = new Vector3(0f, 4000f, 0f);
            var lightGo = new GameObject("Key");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(30f, 40f, 0f);
            var key = lightGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 2.4f;
            var camGo = new GameObject("SheetCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.6f, 0.66f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 8f;
            var rt = new RenderTexture(CellW, CellH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.aspect = CellW / (float)CellH;
            var cell = new Texture2D(CellW, CellH, TextureFormat.RGB24, false);
            try
            {
                foreach (var (fbx, t) in jobs)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (!clip) { Debug.LogWarning("[Nainital] pose sheet: no clip in " + fbx); continue; }
                    int rows = (entries.Length + PerRow - 1) / PerRow;
                    var sheet = new Texture2D(PerRow * CellW * 2, rows * CellH, TextureFormat.RGB24, false);
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var e = entries[i];
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(e.prefab);
                        go.transform.SetParent(root.transform, false);
                        go.transform.SetPositionAndRotation(stage, Quaternion.identity);   // faces +Z
                        foreach (var lg in go.GetComponentsInChildren<LODGroup>(true)) lg.ForceLOD(0);
                        var anim = go.GetComponentInChildren<Animator>(true);
                        PlayableGraph g = default;
                        if (anim && anim.avatar)
                        {
                            anim.applyRootMotion = false;
                            anim.Rebind();
                            AnimationMode.StartAnimationMode();
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(anim.gameObject, clip, t * clip.length);
                            AnimationMode.EndSampling();
                        }
                        else Debug.LogWarning($"[Nainital] pose sheet: {e.id} has no avatar");
                        int row = i / PerRow, col = i % PerRow;
                        for (int v = 0; v < 2; v++)
                        {
                            Vector3 c = stage + Vector3.up * 0.95f;
                            if (v == 0) camGo.transform.SetPositionAndRotation(c + Vector3.right * 4f, Quaternion.LookRotation(Vector3.left));
                            else camGo.transform.SetPositionAndRotation(c + Vector3.forward * 4f, Quaternion.LookRotation(Vector3.back));
                            cam.Render();
                            RenderTexture.active = rt;
                            cell.ReadPixels(new Rect(0, 0, CellW, CellH), 0, 0);
                            cell.Apply();
                            RenderTexture.active = null;
                            sheet.SetPixels((col * 2 + v) * CellW, (rows - 1 - row) * CellH, CellW, CellH, cell.GetPixels());
                        }
                        if (g.IsValid()) g.Destroy();
                        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                        Object.DestroyImmediate(go);
                    }
                    sheet.Apply();
                    string stem = Path.GetFileNameWithoutExtension(fbx).Replace(" ", "").Replace("(", "").Replace(")", "") + "_" + Mathf.RoundToInt(t * 100);
                    File.WriteAllBytes(Path.Combine(outDir, $"pose_{stem}.png"), sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);
                    Debug.Log($"[Nainital] pose sheet {clip.name} at {t:F2}: {entries.Length} characters -> Shots/pose_{stem}.png");
                }
                Debug.Log("[Nainital] pose sheet order: " + string.Join(", ", entries.Select((e, i) => $"{i}:{e.id}")));
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(cell);
                Object.DestroyImmediate(root);
            }
        }
    }
}

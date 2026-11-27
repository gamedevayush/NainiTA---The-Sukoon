using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Pictures of a story timeline as the owner sees it in the editor (27 Nov 2026: "tum kya check krte ho"): the scene's
    /// timeline evaluated at given times, the frame rendered from its Story Camera. Bridge: call StoryTimelineShots.Render,
    /// arg.txt "c0_0_trip 3 12.5 40" (times in seconds) or "c0_0_trip every 4". Writes Shots/tlx_[id]_[nn].png and logs
    /// each time. Refuses when Story_C0 has unsaved changes (it reloads the scene from disk afterwards, to throw away
    /// what the evaluation moved).
    /// </summary>
    public static class StoryTimelineShots
    {
        const string StoryScene = "Assets/Nainital/Scenes/Story_C0.unity";

        public static void Render()
        {
            string arg;
            try { arg = File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { arg = ""; }
            // "@Object name|x,y,z|x,y,z" at the end: a camera of its own, placed in that object's frame (a look inside a car)
            string peek = null;
            int at = arg.IndexOf('@');
            if (at >= 0) { peek = arg.Substring(at + 1); arg = arg.Substring(0, at).Trim(); }
            var a = arg.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (a.Length < 2) { Debug.LogWarning("[Nainital] timeline shots: arg 'id t1 t2 ...' or 'id every N'"); return; }
            string id = a[0];
            var story = SceneManager.GetSceneByPath(StoryScene);
            if (!story.isLoaded) story = EditorSceneManager.OpenScene(StoryScene, OpenSceneMode.Additive);
            if (story.isDirty) { Debug.LogWarning("[Nainital] timeline shots: Story_C0 has unsaved changes - save it first"); return; }
            GameObject root = null;
            foreach (var g in story.GetRootGameObjects()) if (g.name == id) root = g;
            var pd = root ? root.GetComponent<PlayableDirector>() : null;
            var sd = root ? root.GetComponent<StoryDirector>() : null;
            if (!pd || !sd || !sd.storyCamera) { Debug.LogWarning("[Nainital] timeline shots: no scene " + id); return; }
            var times = new List<double>();
            if (a[1] == "every" && a.Length > 2 && double.TryParse(a[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double step))
                for (double t = 0.5; t < pd.duration; t += step) times.Add(t);
            else
                for (int i = 1; i < a.Length; i++) if (double.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double t)) times.Add(t);
            var dn = Object.FindAnyObjectByType<DayNightCycle>();
            if (dn && sd.hour >= 0f) { dn.timeOfDay = sd.hour; dn.Apply(); }
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots"));
            Directory.CreateDirectory(dir);
            var cam = sd.storyCamera;
            Transform peekAt = null; Vector3 peekPos = Vector3.zero, peekLook = Vector3.forward;
            if (peek != null)
            {
                var pp = peek.Split('|');
                foreach (var t in sd.set.GetComponentsInChildren<Transform>(true)) if (t.name == pp[0]) { peekAt = t; break; }
                System.Func<string, Vector3> V = q => { var n = q.Split(','); return new Vector3(float.Parse(n[0], CultureInfo.InvariantCulture), float.Parse(n[1], CultureInfo.InvariantCulture), float.Parse(n[2], CultureInfo.InvariantCulture)); };
                if (pp.Length >= 3) { peekPos = V(pp[1]); peekLook = V(pp[2]); }
                if (!peekAt) Debug.LogWarning("[Nainital] timeline shots: no " + pp[0] + " in the set");
                var pc = new GameObject("Peek camera");
                SceneManager.MoveGameObjectToScene(pc, story);
                cam = pc.AddComponent<Camera>();
                cam.fieldOfView = 60f; cam.nearClipPlane = 0.03f; cam.farClipPlane = 3000f;
            }
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var before = cam.targetTexture;
            // as the Timeline window previews: in animation mode (the characters' humanoid clips apply, and everything the
            // evaluation moves is put back when it stops)
            AnimationMode.StartAnimationMode();
            try
            {
                pd.RebuildGraph();
                cam.targetTexture = rt;
                int n = 0;
                foreach (var t in times)
                {
                    pd.time = t;
                    AnimationMode.BeginSampling();
                    pd.Evaluate();
                    AnimationMode.EndSampling();
                    if (peekAt) cam.transform.SetPositionAndRotation(peekAt.TransformPoint(peekPos), Quaternion.LookRotation(peekAt.TransformPoint(peekLook) - peekAt.TransformPoint(peekPos), peekAt.up));
                    cam.Render();
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    string name = $"tlx_{id}_{n:00}";
                    File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    Debug.Log($"[Nainital] timeline shot {name} at {t:F1} s (of {pd.duration:F1})");
                    n++;
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                cam.targetTexture = before;
                rt.Release();
                Object.DestroyImmediate(rt);
                // what the evaluation moved and switched on goes: the scene again as saved
                EditorSceneManager.CloseScene(story, true);
                EditorSceneManager.OpenScene(StoryScene, OpenSceneMode.Additive);
            }
        }
    }
}

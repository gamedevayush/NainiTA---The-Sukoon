using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Nainital > Bug capture karo (Ctrl+Shift+B). In play mode it opens the game's own bug box (BugReporter: the Game
    /// view as the player sees it). Outside play mode this window grabs the Scene view, asks what is wrong, takes any
    /// extra photos, and writes the same kind of folder in BugReports (screen.png, note.txt with the camera's spot).
    /// </summary>
    public class BugCapture : EditorWindow
    {
        string text = "";
        Texture2D shot;
        string where = "";
        readonly System.Collections.Generic.List<string> extras = new System.Collections.Generic.List<string>();
        Vector2 scroll;

        [MenuItem("Nainital/Bug capture karo %#b", priority = 0)]
        static void Open()
        {
            if (EditorApplication.isPlaying) { BugReporter.Open(); return; }
            var w = GetWindow<BugCapture>(true, "Bug batao", true);
            w.minSize = new Vector2(760, 640);
            w.Grab();
        }

        void Grab()
        {
            var sv = SceneView.lastActiveSceneView;
            extras.Clear();
            text = "";
            if (!sv || !sv.camera) { where = "no Scene view open"; return; }
            var cam = sv.camera;
            int w = Mathf.Max(64, (int)sv.position.width), h = Mathf.Max(64, (int)sv.position.height);
            var rt = new RenderTexture(w, h, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = old;
            RenderTexture.active = rt;
            if (shot) DestroyImmediate(shot);
            shot = new Texture2D(w, h, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            shot.Apply();
            RenderTexture.active = null;
            rt.Release();
            DestroyImmediate(rt);
            Vector3 c = cam.transform.position, f = cam.transform.forward, l = c + f * 20f;
            where = "time: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" +
                    "from: Scene view (editor, not playing)\n" +
                    $"camera: {c.x:F1},{c.y:F1},{c.z:F1} looking {f.x:F2},{f.y:F2},{f.z:F2}\n" +
                    $"shot spec: bug;{c.x:F1},{c.y:F1},{c.z:F1};{l.x:F1},{l.y:F1},{l.z:F1};12\n" +
                    "selected: " + (Selection.activeGameObject ? Selection.activeGameObject.name : "-") + "\n";
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (shot) GUILayout.Label(shot, GUILayout.Height(300), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Scene view se photo dobara lo")) Grab();
            EditorGUILayout.LabelField("Kya galat hai? (jagah, kya dikh raha hai, kya hona chahiye)");
            text = EditorGUILayout.TextArea(text, GUILayout.MinHeight(110));
            if (GUILayout.Button("Aur photo jodo"))
            {
                string f = EditorUtility.OpenFilePanel("Photo jodo", "", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(f)) extras.Add(f);
            }
            foreach (var e in extras) EditorGUILayout.LabelField("  + " + Path.GetFileName(e));
            GUILayout.Space(8);
            if (GUILayout.Button("Save karo", GUILayout.Height(36))) Save();
            EditorGUILayout.EndScrollView();
        }

        void Save()
        {
            string dir = Path.Combine(BugReporter.Folder, System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(dir);
            if (shot) File.WriteAllBytes(Path.Combine(dir, "screen.png"), shot.EncodeToPNG());
            for (int i = 0; i < extras.Count; i++)
                try { File.Copy(extras[i], Path.Combine(dir, $"photo_{i + 1}{Path.GetExtension(extras[i])}"), true); } catch { }
            File.WriteAllText(Path.Combine(dir, "note.txt"), "BUG: " + text.Trim() + "\n\n" + where, System.Text.Encoding.UTF8);
            Debug.Log("[Nainital] bug report saved -> " + dir);
            EditorUtility.DisplayDialog("Bug save ho gaya", "Folder: " + dir + "\n\nAb Claude ko bolo: \"bug dekho\"", "OK");
            Close();
        }
    }
}

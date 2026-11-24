using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The owner's own moves of generated objects survive Build World (25 Nov 2026: he turned the Snow View ropeway bench
    /// and moved the Tallital traffic post in the editor, then asked for a rebuild). Select what you moved, then
    /// Nainital > Keep my move (selected): its hierarchy path and local position / rotation / scale go into
    /// Assets/Nainital/Data/hand_moves.json, and Build World puts them back as its last step.
    /// </summary>
    public static class HandMoves
    {
        public const string FilePath = WorldBuilder.Root + "/Data/hand_moves.json";

        [Serializable] public class Move { public string path; public Vector3 pos; public Quaternion rot; public Vector3 scale = Vector3.one; public string note; }
        [Serializable] public class MoveList { public List<Move> moves = new List<Move>(); }

        public static MoveList Load()
        {
            if (!File.Exists(FilePath)) return new MoveList();
            return JsonUtility.FromJson<MoveList>(File.ReadAllText(FilePath)) ?? new MoveList();
        }

        static void Save(MoveList l) => File.WriteAllText(FilePath, JsonUtility.ToJson(l, true));

        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        static Transform Find(Scene s, string path)
        {
            var parts = path.Split('/');
            foreach (var g in s.GetRootGameObjects())
            {
                if (g.name != parts[0]) continue;
                Transform t = g.transform;
                for (int i = 1; i < parts.Length && t; i++)
                {
                    Transform next = null;
                    foreach (Transform c in t) if (c.name == parts[i]) { next = c; break; }
                    t = next;
                }
                if (t) return t;
            }
            return null;
        }

        [MenuItem("Nainital/Keep my move (selected)", priority = 3)]
        static void KeepSelected()
        {
            var l = Load();
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (!go.scene.IsValid()) continue;
                string p = PathOf(go.transform);
                l.moves.RemoveAll(m => m.path == p);
                l.moves.Add(new Move { path = p, pos = go.transform.localPosition, rot = go.transform.localRotation, scale = go.transform.localScale, note = DateTime.Now.ToString("dd MMM yyyy HH:mm") });
                n++;
            }
            Save(l);
            AssetDatabase.ImportAsset(FilePath);
            Debug.Log($"[Nainital] kept {n} move(s) in {FilePath}: Build World puts them back");
        }

        [MenuItem("Nainital/Keep my move (selected)", true)]
        static bool KeepSelectedValid() => Selection.gameObjects.Length > 0;

        /// <summary>Build World's last step.</summary>
        internal static void Apply(Scene s)
        {
            var l = Load();
            int done = 0;
            foreach (var m in l.moves)
            {
                var t = Find(s, m.path);
                if (!t) { Debug.LogWarning("[Nainital] hand move: not found " + m.path); continue; }
                t.localPosition = m.pos;
                t.localRotation = m.rot;
                t.localScale = m.scale;
                done++;
            }
            if (l.moves.Count > 0) Debug.Log($"[Nainital] hand moves: {done} of {l.moves.Count} put back");
        }
    }
}

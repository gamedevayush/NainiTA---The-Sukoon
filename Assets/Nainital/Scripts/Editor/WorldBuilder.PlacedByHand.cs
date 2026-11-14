using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Things placed by hand survive a world rebuild (user, 2026-11-14: the Shaktipeeth gate is placed by hand).
    /// Any prefab dropped under the "Placed by hand" object at the root of the Nainital scene is written to
    /// Data/placed_by_hand.json whenever the scene is saved (its position, rotation and scale, and those of its direct
    /// children, so the gate's chowki can be moved on its own). Build World puts every one of them back, and keeps
    /// houses off the spots. The first build, with no file yet, puts the gate where the map suggests.
    /// </summary>
    public static partial class WorldBuilder
    {
        public const string PlacedRootName = "Placed by hand";
        public const string PlacedJson = Root + "/Data/placed_by_hand.json";

        [Serializable] public class PlacedChild { public string name; public Vector3 pos; public Quaternion rot; public Vector3 scale = Vector3.one; }
        [Serializable] public class PlacedItem
        {
            public string prefab;
            public Vector3 pos;
            public Quaternion rot = Quaternion.identity;
            public Vector3 scale = Vector3.one;
            public List<PlacedChild> children = new List<PlacedChild>();
        }
        [Serializable] public class PlacedList { public List<PlacedItem> items = new List<PlacedItem>(); }

        static PlacedList LoadPlaced()
        {
            if (!File.Exists(PlacedJson)) return null;
            try { return JsonUtility.FromJson<PlacedList>(File.ReadAllText(PlacedJson)); }
            catch (Exception e) { Debug.LogWarning("[Nainital] placed_by_hand.json unreadable: " + e.Message); return null; }
        }

        /// <summary>Where things placed by hand stand, for PlanMallital to keep houses off them.</summary>
        static IEnumerable<Vector2> PlacedSpots(WorldCtx ctx)
        {
            var list = LoadPlaced();
            if (list != null) { foreach (var it in list.items) yield return new Vector2(it.pos.x, it.pos.z); }
            else if (MargGateSite(ctx, out var at, out _)) yield return at;
        }

        static void RestorePlacedByHand(WorldCtx ctx)
        {
            var list = LoadPlaced();
            if (list == null)
            {
                list = new PlacedList();
                if (MargGateSite(ctx, out var at, out var f))
                    list.items.Add(new PlacedItem { prefab = GatePrefabPath, pos = V(at, ctx.H(at)), rot = Quaternion.LookRotation(Flat(f)) });
            }
            if (!File.Exists(GatePrefabPath) && list.items.Exists(i => i.prefab == GatePrefabPath)) MakeGatePrefab();
            var root = new GameObject(PlacedRootName).transform;   // at the scene root, beside the generated world
            int n = 0;
            foreach (var it in list.items)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(it.prefab);
                if (!pf) { Debug.LogWarning("[Nainital] placed by hand: prefab missing " + it.prefab); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(it.pos, it.rot);
                go.transform.localScale = it.scale;
                foreach (var c in it.children)
                {
                    var t = go.transform.Find(c.name);
                    if (!t) continue;
                    t.localPosition = c.pos;
                    t.localRotation = c.rot;
                    t.localScale = c.scale;
                }
                n++;
            }
            Debug.Log($"[Nainital] placed by hand: {n} put back");
        }

        internal static void WritePlaced(Scene s)
        {
            GameObject root = null;
            foreach (var g in s.GetRootGameObjects()) if (g.name == PlacedRootName) { root = g; break; }
            if (!root) return;   // nothing to keep (the file stays as it was)
            var list = new PlacedList();
            foreach (Transform t in root.transform)
            {
                string path = PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) : null;
                if (string.IsNullOrEmpty(path)) { Debug.LogWarning($"[Nainital] placed by hand: '{t.name}' is not a prefab, so a rebuild cannot put it back"); continue; }
                var it = new PlacedItem { prefab = path, pos = t.position, rot = t.rotation, scale = t.localScale };
                foreach (Transform c in t) it.children.Add(new PlacedChild { name = c.name, pos = c.localPosition, rot = c.localRotation, scale = c.localScale });
                list.items.Add(it);
            }
            File.WriteAllText(PlacedJson, JsonUtility.ToJson(list, true));
            Debug.Log($"[Nainital] placed by hand: {list.items.Count} saved -> {PlacedJson}");
        }
    }

    [InitializeOnLoad]
    static class PlacedByHandSaver
    {
        static PlacedByHandSaver() => EditorSceneManager.sceneSaved += s =>
        {
            if (s.path == WorldBuilder.ScenePath) WorldBuilder.WritePlaced(s);
        };
    }
}

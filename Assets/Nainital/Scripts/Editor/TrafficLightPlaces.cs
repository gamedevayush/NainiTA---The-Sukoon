using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The traffic lights in the editor (owner, 25 Nov 2026: "traffic lights kis naam se hain unity me? 3d world me dikh nhi
    /// rhe" / "what if i want to edit their place in editor?"). The game finds its signalled junctions at start
    /// (TrafficSignals); Build World also builds them into the scene under "Traffic Lights (placed)" so they can be seen and
    /// moved. Move or turn a post ("Signal x,z" > "Post n road"), save the scene: every post's place goes into
    /// Assets/Resources/TrafficLightPlaces.json and the game stands each post there (and hides this editor copy). Nainital >
    /// Traffic Lights > Back to automatic places forgets them.
    /// </summary>
    [InitializeOnLoad]
    public static class TrafficLightPlaces
    {
        public const string JsonPath = "Assets/Resources/TrafficLightPlaces.json";

        static TrafficLightPlaces()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorSceneManager.sceneSaving += (scene, path) => Write(scene);
        }

        static GameObject Root(Scene s)
        {
            foreach (var g in s.GetRootGameObjects()) if (g.name == TrafficSignals.PlacedRoot) return g;
            return null;
        }

        /// <summary>Build the posts into the open scene from the saved places (or the automatic ones).</summary>
        [MenuItem("Nainital/Traffic Lights/Show in Scene (to move)", priority = 40)]
        public static void ShowMenu() => Show(SceneManager.GetActiveScene());

        internal static void Show(Scene s)
        {
            var old = Root(s);
            if (old) Object.DestroyImmediate(old);
            RoadNetwork net = null;   // a ScriptableObject, held by the scene's TrafficManager
            foreach (var g in s.GetRootGameObjects())
            {
                var tm = g.GetComponentInChildren<TrafficManager>(true);
                if (tm && tm.network) { net = tm.network; break; }
            }
            if (!net) { Debug.LogWarning("[Nainital] traffic lights: no RoadNetwork in the scene"); return; }
            AssetDatabase.Refresh();   // the latest places file
            var root = new GameObject(TrafficSignals.PlacedRoot);
            SceneManager.MoveGameObjectToScene(root, s);
            var sig = root.AddComponent<TrafficSignals>();
            Physics.SyncTransforms();   // the kerb search ray-casts and checks for things standing there
            sig.Build(net);
            int n = sig.junctions.Count;
            Object.DestroyImmediate(sig);   // the editor copy has no logic: the game's own TrafficSignals runs the lights
            Debug.Log($"[Nainital] traffic lights in the scene to move: {n} junctions under \"{TrafficSignals.PlacedRoot}\"");
        }

        static void Write(Scene s)
        {
            var root = Root(s);
            if (!root) return;
            var list = new TrafficSignals.PlaceList();
            foreach (Transform j in root.transform)
                foreach (Transform post in j)
                {
                    if (!post.name.StartsWith("Post ")) continue;
                    var parts = post.name.Split(' ');
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int k)) continue;
                    list.posts.Add(new TrafficSignals.Place { key = TrafficSignals.PostKey(j.name, k - 1), pos = post.position, rot = post.rotation });
                }
            string json = JsonUtility.ToJson(list, true);
            if (File.Exists(JsonPath) && File.ReadAllText(JsonPath) == json) return;
            File.WriteAllText(JsonPath, json);
            AssetDatabase.ImportAsset(JsonPath);
            Debug.Log($"[Nainital] traffic light places saved: {list.posts.Count} posts -> {JsonPath}");
        }

        [MenuItem("Nainital/Traffic Lights/Back to automatic places", priority = 41)]
        static void Forget()
        {
            if (File.Exists(JsonPath)) AssetDatabase.DeleteAsset(JsonPath);
            var root = Root(SceneManager.GetActiveScene());
            if (root) Object.DestroyImmediate(root);
            Show(SceneManager.GetActiveScene());
            Debug.Log("[Nainital] traffic light places forgotten: placed automatically again");
        }
    }
}

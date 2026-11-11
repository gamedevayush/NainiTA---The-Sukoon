using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Opens the Nainital scene and frames the player (used when launching the Editor).</summary>
    public static class Startup
    {
        [MenuItem("Nainital/0. Open Nainital scene", priority = 0)]
        public static void OpenNainital()
        {
            if (System.IO.File.Exists(WorldBuilder.ScenePath)) EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            EditorApplication.delayCall += Frame;
        }

        static void Frame()
        {
            var player = GameObject.Find("Player");
            if (!player) return;
            Selection.activeGameObject = player;
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) { EditorApplication.delayCall += Frame; return; }
            var t = player.transform;
            sv.LookAt(t.position + Vector3.up * 1.5f + t.forward * 30f, Quaternion.LookRotation(t.forward + Vector3.down * 0.25f), 70f);
            sv.Repaint();
        }
    }
}

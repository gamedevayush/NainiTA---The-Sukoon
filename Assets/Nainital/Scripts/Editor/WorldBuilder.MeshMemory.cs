using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The world's meshes (WorldMeshes.asset) are made in code, and a mesh made in code keeps "Read/Write" on: the player
    /// then holds every one twice, a CPU copy beside the GPU one. 24 Nov 2026 that was 412 MB of the 3 GB the iPhone
    /// allows, and iOS killed the game a few seconds into play ("exceeded mem limit: ActiveHard 3072 MB"). A mesh that
    /// only draws needs no CPU copy; meshes a MeshCollider uses keep theirs (bakeCollisionMeshes is off, so the player
    /// cooks them from that copy). No runtime code reads world meshes (only vehicles, characters and the bus display,
    /// none of them in the bank).
    /// Build World runs it last; `call MeshMemory.Run` (Bridge/batch.txt) does it on the saved scene without a rebuild.
    /// </summary>
    public static class MeshMemory
    {
        const string BankPath = WorldBuilder.Gen + "/WorldMeshes.asset";

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(WorldBuilder.ScenePath, OpenSceneMode.Single);
            var bank = AssetDatabase.LoadMainAssetAtPath(BankPath);
            if (!bank) { Debug.LogError("[Nainital] MeshMemory: no " + BankPath); return; }
            MarkRenderOnly(bank);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        public static void MarkRenderOnly(Object bank)
        {
            var colliderMeshes = new HashSet<Mesh>();
            foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include))
                if (mc.sharedMesh) colliderMeshes.Add(mc.sharedMesh);

            string bankPath = AssetDatabase.GetAssetPath(bank);
            int marked = 0, kept = 0;
            long bytes = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(bankPath))
            {
                if (!(o is Mesh m)) continue;
                if (colliderMeshes.Contains(m)) { kept++; continue; }
                var so = new SerializedObject(m);
                var p = so.FindProperty("m_IsReadable");
                if (p == null || !p.boolValue) continue;
                long idx = 0;
                for (int s = 0; s < m.subMeshCount; s++) idx += m.GetIndexCount(s);
                bytes += (long)m.vertexCount * 32 + idx * 2;   // rough
                p.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                marked++;
            }
            EditorUtility.SetDirty(bank);
            Debug.Log($"[Nainital] MeshMemory: {marked} world meshes render-only (~{bytes / (1 << 20)} MB of CPU copies gone), " +
                      $"{kept} kept readable for colliders");
        }
    }
}

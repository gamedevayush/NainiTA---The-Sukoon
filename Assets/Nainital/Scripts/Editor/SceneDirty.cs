using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Bridge check before closing the owner's open editor (26 Nov 2026): does any open scene have changes that
    /// are not saved? `call SceneDirty.Report` -> "[Nainital] scene dirty: ..." lines.</summary>
    public static class SceneDirty
    {
        public static void Report()
        {
            int n = EditorSceneManager.sceneCount, dirty = 0;
            for (int i = 0; i < n; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.isDirty) dirty++;
                Debug.Log($"[Nainital] scene dirty: {s.path} loaded={s.isLoaded} dirty={s.isDirty}");
            }
            Debug.Log($"[Nainital] scene dirty: {dirty} of {n} scenes have unsaved changes");
        }
    }
}

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital.EditorTools
{
    /// <summary>Steadier, sharper shadows (user, 2026-11-12: "shadows poore mein flicker"): on phones the one 1024 shadow
    /// map was spread over 160 m (16 cm per texel, so edges shimmered as the camera moved). Phones now get two cascades
    /// over 70 m at 2048; PC four cascades at 2048 over 160 m. Set through the URP assets' serialized fields.</summary>
    public static class ShadowSettings
    {
        static void Set(string path, float distance, int cascades, int res)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (!asset) { Debug.LogWarning("[Nainital] no " + path); return; }
            var so = new SerializedObject(asset);
            so.FindProperty("m_ShadowDistance").floatValue = distance;
            so.FindProperty("m_ShadowCascadeCount").intValue = cascades;
            so.FindProperty("m_MainLightShadowmapResolution").intValue = res;
            var c2 = so.FindProperty("m_Cascade2Split"); if (c2 != null) c2.floatValue = 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Debug.Log($"[Nainital] shadows {path}: {distance} m, {cascades} cascades, {res}");
        }

        public static void Apply()
        {
            Set("Assets/Settings/Mobile_RPAsset.asset", 70f, 2, 2048);
            Set("Assets/Settings/PC_RPAsset.asset", 160f, 4, 2048);
        }
    }
}

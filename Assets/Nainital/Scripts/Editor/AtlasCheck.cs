using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>The building atlas material (M_Atlas): what it holds on disk and in the scene, and a re-link that puts its
    /// generated textures back and saves them into the asset (2026-11-12: the buildings turned white after the material
    /// was reloaded from disk without them).</summary>
    public static class AtlasCheck
    {
        const string Mat = "Assets/Nainital/Generated/Materials/M_Atlas.mat";
        const string Tex = "Assets/Nainital/Generated/Textures/";

        static string Name(Texture t) => t ? t.name : "NONE";

        public static void Report()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Mat);
            if (!m) { Debug.Log("[Nainital] atlas: no M_Atlas"); return; }
            Debug.Log($"[Nainital] atlas asset: shader {m.shader.name}, base {Name(m.GetTexture("_BaseMap"))}, normal {Name(m.GetTexture("_BumpMap"))}, emission {Name(m.GetTexture("_EmissionMap"))}, instancing {m.enableInstancing}, has _BaseMap prop {m.shader.FindPropertyIndex("_BaseMap") >= 0}, persistent base {(m.GetTexture("_BaseMap") ? EditorUtility.IsPersistent(m.GetTexture("_BaseMap")) : false)}");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith("Houses")) continue;
                var s = r.sharedMaterial;
                Debug.Log($"[Nainital] atlas in scene ({r.name}): {(s ? s.name : "null")} same asset {s == m}, base {(s ? Name(s.GetTexture("_BaseMap")) : "-")}, path {AssetDatabase.GetAssetPath(s)}");
                break;
            }
        }

        public static void Relink()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Mat);
            if (!m) { Debug.Log("[Nainital] atlas: no M_Atlas"); return; }
            Texture2D T(string f) => AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + f);
            var b = T("Atlas_Base.png");
            var n = T("Atlas_Normal.png");
            var e = T("Atlas_Emission.png");
            var mask = T("Atlas_DetailMask.png");
            if (b) { m.SetTexture("_BaseMap", b); m.mainTexture = b; }
            if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            if (e) { m.SetTexture("_EmissionMap", e); m.EnableKeyword("_EMISSION"); }
            if (mask && m.HasProperty("_DetailMask")) m.SetTexture("_DetailMask", mask);
            var pd = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nainital/Data/PBR/plaster_detail.png");
            var pn = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nainital/Data/PBR/white_rough_plaster_normal.png");
            if (pd && m.HasProperty("_DetailAlbedoMap")) { m.SetTexture("_DetailAlbedoMap", pd); m.SetTextureScale("_DetailAlbedoMap", new Vector2(48f, 48f)); }
            if (pn && m.HasProperty("_DetailNormalMap")) m.SetTexture("_DetailNormalMap", pn);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            Debug.Log($"[Nainital] atlas relinked: base {Name(b)}, normal {Name(n)}, emission {Name(e)}, mask {Name(mask)}, plaster {Name(pd)}/{Name(pn)}");
            Report();
        }
    }
}

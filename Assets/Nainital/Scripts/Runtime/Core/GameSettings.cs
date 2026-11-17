using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nainital
{
    /// <summary>
    /// The player's settings (user, 2026-11-17: "volume ya jo bhi settings hoti hain"), kept in PlayerPrefs and applied at
    /// once: sound (everything, the surroundings, effects), controls (look speed, invert Y, vibration), graphics (quality,
    /// 30 or 60 fps) and the language (Loc).
    /// </summary>
    public static class GameSettings
    {
        static bool loaded;
        static float master = 1f, ambience = 1f, effects = 1f, look = 1f;
        static bool invertY, haptics = true, fps60 = true;
        static int quality = 2;

        public static event System.Action Changed;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            master = PlayerPrefs.GetFloat("nt_vol_master", 1f);
            ambience = PlayerPrefs.GetFloat("nt_vol_amb", 1f);
            effects = PlayerPrefs.GetFloat("nt_vol_fx", 1f);
            look = PlayerPrefs.GetFloat("nt_look", 1f);
            invertY = PlayerPrefs.GetInt("nt_invert_y", 0) == 1;
            haptics = PlayerPrefs.GetInt("nt_haptics", 1) == 1;
            fps60 = PlayerPrefs.GetInt("nt_fps60", 1) == 1;
            quality = PlayerPrefs.GetInt("nt_quality", Application.isMobilePlatform ? 1 : 2);
        }

        static void Save(string key, float v) { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); Changed?.Invoke(); }
        static void Save(string key, int v) { PlayerPrefs.SetInt(key, v); PlayerPrefs.Save(); Changed?.Invoke(); }

        /// <summary>Everything the game plays (AudioListener).</summary>
        public static float Master { get { Load(); return master; } set { Load(); master = Mathf.Clamp01(value); AudioListener.volume = master; Save("nt_vol_master", master); } }
        /// <summary>The surroundings: birds, wind, the lake, the bazaar, rain (AudioManager's beds).</summary>
        public static float Ambience { get { Load(); return ambience; } set { Load(); ambience = Mathf.Clamp01(value); Save("nt_vol_amb", ambience); } }
        /// <summary>Steps, punches, horns, engines, splashes.</summary>
        public static float Effects { get { Load(); return effects; } set { Load(); effects = Mathf.Clamp01(value); Save("nt_vol_fx", effects); } }
        /// <summary>How fast a swipe or the mouse turns the camera, 0.4 to 2.</summary>
        public static float LookSpeed { get { Load(); return look; } set { Load(); look = Mathf.Clamp(value, 0.4f, 2f); Save("nt_look", look); } }
        public static bool InvertY { get { Load(); return invertY; } set { Load(); invertY = value; Save("nt_invert_y", value ? 1 : 0); } }
        public static bool HapticsOn { get { Load(); return haptics; } set { Load(); haptics = value; Save("nt_haptics", value ? 1 : 0); } }
        public static bool Fps60 { get { Load(); return fps60; } set { Load(); fps60 = value; Application.targetFrameRate = value ? 60 : 30; Save("nt_fps60", value ? 1 : 0); } }
        /// <summary>0 low, 1 medium, 2 high.</summary>
        public static int Quality { get { Load(); return quality; } set { Load(); quality = Mathf.Clamp(value, 0, 2); ApplyGraphics(); Save("nt_quality", quality); } }

        public static string QualityName(int q) => Loc.English ? new[] { "Low", "Medium", "High" }[q] : new[] { "Kam", "Theek", "Zabardast" }[q];

        /// <summary>Everything at once (the game's start).</summary>
        public static void ApplyAll()
        {
            Load();
            AudioListener.volume = master;
            Application.targetFrameRate = fps60 ? 60 : 30;
            ApplyGraphics();
        }

        /// <summary>Render scale, shadows, ambient occlusion (PC), grass, tree distances, depth of field.</summary>
        public static void ApplyGraphics()
        {
            int q = Quality;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = new[] { 0.7f, 0.85f, 1f }[q];
                urp.shadowDistance = new[] { 50f, 100f, 160f }[q];
                foreach (var rd in urp.rendererDataList)
                {
                    if (!rd) continue;
                    foreach (var f in rd.rendererFeatures)
                        if (f && f.GetType().Name.Contains("ScreenSpaceAmbientOcclusion") && !Application.isMobilePlatform) f.SetActive(q == 2);
                }
            }
            foreach (var g in Object.FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None))
            {
                g.radius = new[] { 0.1f, 32f, 55f }[q];
                g.enabled = q > 0;
            }
            foreach (var t in Object.FindObjectsByType<TreeRenderer>(FindObjectsSortMode.None))
            {
                t.maxDistance = new[] { 700f, 1100f, 1500f }[q];
                t.lod0Distance = new[] { 55f, 85f, 110f }[q];
            }
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v.sharedProfile && v.sharedProfile.TryGet<DepthOfField>(out var dof)) dof.active = q == 2;
        }
    }
}

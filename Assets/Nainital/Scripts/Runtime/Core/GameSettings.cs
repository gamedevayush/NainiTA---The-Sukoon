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

        public static string QualityName(int q) => Loc.L(new[] { "Kam", "Theek", "Zabardast" }[q], new[] { "Low", "Medium", "High" }[q]);

        /// <summary>Everything at once (the game's start).</summary>
        public static void ApplyAll()
        {
            Load();
            AudioListener.volume = master;
            Application.targetFrameRate = fps60 ? 60 : 30;
            ApplyGraphics();
        }

        /// <summary>Render scale, shadows, ambient occlusion (PC), grass, tree distances, depth of field, ground-floor rooms.</summary>
        public static void ApplyGraphics()
        {
            int q = Quality;
            // the rooms behind ordinary ground-floor windows (AtlasLit, NainitalRooms.hlsl): off on Low; Mall Road's shop
            // glass (ShopGlass) always shows its room
            Shader.SetGlobalFloat("_NainitalInteriorOff", q == 0 ? 1f : 0f);
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
            HighGrade(q == 2);
            ApplyCull(Camera.main);
            ApplyPhoneShadows(q);
        }

        /// <summary>Things too small to matter far away are not drawn there (owner, 25 Nov 2026: "game ko or realistic but
        /// not heavy"): vehicles (layer Vehicle) and people / animals (layer Character) past these distances cost no draw
        /// calls, skinning or shadows. The player (layer Player) and the world are never culled this way.</summary>
        public static void ApplyCull(Camera cam)
        {
            if (!cam) return;
            int q = Quality;
            var d = new float[32];
            d[10] = new[] { 140f, 200f, 280f }[q];   // VehicleFactory.VehicleLayer
            d[9] = new[] { 70f, 100f, 140f }[q];     // Character: townsfolk, animals
            cam.layerCullDistances = d;
            cam.layerCullSpherical = true;
        }

        /// <summary>On the phone the shadow map covers less ground (a 2048 map over 100 m draws soft, costly shadows): 40 /
        /// 60 / 90 m, and the low setting's map is 1024 (a quarter of the memory).</summary>
        static void ApplyPhoneShadows(int q)
        {
            if (!Application.isMobilePlatform) return;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = new[] { 40f, 60f, 90f }[q];
                urp.mainLightShadowmapResolution = q == 0 ? 1024 : 2048;
            }
        }

        static Volume highVol;

        /// <summary>The High setting's own grade (owner, 23 Nov 2026: "ek volume bnaado, high graphics ki ..usme AO, Color
        /// Corrections wgera dedo"), over the world's base grade (NainitalPost, priority 0) and under the night grade
        /// (priority 1, DayNightCycle): more contrast and colour, lift / gamma / gain and split toning (cool shadows, warm
        /// light), a warmer white balance, shadows / midtones / highlights, finer bloom, a deeper vignette, a little more
        /// motion blur. The ambient occlusion is the SSAO pass of PC_Renderer, on at High (above; 0.85, 0.45 m, high samples).</summary>
        static void HighGrade(bool on)
        {
            if (!on) { if (highVol) highVol.enabled = false; return; }
            if (!highVol)
            {
                var go = new GameObject("High Graphics Grade");
                Object.DontDestroyOnLoad(go);
                highVol = go.AddComponent<Volume>();
                highVol.isGlobal = true;
                highVol.priority = 0.5f;
                var p = ScriptableObject.CreateInstance<VolumeProfile>();
                p.name = "HighGraphics";
                // (first try at 00:55 read as dusk at 11 am - blue shadows from three places on top of the SSAO: now the
                // shadows stay neutral and a touch lifted, the warmth only in the light)
                var ca = p.Add<ColorAdjustments>(true);
                ca.postExposure.Override(0.42f);   // the SSAO darkens corners: a little more light back
                ca.contrast.Override(18f);
                ca.saturation.Override(13f);
                var lgg = p.Add<LiftGammaGain>(true);
                lgg.lift.Override(new Vector4(1f, 1f, 1f, 0.01f));
                lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0.02f));
                lgg.gain.Override(new Vector4(1.03f, 1.01f, 0.97f, 0.02f));
                var st = p.Add<SplitToning>(true);
                st.shadows.Override(new Color(0.5f, 0.5f, 0.52f));
                st.highlights.Override(new Color(0.58f, 0.54f, 0.48f));
                st.balance.Override(25f);
                var wb = p.Add<WhiteBalance>(true);
                wb.temperature.Override(5f);
                var smh = p.Add<ShadowsMidtonesHighlights>(true);
                smh.shadows.Override(new Vector4(1f, 1f, 1.02f, 0f));
                smh.highlights.Override(new Vector4(1.05f, 1.01f, 0.94f, 0.02f));
                var bloom = p.Add<Bloom>(true);
                bloom.threshold.Override(0.95f);
                bloom.intensity.Override(0.7f);
                bloom.scatter.Override(0.72f);
                bloom.highQualityFiltering.Override(true);
                var vig = p.Add<Vignette>(true);
                vig.intensity.Override(0.22f);
                vig.smoothness.Override(0.45f);
                var mb = p.Add<MotionBlur>(true);
                mb.quality.Override(MotionBlurQuality.Medium);
                mb.intensity.Override(0.26f);
                highVol.sharedProfile = p;
            }
            highVol.enabled = true;
        }

        /// <summary>The High grade's volume now (tests), or null.</summary>
        public static Volume HighVolume => highVol;
    }
}

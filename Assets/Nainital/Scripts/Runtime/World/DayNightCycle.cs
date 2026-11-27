using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Sun/moon, sky, ambient, fog, building window glow and street lamps over a 24 h cycle.
    /// One directional light acts as sun by day and moon by night (keeps one shadow map).
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Range(0, 24)] public float timeOfDay = 17.5f;
        public float minutesPerDay = 24f;
        public bool paused;
        public Light sun;
        /// <summary>The cinema camera (CinemaRunner): the sun's / moon's shadows at least this dark. 0 = off.</summary>
        public static float ShadowMin;
        public Material skyMaterial;
        public Material[] emissiveMaterials;   // atlas materials whose emission glows at night
        public Color emissionColor = new Color(1f, 0.82f, 0.55f);
        public float emissionIntensity = 2.2f;
        public ReflectionProbe lakeProbe;
        public UnityEngine.Rendering.Volume nightVolume;   // night colour grade (exposure, blue tint, stronger bloom)
        public float valleyHeight;                          // lake level: the haze pools around this height
        public float latitude = 29.39f;

        [System.NonSerialized] public float weatherDim = 1f;   // set by WeatherSystem (overcast)
        [System.NonSerialized] public float flash;             // lightning
        public float NightFactor { get; private set; }
        public float SunElevation { get; private set; }

        static readonly int SunDirId = Shader.PropertyToID("_SunDir");
        static readonly int MoonDirId = Shader.PropertyToID("_MoonDir");
        static readonly int NightId = Shader.PropertyToID("_NightFactor");
        static float interiorDim;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        float probeTimer;
        float lastProbeNight = -1f;
        float probeReadyTimer = -1f;
        static readonly int ProbeReadyId = Shader.PropertyToID("_LakeProbeReady");

        void Awake() => Instance = this;

        static readonly int HourP1Id = Shader.PropertyToID("_NtHourP1"), GameMinutesId = Shader.PropertyToID("_NtGameMinutes");
        float gameMinutes, lastHourForMinutes = -1f;
        void OnEnable() => Shader.SetGlobalFloat(ProbeReadyId, 0f);
        void OnDisable() => Shader.SetGlobalFloat(ProbeReadyId, 0f);

        public void ToggleDayNight()
        {
            timeOfDay = NightFactor > 0.5f ? 9.5f : 20.5f;
            lastProbeNight = -1f;
            GameEvents.ShowToast(NightFactor > 0.5f ? "Subah ho gayi" : "Raat ho gayi - Mall Road ki lights dekho!", 2f);
        }

        void Update()
        {
            if (GameInput.NightTogglePressed && !GameInput.UiBlocking) ToggleDayNight();
            if (!paused) timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime * 24f / (minutesPerDay * 60f), 24f);
            Apply();
        }

        void OnValidate() { if (sun) Apply(); }

        Vector3 lastLightFrom = Vector3.zero;
        bool lastUseMoon;

        public void Apply()
        {
            // Sun path: rises in the east (~6:00), sets in the west (~18:30); south-leaning at this latitude.
            float dayT = (timeOfDay - 6.2f) / 12.3f;               // 0 sunrise .. 1 sunset
            float elev = Mathf.Sin(dayT * Mathf.PI) * (90f - latitude + 8f);
            float azim = Mathf.Lerp(-100f, 100f, dayT);            // degrees from south
            Vector3 sunDir = Quaternion.Euler(0, 180f + azim, 0) * Quaternion.Euler(-elev, 0, 0) * Vector3.forward;
            // sunDir points from ground towards the sun
            Vector3 moonDir = Quaternion.Euler(0, 20f + Mathf.Lerp(-90f, 90f, Mathf.Repeat((timeOfDay - 18.5f) / 12f, 1f)), 0) *
                              Quaternion.Euler(-Mathf.Sin(Mathf.Repeat((timeOfDay - 18.5f) / 12f, 1f) * Mathf.PI) * 55f, 0, 0) * Vector3.forward;
            SunElevation = elev;

            float day = Mathf.Clamp01((elev + 4f) / 14f);           // 0 at night, 1 when sun > 10 deg
            float golden = Mathf.Clamp01(1f - Mathf.Abs(elev - 4f) / 14f) * (elev > -6f ? 1f : 0f);
            NightFactor = 1f - Mathf.Clamp01((elev + 6f) / 10f);

            if (sun)
            {
                bool useMoon = elev < -2f;
                Vector3 lightFrom = useMoon ? moonDir : sunDir;
                if (lightFrom.y < 0.08f) lightFrom.y = 0.08f;
                // the light turns in steps of 0.2 degrees, not a little every frame: a shadow map redrawn from a slightly
                // new angle each frame makes every shadow edge crawl and flicker (user, 2026-11-12)
                // (the first time always: Vector3.Angle with the zero vector is 0, so a game started by day kept the scene's
                // sun - 30 degrees up in the north-east - all day, Mall Road in the hill's shade; found 27 Nov 2026)
                if (lastLightFrom == Vector3.zero || useMoon != lastUseMoon || Vector3.Angle(lastLightFrom, lightFrom) > 0.2f)
                {
                    lastLightFrom = lightFrom;
                    lastUseMoon = useMoon;
                    sun.transform.rotation = Quaternion.LookRotation(-lightFrom.normalized, Vector3.up);
                }
                Color sunCol = Color.Lerp(new Color(1f, 0.55f, 0.3f), new Color(1f, 0.96f, 0.88f), Mathf.Clamp01(elev / 25f));
                Color moonCol = new Color(0.5f, 0.58f, 0.85f);
                float fade = Mathf.Clamp01(Mathf.Abs(elev + 2f) / 4f);  // dip to 0 while swapping sun <-> moon
                sun.color = useMoon ? moonCol : sunCol;
                // the moon a little stronger than before (0.07: away from a lamp a street was black - owner, 21 Nov 2026: "raat ki
                // overall lighting... bahut area blackout ya dimmed hain")
                sun.intensity = (useMoon ? 0.2f : Mathf.Lerp(0.3f, 1.8f, day)) * fade * weatherDim + flash * 3f;   // a mountain sun, with less ambient below
                float oc = WeatherSystem.Instance ? WeatherSystem.Instance.Overcast : 0f;
                sun.color = Color.Lerp(sun.color, new Color(0.8f, 0.82f, 0.85f), oc);   // what gets through cloud is grey
                if (flash > 0f) sun.color = Color.Lerp(sun.color, new Color(0.8f, 0.85f, 1f), flash);
                // skylight still fills the shade (0.85 left shaded streets near black); under cloud no hard shadow at all
                sun.shadowStrength = Mathf.Max(ShadowMin, Mathf.Lerp(useMoon ? 0.55f : 0.72f, 0.08f, oc));
            }

            // Ambient (trilight): at night a moonlit blue - the lamps, windows and headlights still carry the town, but a street
            // between two lamps, a lane without one, the wall of a house read in the moonlight instead of going black (the
            // old 0.016 / 0.01 / 0.004: owner, 21 Nov 2026: "abhi bhi bahut area balckout ya dimmed hain")
            // daylight ambient lowered (15 Nov, photoreal pass): a strong flat fill took the shape out of every wall and slope
            // (at 0.46 / 0.38 / 0.16 the streets in shade went near black: skylight still lights a shaded street well)
            Color skyDay = new Color(0.52f, 0.62f, 0.78f), skyNight = new Color(0.07f, 0.083f, 0.12f), skyGold = new Color(0.75f, 0.5f, 0.4f);
            Color eqDay = new Color(0.45f, 0.47f, 0.45f), eqNight = new Color(0.046f, 0.054f, 0.077f);
            Color grDay = new Color(0.20f, 0.21f, 0.16f), grNight = new Color(0.018f, 0.019f, 0.025f);
            if (nightVolume) nightVolume.weight = Mathf.SmoothStep(0f, 1f, NightFactor);
            // overcast (rain, fog, cloud: WeatherSystem.Overcast): the sun's share comes back as an even grey light from the
            // whole sky - flat, shadowless, a little dimmer, no blue in it (owner, 23 Nov 2026: "baarish me bhi dhoop hai hlki")
            float overcast = WeatherSystem.Instance ? WeatherSystem.Instance.Overcast : 0f;
            float wFog = WeatherSystem.Instance ? WeatherSystem.Instance.Fog : 0f;
            float wRain = WeatherSystem.Instance ? WeatherSystem.Instance.Rain : 0f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(Color.Lerp(skyNight, skyDay, day), skyGold, golden * 0.5f), new Color(0.6f, 0.62f, 0.65f) * Mathf.Lerp(0.12f, 1f, day), overcast);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(eqNight, eqDay, day), new Color(0.5f, 0.51f, 0.52f) * Mathf.Lerp(0.1f, 1f, day), overcast);
            RenderSettings.ambientGroundColor = Color.Lerp(Color.Lerp(grNight, grDay, day), new Color(0.22f, 0.22f, 0.21f) * Mathf.Lerp(0.1f, 1f, day), overcast);
            // inside a building (InteriorZone): the sky's light does not come through the walls - a room's dim ambient, the
            // same by day and night; its own lamps light it (owner, 23 Nov 2026, the game parlour)
            interiorDim = Mathf.MoveTowards(interiorDim, InteriorZone.CameraInside ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);
            if (interiorDim > 0f)
            {
                float k = Mathf.Lerp(1f, 0.16f, interiorDim);
                RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor * k, new Color(0.05f, 0.05f, 0.06f), interiorDim * 0.5f);
                RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor * k, new Color(0.045f, 0.045f, 0.05f), interiorDim * 0.5f);
                RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor * k, new Color(0.03f, 0.03f, 0.035f), interiorDim * 0.5f);
            }

            // Fog: valley haze by day, dark blue at night; in weather a grey wall
            Color fogDay = new Color(0.68f, 0.75f, 0.82f), fogNight = new Color(0.02f, 0.025f, 0.04f), fogGold = new Color(0.85f, 0.62f, 0.48f);
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(Color.Lerp(fogNight, fogDay, day), fogGold, golden * 0.6f), new Color(0.6f, 0.62f, 0.64f) * Mathf.Lerp(0.1f, 1f, day), Mathf.Max(overcast * 0.8f, wFog));
            // the sky in it: grey under cloud, the fog's colour all over in a fog (Sky.shader, AtmosphereFog.shader)
            Shader.SetGlobalFloat("_Overcast", overcast);
            Shader.SetGlobalFloat("_SkyFog", Mathf.Clamp01(wFog * 0.9f));

            // aerial-perspective haze for the full-screen atmosphere pass
            Color hz = Color.Lerp(Color.Lerp(new Color(0.017f, 0.021f, 0.033f), new Color(0.60f, 0.70f, 0.85f), day), new Color(0.9f, 0.66f, 0.5f), golden * 0.6f);   // night: the ridges stand out against a moonlit haze
            hz = Color.Lerp(hz, new Color(0.62f, 0.65f, 0.68f) * Mathf.Lerp(0.15f, 1f, day), wFog * 0.7f);
            bool moonUp = elev < -2f;
            Color scatter = moonUp ? new Color(0.02f, 0.025f, 0.04f)
                                   : Color.Lerp(new Color(1f, 0.58f, 0.32f), new Color(1f, 0.95f, 0.86f), Mathf.Clamp01(elev / 30f)) * 1.1f;
            Shader.SetGlobalColor("_HazeColor", hz);
            Shader.SetGlobalColor("_SunHazeColor", Color.Lerp(scatter, hz, wFog * 0.6f));
            Shader.SetGlobalVector("_HazeLightDir", moonUp ? moonDir : sunDir);
            // far ridges fade to blue-grey like the photos; lighter than the old 0.00072, which milked out the town across the lake
            Shader.SetGlobalFloat("_HazeDensity", 0.00046f * (1f + wFog * 15f + wRain * 3.5f));
            Shader.SetGlobalFloat("_HazeBase", valleyHeight - 30f);
            Shader.SetGlobalFloat("_HazeFalloff", Mathf.Lerp(0.0045f, 0.0022f, wFog));
            Shader.SetGlobalFloat("_HazeMax", Mathf.Lerp(0.74f, 0.97f, wFog));

            Shader.SetGlobalVector(SunDirId, sunDir);
            Shader.SetGlobalVector(MoonDirId, moonDir);
            Shader.SetGlobalFloat(NightId, NightFactor);
            // windows on/off and shop shutters (Shaders/NainitalRooms.hlsl): only in play, the editor keeps all lit and open
            if (Application.isPlaying)
            {
                if (lastHourForMinutes < 0f) lastHourForMinutes = timeOfDay;
                float dh = Mathf.Repeat(timeOfDay - lastHourForMinutes, 24f);
                if (dh < 6f) gameMinutes += dh * 60f;
                lastHourForMinutes = timeOfDay;
                Shader.SetGlobalFloat(HourP1Id, timeOfDay + 1f);
                Shader.SetGlobalFloat(GameMinutesId, gameMinutes);
            }
            else Shader.SetGlobalFloat(HourP1Id, 0f);
            if (skyMaterial)
            {
                skyMaterial.SetVector(SunDirId, sunDir);
                skyMaterial.SetVector(MoonDirId, moonDir);
                skyMaterial.SetFloat(NightId, NightFactor);
            }

            if (emissiveMaterials != null)
            {
                float glow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((NightFactor - 0.15f) / 0.6f));
                // Never exactly black: URP strips the _EMISSION keyword from materials whose emission is black.
                Color e = emissionColor * Mathf.Max(emissionIntensity * glow, 0.002f);
                foreach (var m in emissiveMaterials)
                {
                    if (!m) continue;
                    m.EnableKeyword("_EMISSION");
                    m.SetColor(EmissionId, e);
                }
            }

            // Re-render the lake reflection when the lighting changed noticeably.
            if (lakeProbe && Application.isPlaying)
            {
                probeTimer -= Time.deltaTime;
                if (probeTimer <= 0f && Mathf.Abs(NightFactor - lastProbeNight) > 0.04f || lastProbeNight < 0f)
                {
                    lakeProbe.RenderProbe();
                    lastProbeNight = NightFactor;
                    probeTimer = 3f;
                    if (probeReadyTimer < 0f) probeReadyTimer = 0.6f;
                }
                if (probeReadyTimer > 0f)
                {
                    probeReadyTimer -= Time.deltaTime;
                    if (probeReadyTimer <= 0f) Shader.SetGlobalFloat(ProbeReadyId, 1f);
                }
            }
        }
    }
}

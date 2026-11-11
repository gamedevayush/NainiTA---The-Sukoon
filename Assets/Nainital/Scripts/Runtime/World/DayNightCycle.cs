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
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        float probeTimer;
        float lastProbeNight = -1f;
        float probeReadyTimer = -1f;
        static readonly int ProbeReadyId = Shader.PropertyToID("_LakeProbeReady");

        void Awake() => Instance = this;

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
                sun.transform.rotation = Quaternion.LookRotation(-lightFrom.normalized, Vector3.up);
                Color sunCol = Color.Lerp(new Color(1f, 0.55f, 0.3f), new Color(1f, 0.96f, 0.88f), Mathf.Clamp01(elev / 25f));
                Color moonCol = new Color(0.5f, 0.58f, 0.85f);
                float fade = Mathf.Clamp01(Mathf.Abs(elev + 2f) / 4f);  // dip to 0 while swapping sun <-> moon
                sun.color = useMoon ? moonCol : sunCol;
                sun.intensity = (useMoon ? 0.07f : Mathf.Lerp(0.3f, 1.4f, day)) * fade * weatherDim + flash * 3f;
                if (flash > 0f) sun.color = Color.Lerp(sun.color, new Color(0.8f, 0.85f, 1f), flash);
                sun.shadowStrength = useMoon ? 0.55f : 0.85f;
            }

            // Ambient (trilight): near black at night - only lamps, windows and headlights light the town
            Color skyDay = new Color(0.55f, 0.66f, 0.82f), skyNight = new Color(0.016f, 0.019f, 0.03f), skyGold = new Color(0.75f, 0.5f, 0.4f);
            Color eqDay = new Color(0.5f, 0.52f, 0.5f), eqNight = new Color(0.01f, 0.012f, 0.018f);
            Color grDay = new Color(0.22f, 0.24f, 0.18f), grNight = new Color(0.004f, 0.004f, 0.006f);
            if (nightVolume) nightVolume.weight = Mathf.SmoothStep(0f, 1f, NightFactor);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(skyNight, skyDay, day), skyGold, golden * 0.5f);
            RenderSettings.ambientEquatorColor = Color.Lerp(eqNight, eqDay, day);
            RenderSettings.ambientGroundColor = Color.Lerp(grNight, grDay, day);

            // Fog: valley haze by day, dark blue at night
            Color fogDay = new Color(0.68f, 0.75f, 0.82f), fogNight = new Color(0.008f, 0.01f, 0.016f), fogGold = new Color(0.85f, 0.62f, 0.48f);
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(fogNight, fogDay, day), fogGold, golden * 0.6f);

            // aerial-perspective haze for the full-screen atmosphere pass
            float wFog = WeatherSystem.Instance ? WeatherSystem.Instance.Fog : 0f;
            float wRain = WeatherSystem.Instance ? WeatherSystem.Instance.Rain : 0f;
            Color hz = Color.Lerp(Color.Lerp(new Color(0.006f, 0.008f, 0.013f), new Color(0.66f, 0.74f, 0.84f), day), new Color(0.9f, 0.66f, 0.5f), golden * 0.6f);
            hz = Color.Lerp(hz, new Color(0.62f, 0.65f, 0.68f) * Mathf.Lerp(0.15f, 1f, day), wFog * 0.7f);
            bool moonUp = elev < -2f;
            Color scatter = moonUp ? new Color(0.02f, 0.025f, 0.04f)
                                   : Color.Lerp(new Color(1f, 0.58f, 0.32f), new Color(1f, 0.95f, 0.86f), Mathf.Clamp01(elev / 30f)) * 1.1f;
            Shader.SetGlobalColor("_HazeColor", hz);
            Shader.SetGlobalColor("_SunHazeColor", Color.Lerp(scatter, hz, wFog * 0.6f));
            Shader.SetGlobalVector("_HazeLightDir", moonUp ? moonDir : sunDir);
            Shader.SetGlobalFloat("_HazeDensity", 0.00072f * (1f + wFog * 10f + wRain * 2.5f));   // far ridges fade to blue-grey like the photos
            Shader.SetGlobalFloat("_HazeBase", valleyHeight - 30f);
            Shader.SetGlobalFloat("_HazeFalloff", Mathf.Lerp(0.0045f, 0.0022f, wFog));
            Shader.SetGlobalFloat("_HazeMax", Mathf.Lerp(0.8f, 0.97f, wFog));

            Shader.SetGlobalVector(SunDirId, sunDir);
            Shader.SetGlobalVector(MoonDirId, moonDir);
            Shader.SetGlobalFloat(NightId, NightFactor);
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

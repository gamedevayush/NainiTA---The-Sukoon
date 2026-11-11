using UnityEngine;

namespace Nainital
{
    /// <summary>Clear / cloudy / Nainital fog / rain with wet roads and lightning. Cycles on its own or via the pause menu.</summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }
        public enum Kind { Clear, Cloudy, Fog, Rain }

        public Kind current = Kind.Clear;
        public bool autoCycle = true;
        public Material[] wetMaterials;          // roads/pavers get glossy in rain
        public float[] dryGloss;

        public float Rain { get; private set; }
        public float Fog { get; private set; }
        public float Cloud { get; private set; } = 0.35f;

        ParticleSystem rain;
        float nextChange, flash, nextFlash;
        float baseFogStart = -1f, baseFogEnd = -1f;
        static readonly int CloudId = Shader.PropertyToID("_CloudCover");
        static readonly int SmoothId = Shader.PropertyToID("_Smoothness");

        void Awake() => Instance = this;

        void Start()
        {
            nextChange = Time.time + Random.Range(240f, 420f);
            if (wetMaterials != null)
            {
                dryGloss = new float[wetMaterials.Length];
                for (int i = 0; i < wetMaterials.Length; i++) dryGloss[i] = wetMaterials[i] ? wetMaterials[i].GetFloat(SmoothId) : 0f;
            }
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            rain = go.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 22f;
            main.startSize = 0.045f;
            main.maxParticles = Application.isMobilePlatform ? 2000 : 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.75f, 0.82f, 0.95f, 0.55f);
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 50f, 1f);   // rotated 90 deg about X -> a flat 50 x 50 m sheet emitting downwards
            shape.rotation = new Vector3(90f, 0, 0);
            var em = rain.emission;
            em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 3f;
            r.velocityScale = 0.02f;
            if (GameAssets.RainMaterial) r.sharedMaterial = GameAssets.RainMaterial;
            rain.Play();
        }

        public void Set(Kind k)
        {
            current = k;
            nextChange = Time.time + Random.Range(300f, 540f);
            string[] msg = { "Mausam saaf ho gaya", "Baadal chha gaye", "Kohra (fog) aa gaya - Nainital special!", "Baarish shuru! Sadkein geeli hain, dhyan se chalao" };
            GameEvents.ShowToast(msg[(int)k], 2.5f);
        }

        public void Next() => Set((Kind)(((int)current + 1) % 4));

        void Update()
        {
            float dt = Time.deltaTime;
            if (autoCycle && Time.time > nextChange)
            {
                float r = Random.value;
                Set(r < 0.45f ? Kind.Clear : r < 0.7f ? Kind.Cloudy : r < 0.85f ? Kind.Fog : Kind.Rain);
            }
            float tc = 0.35f, tf = 0f, tr = 0f;
            switch (current)
            {
                case Kind.Cloudy: tc = 0.8f; tf = 0.12f; break;
                case Kind.Fog: tc = 0.65f; tf = 1f; break;
                case Kind.Rain: tc = 0.98f; tf = 0.45f; tr = 1f; break;
            }
            Cloud = Mathf.MoveTowards(Cloud, tc, dt * 0.05f);
            Fog = Mathf.MoveTowards(Fog, tf, dt * 0.06f);
            Rain = Mathf.MoveTowards(Rain, tr, dt * 0.08f);
            Shader.SetGlobalFloat(CloudId, Cloud);

            if (baseFogEnd < 0f) { baseFogStart = RenderSettings.fogStartDistance; baseFogEnd = RenderSettings.fogEndDistance; }
            RenderSettings.fogStartDistance = Mathf.Lerp(baseFogStart, 5f, Fog);
            RenderSettings.fogEndDistance = Mathf.Lerp(baseFogEnd, 280f, Fog);

            var cam = Camera.main;
            if (rain && cam)
            {
                rain.transform.position = cam.transform.position + Vector3.up * 14f + cam.transform.forward * 8f;
                var em = rain.emission;
                em.rateOverTime = Rain * (Application.isMobilePlatform ? 1800f : 5000f);
            }
            if (wetMaterials != null)
                for (int i = 0; i < wetMaterials.Length; i++)
                    if (wetMaterials[i]) wetMaterials[i].SetFloat(SmoothId, Mathf.Lerp(dryGloss[i], Mathf.Max(dryGloss[i], 0.98f), Rain));

            // lightning
            var dn = DayNightCycle.Instance;
            if (Rain > 0.8f && Time.time > nextFlash)
            {
                nextFlash = Time.time + Random.Range(8f, 25f);
                flash = 1f;
                if (AudioManager.Instance) AudioManager.Instance.Invoke(nameof(AudioManager.Thunder), Random.Range(0.4f, 2f));
            }
            flash = Mathf.MoveTowards(flash, 0f, dt * 4f);
            if (dn) dn.weatherDim = Cloud > 0.5f ? Mathf.Lerp(1f, 0.55f, (Cloud - 0.5f) * 2f) : 1f;
            if (dn) dn.flash = flash > 0.5f ? flash : 0f;
        }

        void OnDisable()
        {
            if (wetMaterials != null && dryGloss != null)
                for (int i = 0; i < wetMaterials.Length; i++) if (wetMaterials[i]) wetMaterials[i].SetFloat(SmoothId, dryGloss[i]);
            Shader.SetGlobalFloat(CloudId, 0.35f);
        }
    }
}

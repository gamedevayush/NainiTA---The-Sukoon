using UnityEngine;

namespace Nainital
{
    /// <summary>Clear / cloudy / Nainital fog / rain with wet roads and lightning. Cycles on its own or via the pause menu.</summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }
        /// <summary>RainFog: rain in a heavy fog (owner, 23 Nov 2026: "chlte game pe baarish or heavy fog") - the rain's
        /// clouds and wet roads with the fog thicker than the Fog kind's (about 120 m against 280 m).</summary>
        public enum Kind { Clear, Cloudy, Fog, Rain, RainFog }

        public Kind current = Kind.Clear;
        public bool autoCycle = true;
        public Material[] wetMaterials;          // roads/pavers get glossy in rain
        public float[] dryGloss;

        public float Rain { get; private set; }
        public float Fog { get; private set; }
        public float Cloud { get; private set; } = 0.35f;
        /// <summary>How much of the sky the cloud shuts out, 0 sun .. 1 a grey sheet (owner, 23 Nov 2026: "baarish me bhi dhoop
        /// hai hlki..balki mausam grey hojaata hai"): DayNightCycle takes the sun's light and shadows away by it, the sky
        /// greys (Sky.shader _Overcast).</summary>
        public float Overcast { get; private set; }
        /// <summary>The cinema camera (CinemaRunner): cloud in the sky but the sun through it, so the town keeps its shadows
        /// (owner, 27 Nov 2026: "proper shadows... hard and soft shadows bnd hain"). 1 = off.</summary>
        public static float OvercastCap = 1f;

        ParticleSystem rain;
        float nextChange, flash, nextFlash;
        float baseFogStart = -1f, baseFogEnd = -1f;
        static readonly int CloudId = Shader.PropertyToID("_CloudCover");
        static readonly int SmoothId = Shader.PropertyToID("_Smoothness");
        static readonly int WindId = Shader.PropertyToID("_NtWind");
        float wind = 0.35f;
        /// <summary>The wind now, 0.25 still .. 1 a storm (sounds, flags).</summary>
        public float Wind => wind;

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
            // thin streaks (owner, 23 Nov 2026: "baaarish ptli kro..bahut moti hai": 4.5 cm drops read as ropes): 1.5 cm,
            // fainter, a little longer, more of them so the rain stays as heavy
            main.startSize = 0.015f;
            main.maxParticles = Application.isMobilePlatform ? 3000 : 9000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.78f, 0.84f, 0.95f, 0.38f);
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 50f, 1f);   // rotated 90 deg about X -> a flat 50 x 50 m sheet emitting downwards
            shape.rotation = new Vector3(90f, 0, 0);
            var em = rain.emission;
            em.rateOverTime = 0f;
            // a drop that meets a vehicle's roof is gone (owner, 23 Nov 2026: "baarish bus ke andr aari"): collisions with the
            // vehicle layer only (their body boxes reach the roof), so no rain falls inside a bus or a car
            var col = rain.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.collidesWith = 1 << VehicleFactory.VehicleLayer;
            col.enableDynamicColliders = true;
            col.quality = Application.isMobilePlatform ? ParticleSystemCollisionQuality.Medium : ParticleSystemCollisionQuality.High;
            col.lifetimeLoss = 1f;
            col.radiusScale = 0.5f;
            col.bounce = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 4.5f;
            r.velocityScale = 0.02f;
            if (GameAssets.RainMaterial) r.sharedMaterial = GameAssets.RainMaterial;
            rain.Play();
        }

        public void Set(Kind k)
        {
            current = k;
            nextChange = Time.time + Random.Range(300f, 540f);
            string[] msg = { "Mausam saaf ho gaya", "Baadal chha gaye", "Kohra (fog) aa gaya - Nainital special!", "Baarish shuru! Sadkein geeli hain, dhyan se chalao",
                             "Baarish aur ghana kohra - kuch dikh nahi raha, dheere chalao!" };
            GameEvents.ShowToast(msg[(int)k], 2.5f);
        }

        public void Next() => Set((Kind)(((int)current + 1) % 5));

        /// <summary>The weather at once (a story scene's look): no toast, no slow blend, no change for 15 minutes.</summary>
        public void SetNow(Kind k)
        {
            current = k;
            nextChange = Time.time + 900f;
            snap = 3;
            // straight to its values (the world may be frozen: no frames to blend in)
            float tc = 0.35f, tf = 0f, tr = 0f;
            switch (k)
            {
                case Kind.Cloudy: tc = 0.8f; tf = 0.12f; break;
                case Kind.Fog: tc = 0.65f; tf = 1f; break;
                case Kind.Rain: tc = 0.98f; tf = 0.45f; tr = 1f; break;
                case Kind.RainFog: tc = 1f; tf = 1f; tr = 1f; break;
            }
            Cloud = tc; Fog = tf; Rain = tr;
            heavy = k == Kind.RainFog ? 1f : 0f;
            Overcast = Mathf.Min(OvercastCap, k == Kind.Cloudy ? 0.55f : k == Kind.Fog ? 0.8f : k == Kind.Rain || k == Kind.RainFog ? 1f : 0f);
        }
        int snap;
        float heavy;   // 0..1: how far the fog has thickened past the Fog kind's (RainFog)

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
                case Kind.RainFog: tc = 1f; tf = 1f; tr = 1f; break;
            }
            float sk = snap > 0 ? 10000f : 1f;   // SetNow: straight there
            if (snap > 0) snap--;
            Cloud = Mathf.MoveTowards(Cloud, tc, dt * 0.05f * sk);
            Fog = Mathf.MoveTowards(Fog, tf, dt * 0.06f * sk);
            Rain = Mathf.MoveTowards(Rain, tr, dt * 0.08f * sk);
            heavy = Mathf.MoveTowards(heavy, current == Kind.RainFog ? 1f : 0f, dt * 0.06f * sk);
            float to = Mathf.Min(OvercastCap, current == Kind.Cloudy ? 0.55f : current == Kind.Fog ? 0.8f : current == Kind.Rain || current == Kind.RainFog ? 1f : 0f);
            Overcast = Mathf.MoveTowards(Overcast, to, dt * 0.06f * sk);
            Shader.SetGlobalFloat(CloudId, Cloud);
            // the wind on trees, grass and cloth (Shaders/NtWind.hlsl): a breeze in fair weather, stronger with cloud, rain,
            // the storm; gusts come and go
            float baseWind = current == Kind.RainFog ? 1f : current == Kind.Rain ? 0.8f : current == Kind.Cloudy ? 0.5f : current == Kind.Fog ? 0.25f : 0.35f;
            wind = Mathf.MoveTowards(wind, baseWind, dt * 0.05f);
            float gusts = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 0.07f, 3.1f) * 2f;
            Shader.SetGlobalFloat(WindId, wind * gusts);

            if (baseFogEnd < 0f) { baseFogStart = RenderSettings.fogStartDistance; baseFogEnd = RenderSettings.fogEndDistance; }
            RenderSettings.fogStartDistance = Mathf.Lerp(baseFogStart, Mathf.Lerp(5f, 1.5f, heavy), Fog);
            RenderSettings.fogEndDistance = Mathf.Lerp(baseFogEnd, Mathf.Lerp(280f, 120f, heavy), Fog);

            var cam = Camera.main;
            if (rain && cam)
            {
                rain.transform.position = cam.transform.position + Vector3.up * 14f + cam.transform.forward * 8f;
                var em = rain.emission;
                em.rateOverTime = Rain * (Application.isMobilePlatform ? 2600f : 7500f);
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
            // the sun through the cloud: next to nothing in rain (8%), a weak disc in fog (DayNightCycle also greys the light)
            if (dn) dn.weatherDim = Mathf.Lerp(1f, 0.08f, Overcast);
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

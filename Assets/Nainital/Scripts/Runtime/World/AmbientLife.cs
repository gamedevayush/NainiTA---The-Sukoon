using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The small moving things that make the town feel alive (owner, 23 Nov 2026: "game feels alive... low cost, without
    /// lag"): leaves falling near trees, dust in the sun, morning mist over the lake, breath fog in the cold, steam from
    /// tea and momo stalls, wood smoke from chimneys. Six particle systems in world space, fed by Emit near the camera
    /// only: nothing is simulated far away, a few hundred particles at most.
    /// The world builder (WorldBuilder.Festive.cs FestiveStep) places this with the steam and chimney spots and the
    /// particle materials.
    /// </summary>
    public class AmbientLife : MonoBehaviour
    {
        public Vector3[] steam = new Vector3[0];
        public Vector3[] chimneys = new Vector3[0];
        public Material softMat, leafMat, sparkMat;

        ParticleSystem leaves, dust, mist, breath, steamPS, smoke;
        static readonly Vector3 WindDir = new Vector3(0.83f, 0f, 0.55f);   // the valley's wind (Shaders/NtWind.hlsl)
        float leafAcc, dustAcc, mistAcc, nearTimer, npcTimer;
        readonly List<int> nearSteam = new List<int>(), nearSmoke = new List<int>();
        readonly float[] steamAcc = new float[64], smokeAcc = new float[64];
        readonly List<NpcWalker> npcs = new List<NpcWalker>();
        readonly Dictionary<Transform, float> breathNext = new Dictionary<Transform, float>();
        static readonly Color[] LeafColors =
        {
            new Color(0.55f, 0.36f, 0.16f), new Color(0.72f, 0.55f, 0.2f), new Color(0.45f, 0.5f, 0.22f),
            new Color(0.62f, 0.3f, 0.12f), new Color(0.35f, 0.42f, 0.18f)
        };

        void Start()
        {
            if (!softMat) { enabled = false; return; }
            leaves = Make("Falling leaves", leafMat ? leafMat : softMat, 160, 0.4f, 1f, 0.1f, 0.15f, true);
            dust = Make("Dust in the sun", sparkMat ? sparkMat : softMat, 80, 0.3f, 1f, 0.3f, 0.3f, false);
            mist = Make("Lake mist", softMat, 60, 0.6f, 1.4f, 0.3f, 0.35f, false);
            breath = Make("Breath fog", softMat, 80, 0.3f, 3f, 0.05f, 0.6f, false);
            steamPS = Make("Steam", softMat, 200, 0.3f, 1.2f, 0.08f, 0.55f, false);
            smoke = Make("Chimney smoke", softMat, 200, 0.25f, 1.3f, 0.1f, 0.6f, false);
            // leaves flutter on their way down
            var n = leaves.noise;
            n.enabled = true; n.strength = 0.7f; n.frequency = 0.45f; n.scrollSpeed = 0.3f; n.quality = ParticleSystemNoiseQuality.Low;
            var dn = dust.noise;
            dn.enabled = true; dn.strength = 0.15f; dn.frequency = 0.3f; dn.quality = ParticleSystemNoiseQuality.Low;
        }

        /// <summary>A world-space system with no emission of its own (fed by Emit): size grows from start*s0 to start*s1,
        /// fades in over the first fadeIn and out over the last fadeOut of its life.</summary>
        ParticleSystem Make(string n, Material mat, int max, float s0, float s1, float fadeIn, float fadeOut, bool spin)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, s0, 1f, s1));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, 1f - fadeOut), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            if (spin)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = spin ? 0f : 10f;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, float size, float life, Color c, float rot = 0f)
        {
            var e = new ParticleSystem.EmitParams
            {
                position = p, velocity = v, startSize = size, startLifetime = life, startColor = c,
                rotation = rot, applyShapeToPosition = false
            };
            ps.Emit(e, 1);
        }

        void Update()
        {
            var cam = Camera.main;
            if (!cam || !leaves) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 c = cam.transform.position;
            var dn = DayNightCycle.Instance;
            var ws = WeatherSystem.Instance;
            float night = dn ? dn.NightFactor : 0f, hour = dn ? dn.timeOfDay : 12f;
            float wind = ws ? ws.Wind : 0.35f, rain = ws ? ws.Rain : 0f, fog = ws ? ws.Fog : 0f, cloud = ws ? ws.Cloud : 0.3f;
            float light = Mathf.Lerp(1f, 0.16f, night);   // the particles are unlit: dimmed at night
            bool inside = InteriorZone.CameraInside;
            float town = AudioManager.Instance ? AudioManager.Instance.Town : 0f;
            Vector3 drift = WindDir * (0.4f + wind * 2f);

            // ---- falling leaves (fewer in the bazaar, none indoors or in heavy rain)
            if (!inside)
            {
                leafAcc += dt * (1.5f + wind * 6f) * (1f - 0.6f * town) * (1f - 0.7f * rain);
                while (leafAcc >= 1f)
                {
                    leafAcc -= 1f;
                    Vector3 p = c + new Vector3(Random.Range(-20f, 20f), Random.Range(5f, 12f), Random.Range(-20f, 20f)) - drift * 2f;
                    Color lc = LeafColors[Random.Range(0, LeafColors.Length)] * light;
                    lc.a = 1f;
                    Emit(leaves, p, drift * 0.7f + Vector3.down * Random.Range(0.6f, 1.1f), Random.Range(0.07f, 0.12f), 9f, lc, Random.Range(0f, 360f));
                }
            }

            // ---- dust in the sunlight: by day, clear skies
            float sun = (1f - night) * Mathf.Clamp01(1f - cloud * 1.3f) * (1f - rain);
            if (!inside && sun > 0.1f)
            {
                dustAcc += dt * 8f * sun;
                while (dustAcc >= 1f)
                {
                    dustAcc -= 1f;
                    Vector3 p = c + cam.transform.forward * Random.Range(2f, 9f) + Random.insideUnitSphere * 4f;
                    Emit(dust, p, drift * 0.08f + Vector3.up * 0.03f, Random.Range(0.025f, 0.045f), 6f, new Color(1f, 0.93f, 0.75f, 0.35f * sun));
                }
            }

            // ---- mist over the lake: mornings, and any time in fog
            float morning = Mathf.Clamp01(1f - Mathf.Abs(hour - 7f) / 2.5f);
            float misty = Mathf.Max(morning * (1f - rain * 0.5f), fog * 0.8f);
            if (misty > 0.05f && LakeArea.Instance)
            {
                mistAcc += dt * 2.2f * misty;
                while (mistAcc >= 1f)
                {
                    mistAcc -= 1f;
                    Vector2 r = Random.insideUnitCircle * 260f;
                    Vector3 p = new Vector3(c.x + r.x, LakeArea.WaterY + Random.Range(1f, 3.5f), c.z + r.y);
                    if (!LakeArea.Contains(p)) continue;
                    float g = Mathf.Lerp(0.95f, 0.4f, night);
                    Emit(mist, p, WindDir * (0.3f + wind * 0.5f), Random.Range(14f, 28f), 26f, new Color(g, g, g * 1.02f, 0.13f * misty));
                }
            }

            // ---- breath fog: the player and people near the camera, in the cold
            float cold = Mathf.Max(Mathf.Max(night, morning), Mathf.Max(fog, rain * 0.8f));
            npcTimer -= dt;
            if (npcTimer <= 0f)
            {
                npcTimer = 2f;
                npcs.Clear();
                foreach (var w in Object.FindObjectsByType<NpcWalker>(FindObjectsSortMode.None))
                    if (w && !w.InCar && (w.transform.position - c).sqrMagnitude < 16f * 16f) { npcs.Add(w); if (npcs.Count >= 8) break; }
            }
            if (cold > 0.25f && !inside)
            {
                var pl = PlayerController.Instance;
                if (pl && pl.Rig && pl.Rig.Head && !CarController.Driving) Breathe(pl.Rig.Head, pl.transform.forward, pl.Rig.speed > 4f, cold, light);
                foreach (var w in npcs)
                {
                    if (!w) continue;
                    var rig = w.GetComponentInChildren<HumanoidRig>();
                    if (rig && rig.Head && !rig.dead) Breathe(rig.Head, w.transform.forward, rig.speed > 3f, cold, light);
                }
            }

            // ---- steam and chimney smoke: the nearest spots only
            nearTimer -= dt;
            if (nearTimer <= 0f)
            {
                nearTimer = 1f;
                Nearest(steam, c, 40f, nearSteam, 12);
                Nearest(chimneys, c, 180f, nearSmoke, 16);
            }
            bool stallsOpen = hour > 6.5f && hour < 22.5f;
            if (stallsOpen)
                for (int k = 0; k < nearSteam.Count; k++)
                {
                    steamAcc[k] += dt * 3.5f * (1f - rain * 0.5f);
                    while (steamAcc[k] >= 1f)
                    {
                        steamAcc[k] -= 1f;
                        Vector3 p = steam[nearSteam[k]] + Random.insideUnitSphere * 0.08f;
                        float g = Mathf.Lerp(1f, 0.5f, night);
                        Emit(steamPS, p, Vector3.up * Random.Range(0.35f, 0.6f) + drift * 0.25f, Random.Range(0.35f, 0.6f), 2.2f, new Color(g, g, g, 0.22f));
                    }
                }
            float fires = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(hour - 7f) / 2f), Mathf.Clamp01(1f - Mathf.Abs(hour - 20f) / 3f), cold * 0.6f);
            if (fires > 0.1f)
                for (int k = 0; k < nearSmoke.Count; k++)
                {
                    smokeAcc[k] += dt * 1.3f * fires;
                    while (smokeAcc[k] >= 1f)
                    {
                        smokeAcc[k] -= 1f;
                        Vector3 p = chimneys[nearSmoke[k]] + Random.insideUnitSphere * 0.15f;
                        float g = Mathf.Lerp(0.6f, 0.25f, night);
                        Emit(smoke, p, Vector3.up * Random.Range(0.6f, 0.9f) + drift * 0.6f, Random.Range(1.2f, 2f), 9f, new Color(g, g, g * 1.03f, 0.3f));
                    }
                }
        }

        void Breathe(Transform head, Vector3 fwd, bool hard, float cold, float light)
        {
            float t = Time.time;
            if (breathNext.TryGetValue(head, out float next) && t < next) return;
            breathNext[head] = t + (hard ? Random.Range(0.9f, 1.3f) : Random.Range(2.6f, 3.6f));
            if (breathNext.Count > 64) breathNext.Clear();
            Vector3 p = head.position + fwd * 0.16f + Vector3.down * 0.06f;
            float g = Mathf.Lerp(1f, 0.55f, 1f - light);
            Emit(breath, p, fwd * 0.4f + Vector3.up * 0.06f, 0.14f, 1.4f, new Color(g, g, g, 0.24f * Mathf.Clamp01(cold * 1.5f)));
        }

        static void Nearest(Vector3[] spots, Vector3 c, float within, List<int> into, int max)
        {
            into.Clear();
            if (spots == null) return;
            float lim = within * within;
            for (int i = 0; i < spots.Length && into.Count < max; i++)
                if ((spots[i] - c).sqrMagnitude < lim) into.Add(i);
        }
    }
}

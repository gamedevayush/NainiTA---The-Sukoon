using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The red and blue light bar on the roof of a police vehicle or the 108 ambulance, and its siren (user,
    /// 2026-11-18: "police or ambulance me siren and top lights hongi"). Red and blue lenses flash in turn (two quick
    /// flashes each); after dusk a small coloured light washes the road round the vehicle when the camera is near.
    /// The siren is a real recording (Mixkit, Tools/fetch_sounds.py: siren_police, siren_ambulance) looped in 3D.
    /// TrafficCar looks at <see cref="All"/>: cars ahead of a siren pull over to the left and let it pass.
    /// </summary>
    public class EmergencyLights : MonoBehaviour
    {
        public static readonly List<EmergencyLights> All = new List<EmergencyLights>();

        public bool lights = true, siren = true;
        public bool police = true;
        Renderer red, blue;
        Light glow;
        AudioSource src;
        float t, checkTimer;
        bool glowAllowed;

        public bool Sounding => siren && src && src.isPlaying;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Put a light bar on this vehicle's roof (model space point on top of the roof, roof width in metres).</summary>
        public static EmergencyLights Fit(GameObject vehicle, Vector3 roof, float roofWidth, bool police)
        {
            var e = vehicle.AddComponent<EmergencyLights>();
            e.police = police;
            e.Build(roof, roofWidth);
            return e;
        }

        void Build(Vector3 roof, float roofWidth)
        {
            var kit = PoliceKit.Instance;
            var bar = new GameObject("LightBar") { layer = gameObject.layer };
            bar.transform.SetParent(transform, false);
            bar.transform.localPosition = roof;
            float w = Mathf.Clamp(roofWidth * 0.82f, 0.35f, 1.35f), d = 0.3f, h = 0.12f;

            // housing: a black base on two feet, a dark lens bed (seen when a lens is off), a white middle block
            var mb = new MeshBuilder();
            Vector2 black = Atlas.ExtraUV(Px.Tyre), dark = Atlas.ExtraUV(Px.CarGlass), white = Atlas.ExtraUV(Px.CarWhite);
            mb.Box(new Vector3(-w * 0.32f, 0.02f, 0f), new Vector3(0.06f, 0.04f, d * 0.8f), black);
            mb.Box(new Vector3(w * 0.32f, 0.02f, 0f), new Vector3(0.06f, 0.04f, d * 0.8f), black);
            mb.Box(new Vector3(0f, 0.06f, 0f), new Vector3(w, 0.04f, d), black);
            mb.Box(new Vector3(0f, 0.08f + h * 0.5f, 0f), new Vector3(w - 0.02f, h - 0.01f, d - 0.03f), dark);
            mb.Box(new Vector3(0f, 0.08f + h * 0.5f, 0f), new Vector3(w * 0.12f, h + 0.005f, d + 0.005f), white);
            var housing = new GameObject("Housing") { layer = gameObject.layer };
            housing.transform.SetParent(bar.transform, false);
            housing.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("LightBarHousing");
            var hr = housing.AddComponent<MeshRenderer>();
            hr.sharedMaterial = GameAssets.AtlasMaterial;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            red = Lens(bar.transform, "Red", -1f, w, d, h, kit ? kit.lampRed : null);
            blue = Lens(bar.transform, "Blue", 1f, w, d, h, kit ? kit.lampBlue : null);

            var lg = new GameObject("Glow");
            lg.transform.SetParent(bar.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            glow = lg.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 11f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;
            glow.enabled = false;

            var clip = Resources.Load<AudioClip>(police ? "Audio/siren_police" : "Audio/siren_ambulance");
            if (clip)
            {
                src = bar.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.minDistance = 9f;
                src.maxDistance = 420f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.dopplerLevel = 0.5f;
                src.time = Random.Range(0f, clip.length * 0.9f);
            }
            t = Random.value;
        }

        static Renderer Lens(Transform bar, string name, float side, float w, float d, float h, Material m)
        {
            var mb = new MeshBuilder();
            mb.Box(new Vector3(side * w * 0.27f, 0.08f + h * 0.5f, 0f), new Vector3(w * 0.4f, h, d), Vector2.zero);
            var go = new GameObject(name) { layer = bar.gameObject.layer };
            go.transform.SetParent(bar, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Lens" + name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m ? m : GameAssets.AtlasMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.enabled = false;
            return r;
        }

        /// <summary>A story vehicle's bar: flashes on the real clock while the world is frozen (StoryDirector).</summary>
        [System.NonSerialized] public bool storyTime;

        void Update()
        {
            float dt = storyTime ? Time.unscaledDeltaTime : Time.deltaTime;
            checkTimer -= dt;
            if (checkTimer <= 0f)
            {
                checkTimer = 0.5f;
                var cam = Camera.main;
                var dn = DayNightCycle.Instance;
                float night = dn ? dn.NightFactor : 0f;
                glowAllowed = lights && night > 0.2f && cam && (cam.transform.position - transform.position).sqrMagnitude < 60f * 60f;
                if (src)
                {
                    float vol = 0.85f * (AudioManager.Instance ? AudioManager.Instance.masterVolume : 0.8f);
                    src.volume = vol;
                    if (siren && !src.isPlaying) src.Play();
                    else if (!siren && src.isPlaying) src.Stop();
                }
            }
            if (!lights)
            {
                if (red && red.enabled) red.enabled = false;
                if (blue && blue.enabled) blue.enabled = false;
                if (glow && glow.enabled) glow.enabled = false;
                return;
            }
            // red: two quick flashes, then blue: two quick flashes (0.9 s round)
            t += dt / 0.9f;
            float u = t - Mathf.Floor(t);
            bool redOn = (u < 0.1f) || (u > 0.15f && u < 0.25f);
            bool blueOn = (u > 0.5f && u < 0.6f) || (u > 0.65f && u < 0.75f);
            if (red && red.enabled != redOn) red.enabled = redOn;
            if (blue && blue.enabled != blueOn) blue.enabled = blueOn;
            if (glow)
            {
                bool on = glowAllowed && (redOn || blueOn);
                if (glow.enabled != on) glow.enabled = on;
                if (on)
                {
                    glow.color = redOn ? new Color(1f, 0.12f, 0.1f) : new Color(0.15f, 0.3f, 1f);
                    glow.intensity = 2.2f;
                }
            }
        }
    }
}

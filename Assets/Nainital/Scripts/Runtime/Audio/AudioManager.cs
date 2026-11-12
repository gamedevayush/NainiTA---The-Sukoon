using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The sound of the town, from real recordings (Resources/Audio, made by Tools/fetch_sounds.py; user, 2026-11-11:
    /// "real ambience, footsteps, SFX, people, market"). Synthesised sounds (AudioSynth) stand in for any missing file.
    ///
    /// Beds (2D loops whose volume follows where the listener is and the hour): birds, quiet forest, wind (louder high up),
    /// crickets at night, the lake shore, light and heavy rain, and the town: bazaar crowds, Mall Road walkers, cafes,
    /// children on the Flats, the bus stand, road traffic. Each town bed fades in near its zones (set by the world builder).
    /// Silent beds are paused, so a phone decodes only what is heard.
    ///
    /// Effects: footsteps by surface (road, stone, grass, forest floor, gravel), landing, punches and kicks, splashes and
    /// swimming, the jetpack, the player's engine, horns and cars passing, dogs, a rooster at dawn, a cow now and then,
    /// birdsong, temple bells, thunder.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public Vector3 templePosition;
        public float masterVolume = 0.8f;

        public enum Zone { Market = 0, Street = 1, Cafe = 2, Kids = 3, BusStand = 4, Traffic = 5 }
        public int[] zoneKinds = new int[0];
        public Vector3[] zonePositions = new Vector3[0];
        public float[] zoneRadii = new float[0];

        public AudioClip engineClip { get; private set; }

        class Bed { public AudioSource src; public float target; }
        readonly Dictionary<string, Bed> beds = new Dictionary<string, Bed>();
        readonly Dictionary<string, AudioClip[]> sets = new Dictionary<string, AudioClip[]>();
        AudioSource sEngine, sJet, sSwim, s2D;
        float birdTimer = 3f, bellTimer = 5f, dogTimer = 20f, roosterTimer = 10f, cowTimer = 90f, stepTimer, lakeTimer, zoneTimer, airTime;
        float lakeDist = 999f;
        readonly float[] zoneNear = new float[6];
        bool wasFlying, wasGrounded = true;
        int lastStep = -1;

        void Awake() => Instance = this;

        static AudioClip Load(string n) => Resources.Load<AudioClip>("Audio/" + n);

        readonly Dictionary<string, AudioClip> named = new Dictionary<string, AudioClip>();
        /// <summary>A clip from Resources/Audio by file name (crash, fire, explosion...), loaded once.</summary>
        public AudioClip Clip(string n)
        {
            if (!named.TryGetValue(n, out var c)) { c = Load(n); named[n] = c; }
            return c;
        }

        AudioClip[] Set(string key, params string[] names)
        {
            var list = new List<AudioClip>();
            foreach (var n in names) { var c = Load(n); if (c) list.Add(c); }
            sets[key] = list.ToArray();
            return sets[key];
        }

        AudioClip One(string key)
        {
            if (!sets.TryGetValue(key, out var a) || a.Length == 0) return null;
            return a[Random.Range(0, a.Length)];
        }

        AudioSource Loop(AudioClip c, float spatial = 0f, string child = null)
        {
            var host = gameObject;
            if (child != null) { host = new GameObject(child); host.transform.SetParent(transform, false); }
            var s = host.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.volume = 0f;
            s.spatialBlend = spatial;
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            return s;
        }

        void AddBed(string name, AudioClip fallback = null)
        {
            var c = Load(name) ?? fallback;
            if (!c) return;
            var s = Loop(c);
            s.time = Random.Range(0f, c.length * 0.9f);   // two beds of the same file never start together
            beds[name] = new Bed { src = s };
        }

        void Start()
        {
            foreach (var n in new[] { "amb_birds_day", "amb_birds_morning", "amb_forest", "amb_wind_high", "amb_night", "amb_night_forest",
                                      "amb_rain_heavy", "amb_traffic", "amb_street", "amb_market", "amb_fair", "amb_cafe", "amb_kids", "amb_busstand" })
                AddBed(n);
            AddBed("amb_wind", AudioSynth.Wind());
            AddBed("amb_lake", AudioSynth.Water());
            AddBed("amb_rain_light", AudioSynth.Rain());
            if (!beds.ContainsKey("amb_night")) AddBed("amb_night_synth", AudioSynth.Crickets());

            Set("step_road", "step_road_0", "step_road_1", "step_road_2", "step_road_3", "step_road_4", "step_road_5");
            foreach (var s in new[] { "stone", "grass", "forest", "gravel" })
                Set("step_" + s, $"step_{s}_0", $"step_{s}_1", $"step_{s}_2", $"step_{s}_3", $"step_{s}_4", $"step_{s}_5");
            Set("punch", "punch_1", "punch_2", "punch_3", "punch_4", "punch_5", "kick_1");
            Set("block", "block_1");
            Set("whoosh", "whoosh_1", "whoosh_2");
            Set("splash", "splash", "jump_water");
            Set("horn", "horn_1", "horn_2", "horn_3", "horn_4");
            Set("hornBig", "horn_truck");
            Set("pass", "car_pass", "bike_pass", "scooter_pass");
            Set("dog", "dog_1", "dog_2", "dog_3");
            Set("dogPack", "dog_pack");
            Set("rooster", "rooster");
            Set("cow", "cow");
            Set("bird", "bird_1", "bird_2", "bird_3");
            Set("thunder", "thunder_1", "thunder_2", "thunder_3");
            Set("bell", "temple_bell");
            Set("jetStart", "jet_start");
            Set("click", "click");
            Set("carStart", "car_start");
            Set("carDoor", "car_door");
            // synthesised stand-ins for anything missing
            if (sets["step_road"].Length == 0) sets["step_road"] = new[] { AudioSynth.Step() };
            if (sets["punch"].Length == 0) sets["punch"] = new[] { AudioSynth.Punch() };
            if (sets["splash"].Length == 0) sets["splash"] = new[] { AudioSynth.Splash() };
            if (sets["horn"].Length == 0) sets["horn"] = new[] { AudioSynth.Horn() };
            if (sets["bell"].Length == 0) sets["bell"] = new[] { AudioSynth.Bell() };
            if (sets["thunder"].Length == 0) sets["thunder"] = new[] { AudioSynth.Thunder() };
            if (sets["bird"].Length == 0) sets["bird"] = new[] { AudioSynth.Bird(), AudioSynth.Bird() };
            Set("jingle");
            sets["jingle"] = new[] { AudioSynth.Jingle() };

            s2D = gameObject.AddComponent<AudioSource>();
            s2D.playOnAwake = false;
            engineClip = Load("loop_engine") ?? AudioSynth.Engine();
            sEngine = Loop(engineClip, 0.6f, "EngineAudio");
            sJet = Loop(Load("loop_jet") ?? AudioSynth.Wind(), 0.3f, "JetpackAudio");
            sSwim = Loop(Load("loop_swim") ?? AudioSynth.Water(), 0.3f, "SwimAudio");

            GameEvents.Hit += (p, blocked) => PlayAt(One(blocked ? "block" : "punch") ?? One("punch"), p, blocked ? 0.5f : 0.9f, Random.Range(0.92f, 1.08f));
            GameEvents.Splash += p => PlayAt(One("splash"), p, 0.9f, Random.Range(0.95f, 1.05f));
            GameEvents.Horn += p => PlayAt(One("horn"), p, 0.9f, 1f);
            GameEvents.Banner += (t, s, c) => { if (t.Contains("PASSED")) s2D.PlayOneShot(One("jingle"), 0.7f * masterVolume); };
        }

        public void PlayAt(AudioClip c, Vector3 pos, float vol, float pitch, float maxDistance = 120f)
        {
            if (!c) return;
            var go = new GameObject("SFX");
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = c;
            s.volume = vol * masterVolume;
            s.pitch = pitch;
            s.spatialBlend = 0.85f;
            s.minDistance = 3f;
            s.maxDistance = maxDistance;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.dopplerLevel = 0f;
            s.Play();
            Destroy(go, c.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }

        public void Thunder() => s2D.PlayOneShot(One("thunder"), 0.9f * masterVolume);

        /// <summary>A traffic vehicle sounds its horn (hill roads: at blind bends and when blocked).</summary>
        public void HornAt(Vector3 p, bool big) => PlayAt(One(big ? "hornBig" : "horn") ?? One("horn"), p, 0.75f, Random.Range(0.95f, 1.05f), 160f);

        /// <summary>A traffic vehicle goes past the listener.</summary>
        public void PassBy(Vector3 p, float speed) => PlayAt(One("pass"), p, Mathf.Clamp01(speed / 12f) * 0.7f, Random.Range(0.9f, 1.1f), 60f);

        /// <summary>Getting into a car: the door shuts, then the engine turns over.</summary>
        public void CarEnter(Vector3 p)
        {
            PlayAt(One("carDoor"), p, 0.7f, 1f, 40f);
            var s = One("carStart");
            if (s) StartCoroutine(Later(0.7f, () => PlayAt(s, p, 0.75f, 1f, 50f)));
        }

        /// <summary>Getting out: the door.</summary>
        public void CarExit(Vector3 p) => PlayAt(One("carDoor"), p, 0.7f, 1.05f, 40f);

        static System.Collections.IEnumerator Later(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }

        /// <summary>A tap on a phone button.</summary>
        public void Click() { var c = One("click"); if (c) s2D.PlayOneShot(c, 0.35f * masterVolume); }

        void SetBed(string n, float v) { if (beds.TryGetValue(n, out var b)) b.target = Mathf.Max(0f, v); }

        void UpdateZones(Vector3 listener)
        {
            for (int k = 0; k < zoneNear.Length; k++) zoneNear[k] = 0f;
            if (zonePositions == null) return;
            int n = Mathf.Min(zonePositions.Length, Mathf.Min(zoneKinds.Length, zoneRadii.Length));
            for (int i = 0; i < n; i++)
            {
                int k = zoneKinds[i];
                if (k < 0 || k >= zoneNear.Length) continue;
                float r = zoneRadii[i];
                Vector3 d = zonePositions[i] - listener;
                d.y *= 0.5f;   // a street just below the hillside still sounds near
                float m = d.sqrMagnitude;
                if (m > r * r) continue;
                float near = 1f - Mathf.Sqrt(m) / r;
                near = near * near * (3f - 2f * near);
                if (near > zoneNear[k]) zoneNear[k] = near;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = Camera.main;
            var p = PlayerController.Instance;
            var dn = DayNightCycle.Instance;
            float night = dn ? dn.NightFactor : 0f;
            float hour = dn ? dn.timeOfDay : 12f;
            float rain = WeatherSystem.Instance ? WeatherSystem.Instance.Rain : 0f;
            Vector3 listener = cam ? cam.transform.position : Vector3.zero;
            float m = masterVolume;

            lakeTimer -= dt;
            if (lakeTimer <= 0f && LakeArea.Instance && LakeArea.Instance.outline != null)
            {
                lakeTimer = 0.5f;
                lakeDist = LakeArea.Contains(listener) ? 0f : Geo.DistanceToPolyline(LakeArea.Instance.outline, new Vector2(listener.x, listener.z), true);
            }
            zoneTimer -= dt;
            if (zoneTimer <= 0f) { zoneTimer = 0.25f; UpdateZones(listener); }

            float market = zoneNear[(int)Zone.Market], street = zoneNear[(int)Zone.Street], cafe = zoneNear[(int)Zone.Cafe];
            float kids = zoneNear[(int)Zone.Kids], bus = zoneNear[(int)Zone.BusStand], traffic = zoneNear[(int)Zone.Traffic];
            float town = Mathf.Max(Mathf.Max(market, street), Mathf.Max(traffic * 0.8f, bus));
            float elevation = LakeArea.Instance ? Mathf.Clamp01((listener.y - LakeArea.WaterY) / 400f) : 0f;
            float day = 1f - night, dry = 1f - rain;
            bool morning = hour > 5f && hour < 9.5f;
            float evening = hour >= 17f && hour < 22.5f ? 1f : (hour >= 9f && hour < 17f ? 0.7f : 0.15f);   // the Mall fills up after dark
            float open = hour >= 8.5f && hour < 22f ? 1f : 0.2f;

            SetBed("amb_birds_day", 0.5f * day * dry * (morning ? 0.3f : 1f) * (1f - 0.65f * town));
            SetBed("amb_birds_morning", 0.55f * day * dry * (morning ? 1f : 0f) * (1f - 0.5f * town));
            SetBed("amb_forest", 0.45f * day * dry * (1f - town));
            SetBed("amb_wind", 0.18f + elevation * 0.3f + rain * 0.12f);
            SetBed("amb_wind_high", Mathf.Clamp01(elevation * 2f - 0.6f) * 0.45f);
            SetBed("amb_night", 0.42f * night * dry * (1f - 0.6f * town));
            SetBed("amb_night_synth", 0.28f * night * dry);
            SetBed("amb_night_forest", 0.35f * night * dry * (1f - town));
            SetBed("amb_lake", 0.6f * Mathf.Clamp01(1f - lakeDist / 60f));
            SetBed("amb_rain_light", 0.75f * Mathf.Clamp01(rain * 2f) * (1f - Mathf.Clamp01(rain * 2f - 1f) * 0.5f));
            SetBed("amb_rain_heavy", 0.8f * Mathf.Clamp01(rain * 2f - 1f));
            float crowd = (1f - 0.5f * rain);
            SetBed("amb_market", 0.85f * market * open * crowd);
            SetBed("amb_fair", 0.45f * market * open * crowd * evening);
            SetBed("amb_street", 0.75f * street * evening * crowd);
            SetBed("amb_cafe", 0.6f * cafe * open);
            SetBed("amb_kids", 0.6f * kids * (hour > 8f && hour < 18.5f ? 1f : 0f) * dry);
            SetBed("amb_busstand", 0.75f * bus * (hour > 5f && hour < 22.5f ? 1f : 0.1f));
            SetBed("amb_traffic", 0.55f * traffic * (0.35f + 0.65f * day));

            foreach (var b in beds.Values)
            {
                float v = b.target * m;
                b.src.volume = Mathf.MoveTowards(b.src.volume, v, dt * 0.35f);
                if (b.src.volume < 0.003f && v < 0.003f) { if (b.src.isPlaying) b.src.Pause(); }
                else if (!b.src.isPlaying) { if (b.src.time > 0f) b.src.UnPause(); else b.src.Play(); }
            }

            Occasional(dt, listener, night, hour, rain, town);
            PlayerSounds(dt, p, m);
        }

        void Occasional(float dt, Vector3 listener, float night, float hour, float rain, float town)
        {
            Vector3 Around(float near, float far)
            {
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(near, far);
                return listener + new Vector3(c.x, Random.Range(-6f, 4f), c.y);
            }
            birdTimer -= dt;
            if (birdTimer <= 0f)
            {
                birdTimer = Random.Range(3f, 9f);
                if (night < 0.4f && rain < 0.5f) PlayAt(One("bird"), listener + Random.insideUnitSphere * 25f + Vector3.up * 8f, 0.22f * (1f - 0.6f * town), Random.Range(0.92f, 1.1f));
            }
            bellTimer -= dt;
            if (bellTimer <= 0f)
            {
                bellTimer = Random.Range(6f, 15f);
                if ((templePosition - listener).sqrMagnitude < 110f * 110f) PlayAt(One("bell"), templePosition + Vector3.up * 3f, 0.7f, Random.Range(0.96f, 1.04f), 140f);
            }
            dogTimer -= dt;   // street dogs: a few barks by day, more at night, now and then a whole pack
            if (dogTimer <= 0f)
            {
                dogTimer = night > 0.5f ? Random.Range(14f, 40f) : Random.Range(35f, 90f);
                bool pack = night > 0.5f && Random.value < 0.15f;
                PlayAt(One(pack ? "dogPack" : "dog"), Around(30f, 80f), pack ? 0.45f : 0.5f, Random.Range(0.9f, 1.1f), 150f);
            }
            roosterTimer -= dt;
            if (roosterTimer <= 0f)
            {
                roosterTimer = Random.Range(35f, 80f);
                if (hour > 5f && hour < 7.8f) PlayAt(One("rooster"), Around(50f, 110f), 0.45f, Random.Range(0.95f, 1.05f), 200f);
            }
            cowTimer -= dt;
            if (cowTimer <= 0f)
            {
                cowTimer = Random.Range(90f, 220f);
                if (night < 0.3f && town < 0.5f) PlayAt(One("cow"), Around(40f, 90f), 0.4f, Random.Range(0.95f, 1.05f), 150f);
            }
        }

        /// <summary>Which recording a footstep at this point uses: the terrain's main texture there, or the kind of
        /// thing the collider is (roads, paving, decks).</summary>
        string Surface(Vector3 pos)
        {
            if (!Physics.Raycast(pos + Vector3.up * 0.6f, Vector3.down, out var hit, 1.6f, ~((1 << 8) | (1 << 9) | (1 << 2)), QueryTriggerInteraction.Ignore))
                return "step_road";
            if (hit.collider is TerrainCollider tc)
            {
                var td = tc.terrainData;
                var t = tc.GetComponent<Terrain>();
                if (td && t && td.alphamapLayers > 0)
                {
                    Vector3 lp = hit.point - t.transform.position;
                    int x = Mathf.Clamp(Mathf.FloorToInt(lp.x / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
                    int z = Mathf.Clamp(Mathf.FloorToInt(lp.z / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
                    var w = td.GetAlphamaps(x, z, 1, 1);
                    int best = 0;
                    for (int l = 1; l < td.alphamapLayers; l++) if (w[0, 0, l] > w[0, 0, best]) best = l;
                    switch (best)   // TL_Grass, TL_ForestFloor, TL_Rock, TL_Dirt
                    {
                        case 0: return "step_grass";
                        case 1: return "step_forest";
                        case 2: return "step_stone";
                        default: return "step_gravel";
                    }
                }
                return "step_grass";
            }
            string n = hit.collider.name;
            if (n.Contains("Road") || n.Contains("Junction") || n.Contains("Mall")) return "step_road";
            if (n.Contains("Path") || n.Contains("Track") || n.Contains("Trail")) return "step_gravel";
            return "step_stone";
        }

        void PlayerSounds(float dt, PlayerController p, float m)
        {
            if (p && p.CurrentMode == PlayerController.Mode.Normal && !p.InWater && p.Rig)
            {
                float sp = p.Rig.speed;
                bool grounded = p.Rig.grounded;
                if (!grounded) airTime += dt;
                else
                {
                    if (!wasGrounded && airTime > 0.35f)   // landing
                    {
                        var c = One(Surface(p.transform.position)) ?? One("step_road");
                        PlayAt(c, p.transform.position, Mathf.Clamp(0.45f + airTime * 0.15f, 0.45f, 0.85f), 0.8f, 40f);
                    }
                    airTime = 0f;
                }
                wasGrounded = grounded;
                if (sp > 0.6f && grounded)
                {
                    stepTimer -= dt;
                    if (stepTimer <= 0f)
                    {
                        stepTimer = Mathf.Clamp(0.6f - sp * 0.055f, 0.25f, 0.58f);
                        var set = sets.TryGetValue(Surface(p.transform.position), out var a) && a.Length > 0 ? a : sets["step_road"];
                        int k = Random.Range(0, set.Length);
                        if (k == lastStep && set.Length > 1) k = (k + 1) % set.Length;   // never the same step twice running
                        lastStep = k;
                        PlayAt(set[k], p.transform.position, Mathf.Lerp(0.32f, 0.55f, Mathf.InverseLerp(1.5f, 6f, sp)), Random.Range(0.92f, 1.08f), 35f);
                    }
                }
            }
            else wasGrounded = true;

            // swimming
            float swim = p && p.InWater && p.Rig && p.Rig.speed > 0.3f ? 0.55f : 0f;
            sSwim.transform.position = p ? p.transform.position : Vector3.zero;
            Fade(sSwim, swim * m, dt * 2f);

            // jetpack
            bool flying = Jetpack.Flying;
            if (flying && !wasFlying && p) PlayAt(One("jetStart"), p.transform.position, 0.6f, 1f, 60f);
            wasFlying = flying;
            if (p) sJet.transform.position = p.transform.position;
            sJet.pitch = Mathf.Lerp(sJet.pitch, GameInput.TouchJumpHeld || GameInput.JumpHeld ? 1.12f : 0.95f, dt * 3f);
            Fade(sJet, flying ? 0.55f * m : 0f, dt * 3f);

            // the player's own vehicle
            var car = CarController.Driving;
            if (car)
            {
                sEngine.transform.position = car.transform.position;
                float spd01 = Mathf.Clamp01(car.Speed / 30f);
                float throttle = Mathf.Abs(GameInput.Move.y);
                sEngine.pitch = Mathf.Lerp(sEngine.pitch, 0.75f + spd01 * 1.1f + throttle * 0.2f, dt * 4f);
                Fade(sEngine, (0.35f + throttle * 0.25f) * m, dt * 4f);
            }
            else Fade(sEngine, 0f, dt * 4f);
        }

        static void Fade(AudioSource s, float target, float rate)
        {
            if (!s) return;
            s.volume = Mathf.Lerp(s.volume, target, Mathf.Clamp01(rate));
            if (s.volume < 0.003f && target < 0.003f) { if (s.isPlaying) s.Pause(); }
            else if (!s.isPlaying) { if (s.time > 0f) s.UnPause(); else s.Play(); }
        }
    }
}

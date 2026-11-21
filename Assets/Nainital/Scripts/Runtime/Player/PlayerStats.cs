using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nainital
{
    /// <summary>
    /// The player's stats, GTA San Andreas style (owner, 21 Nov 2026: "all of them: stamina, muscle, fat, driving, bike,
    /// swimming / breath - numbers and their effects only, no body change; stamina also drains while walking"; hunger:
    /// "health drops slowly, and the screen gets more and more blurry"). Six stats 0-100 plus three meters, saved with
    /// PlayerPrefs like GameState:
    ///   Stamina  - how long a sprint lasts (10-45 s); rises by sprinting, running, swimming, the gym.
    ///   Muscle   - punch damage (+60 % at 100), jump (+12 %), how high a bank you can climb out of the lake; the gym.
    ///   Fat      - up with food (more when already full), down with running, swimming, the gym; slows the sprint (-10 %),
    ///              the jump (-8 %) and shortens the sprint (-25 %) at 100. Starving burns it, then muscle.
    ///   Driving  - car grip (0.94-1.08) and steering speed (0.85-1.25); rises by the km driven (CarController).
    ///   Bike     - a two-wheeler's top speed (0.92-1.12); rises by the km ridden (PlayerBike).
    ///   Swimming - swim speed (+40 %) and breath: how long a fast swim lasts (6-36 s); rises by swimming.
    ///   Energy (the stamina bar now) - drains sprinting (fast), running, swimming, rowing and walking (slowly); refills
    ///              standing still and faster sitting (a bench, a vehicle seat). Empty: no sprint until it is back to 25 %.
    ///   Breath  - drains on a fast swim; empty: no fast swim until 35 %.
    ///   Hunger (Hunger01, 1 = full) - full to empty in ~30 min (faster running, sprinting, swimming). Under 30 % health
    ///              stops coming back and starts falling (0.02/s at 30 % to 0.15/s at 0), the screen blurs more and more
    ///              (a runtime URP Volume: Gaussian depth of field, vignette, less colour); at 0 health the player faints.
    ///              Over 30 % health comes back on its own at 0.4-1.2/s (was 3/s), 8 s after the last hit.
    /// Eat(food01, healthAdd) is the one way food comes in (FoodStall, restaurants, shops, the house).
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }

        public enum Stat { Stamina = 0, Muscle = 1, Fat = 2, Driving = 3, Bike = 4, Swimming = 5 }
        public const int StatCount = 6;

        // ---------------------------------------------------------------- tuning
        const float HungerSeconds = 30f * 60f;   // full to empty, standing about (running ~26 min)
        public const float HungryBelow = 0.3f;   // under this: no regen, health falls, the screen blurs
        const float RegenDelay = 8f;

        static readonly string[] Keys = { "nt_stat_stamina", "nt_stat_muscle", "nt_stat_fat", "nt_stat_drive", "nt_stat_bike", "nt_stat_swim" };
        const string HungerKey = "nt_stat_hunger";
        static readonly float[] Starts = { 15f, 10f, 20f, 10f, 10f, 10f };
        // a skill-up toast every 5 points (fat both ways)
        static readonly string[] UpToast = { "Stamina badhi: ", "Muscle badhe: ", "Fat badh gaya: ", "Driving skill badhi: ", "Bike skill badhi: ", "Swimming skill badhi: " };

        static readonly float[] vals = new float[StatCount];
        static readonly int[] announced = new int[StatCount];
        static float hunger = 1f, energy = 1f, breath = 1f;
        static bool loaded, dirty, exhausted, breathOut;

        /// <summary>A gym workout is on (GymStation spends the energy itself; no refill meanwhile).</summary>
        public static bool Working;

        // ---------------------------------------------------------------- read
        public static float Get(Stat s) { if (!loaded) Load(); return vals[(int)s]; }
        public static float Get01(Stat s) => Get(s) * 0.01f;
        /// <summary>The hunger bar, 1 = full, 0 = starving.</summary>
        public static float Hunger01 { get { if (!loaded) Load(); return hunger; } }
        /// <summary>The stamina bar now, 0..1.</summary>
        public static float Energy01 { get { if (!loaded) Load(); return energy; } }
        /// <summary>Breath for a fast swim, 0..1.</summary>
        public static float Breath01 { get { if (!loaded) Load(); return breath; } }
        public static bool Exhausted { get { if (!loaded) Load(); return exhausted; } }
        public static bool BreathOut { get { if (!loaded) Load(); return breathOut; } }
        public static bool Hungry => Hunger01 < HungryBelow;

        static float S01(int i) { if (!loaded) Load(); return vals[i] * 0.01f; }

        // ---------------------------------------------------------------- effects (read by the hooks)
        /// <summary>PlayerController: sprint only with energy left.</summary>
        public static bool CanSprint { get { if (!loaded) Load(); return !exhausted; } }
        /// <summary>PlayerController: sprint speed (fit a little faster, fat a little slower).</summary>
        public static float SprintScale => 1f + 0.05f * S01(0) - 0.10f * S01(2);
        /// <summary>PlayerController: jump height.</summary>
        public static float JumpScale => 1f + 0.12f * S01(1) - 0.08f * S01(2);
        /// <summary>PlayerController: extra metres of bank the player can pull himself up out of the lake.</summary>
        public static float ClimbBonus => 0.6f * S01(1);
        /// <summary>Combatant.damageMultiplier of the player (set here every frame).</summary>
        public static float PunchScale => 1f + 0.6f * S01(1);
        /// <summary>PlayerController: swim speed.</summary>
        public static float SwimScale => 1f + 0.4f * S01(5);
        /// <summary>PlayerController: a fast swim only with breath left.</summary>
        public static bool CanSwimFast { get { if (!loaded) Load(); return !breathOut; } }
        /// <summary>CarController: tyre grip of the player's car.</summary>
        public static float CarGrip => 0.94f + 0.14f * S01(3);
        /// <summary>CarController: how fast the player's steering follows the stick.</summary>
        public static float CarSteer => 0.85f + 0.4f * S01(3);
        /// <summary>PlayerBike: top speed.</summary>
        public static float BikeSpeed => 0.92f + 0.2f * S01(4);
        /// <summary>Seconds of sprint in a full bar.</summary>
        public static float SprintSeconds => (10f + 0.35f * Get(Stat.Stamina)) * (1f - 0.25f * S01(2));
        /// <summary>Seconds of fast swim in a full breath.</summary>
        public static float BreathSeconds => 6f + 0.3f * Get(Stat.Swimming);
        /// <summary>Health back per second now (0 when hungry).</summary>
        public static float RegenPerSecond
        {
            get
            {
                float h = Hunger01;
                return h >= HungryBelow ? Mathf.Lerp(0.4f, 1.2f, (h - HungryBelow) / (1f - HungryBelow)) : 0f;
            }
        }
        /// <summary>Health lost per second to hunger now.</summary>
        public static float StarvePerSecond
        {
            get
            {
                float h = Hunger01;
                return h < HungryBelow ? Mathf.Lerp(0.02f, 0.15f, 1f - h / HungryBelow) : 0f;
            }
        }

        // ---------------------------------------------------------------- write (the API other systems call)
        /// <summary>Food: food01 of the hunger bar (0..1; a plate of momos ~0.35, a full meal ~0.6) and health back.
        /// Eating more than the bar holds puts on fat.</summary>
        public static void Eat(float food01, float healthAdd)
        {
            if (!loaded) Load();
            food01 = Mathf.Max(0f, food01);
            float over = Mathf.Max(0f, hunger + food01 - 1f);
            hunger = Mathf.Min(1f, hunger + food01);
            Add(Stat.Fat, food01 * 4f + over * 8f);
            var p = PlayerController.Instance;
            if (p && p.Health && healthAdd > 0f) p.Health.Heal(healthAdd);
            if (Instance) Instance.warned = 0;
            dirty = true;
        }

        /// <summary>Spends stamina-bar energy (a workout).</summary>
        public static void UseEnergy(float amount)
        {
            if (!loaded) Load();
            energy = Mathf.Max(0f, energy - amount);
            if (energy <= 0f) exhausted = true;
        }

        /// <summary>Burns food off the hunger bar (a workout).</summary>
        public static void UseFood(float amount)
        {
            if (!loaded) Load();
            hunger = Mathf.Max(0f, hunger - amount);
            dirty = true;
        }

        /// <summary>A skill gained by doing the thing: the higher it is, the slower it rises (x1.15 at 0 down to x0.25).
        /// Returns what was added.</summary>
        public static float Train(Stat s, float amount)
        {
            if (!loaded) Load();
            if (amount <= 0f) return 0f;
            float k = Mathf.Clamp(1.15f - vals[(int)s] * 0.01f, 0.25f, 1f);
            return Add(s, amount * k);
        }

        /// <summary>Fat burnt (amount > 0). Returns how much went.</summary>
        public static float Burn(float amount) => amount > 0f ? -Add(Stat.Fat, -amount) : 0f;

        /// <summary>Adds (or takes) points, 0-100; a toast at every fifth point gained. Returns the change.</summary>
        public static float Add(Stat s, float amount)
        {
            if (!loaded) Load();
            int i = (int)s;
            float before = vals[i];
            vals[i] = Mathf.Clamp(before + amount, 0f, 100f);
            float d = vals[i] - before;
            if (d == 0f) return 0f;
            dirty = true;
            int step = Mathf.FloorToInt(vals[i] / 5f);
            if (step > announced[i])
            {
                announced[i] = step;
                GameEvents.ShowToast(UpToast[i] + (step * 5), 2.5f);
            }
            else if (step < announced[i] - (s == Stat.Fat ? 0 : 1))
            {
                if (s == Stat.Fat) GameEvents.ShowToast("Fat ghata: " + ((step + 1) * 5), 2.5f);
                announced[i] = s == Stat.Fat ? step : step + 1;
            }
            return d;
        }

        // ---------------------------------------------------------------- save
        static void Load()
        {
            if (loaded) return;
            loaded = true;
            for (int i = 0; i < StatCount; i++)
            {
                vals[i] = Mathf.Clamp(PlayerPrefs.GetFloat(Keys[i], Starts[i]), 0f, 100f);
                announced[i] = Mathf.FloorToInt(vals[i] / 5f);
            }
            hunger = Mathf.Clamp01(PlayerPrefs.GetFloat(HungerKey, 0.85f));
            energy = 1f;
            breath = 1f;
            exhausted = breathOut = false;
        }

        public static void Save()
        {
            if (!loaded) return;
            for (int i = 0; i < StatCount; i++) PlayerPrefs.SetFloat(Keys[i], vals[i]);
            PlayerPrefs.SetFloat(HungerKey, hunger);
            PlayerPrefs.Save();
            dirty = false;
        }

        /// <summary>A new game: every stat back to the start (SaveGame.Clear should call it).</summary>
        public static void ResetAll()
        {
            for (int i = 0; i < StatCount; i++) PlayerPrefs.DeleteKey(Keys[i]);
            PlayerPrefs.DeleteKey(HungerKey);
            loaded = false;
            Load();
            hunger = 1f;
            dirty = false;
        }

        // ---------------------------------------------------------------- test hooks (Editor/StatsTest.cs)
        public static void DebugSet(Stat s, float v) { if (!loaded) Load(); vals[(int)s] = Mathf.Clamp(v, 0f, 100f); announced[(int)s] = Mathf.FloorToInt(vals[(int)s] / 5f); dirty = true; }
        public static void DebugHunger(float h) { if (!loaded) Load(); hunger = Mathf.Clamp01(h); if (Instance) Instance.warned = 0; dirty = true; }
        public static void DebugEnergy(float e) { if (!loaded) Load(); energy = Mathf.Clamp01(e); exhausted = energy <= 0f; }

        /// <summary>One line per value, for the bridge report.</summary>
        public static string Report()
        {
            if (!loaded) Load();
            var ci = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < StatCount; i++) sb.Append(((Stat)i).ToString()).Append(' ').Append(vals[i].ToString("0.00", ci)).Append('\n');
            sb.Append("hunger ").Append(hunger.ToString("0.000", ci)).Append('\n');
            sb.Append("energy ").Append(energy.ToString("0.000", ci)).Append(exhausted ? " exhausted" : "").Append('\n');
            sb.Append("breath ").Append(breath.ToString("0.000", ci)).Append(breathOut ? " out" : "").Append('\n');
            sb.Append("sprintSeconds ").Append(SprintSeconds.ToString("0.0", ci)).Append("  sprintScale ").Append(SprintScale.ToString("0.000", ci))
              .Append("  jumpScale ").Append(JumpScale.ToString("0.000", ci)).Append("  punch ").Append(PunchScale.ToString("0.000", ci)).Append('\n');
            sb.Append("swimScale ").Append(SwimScale.ToString("0.000", ci)).Append("  breathSeconds ").Append(BreathSeconds.ToString("0.0", ci))
              .Append("  carGrip ").Append(CarGrip.ToString("0.000", ci)).Append("  carSteer ").Append(CarSteer.ToString("0.000", ci))
              .Append("  bikeSpeed ").Append(BikeSpeed.ToString("0.000", ci)).Append('\n');
            sb.Append("regen/s ").Append(RegenPerSecond.ToString("0.00", ci)).Append("  starve/s ").Append(StarvePerSecond.ToString("0.000", ci)).Append('\n');
            sb.Append("working ").Append(Working).Append("  blur ").Append(Instance ? Instance.blurShown.ToString("0.000", ci) : "-")
              .Append("  volume ").Append(Instance && Instance.vol ? (Instance.vol.enabled ? "on w=" + Instance.vol.weight.ToString("0.00", ci) : "off") : "none").Append('\n');
            return sb.ToString();
        }

        // ---------------------------------------------------------------- the per-frame part
        public static PlayerStats Ensure()
        {
            if (!Instance) Instance = new GameObject("PlayerStats").AddComponent<PlayerStats>();
            return Instance;
        }

        PlayerController player;
        CharacterMotor motor;
        Combatant combat;
        bool wasDead;
        int warned;              // hunger warnings given: 1 under 30 %, 2 under 15 %, 3 empty
        float warnRepeat, tiredToastAt, saveT;

        void Awake()
        {
            Instance = this;
            Working = false;
            Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (dirty) Save();
            if (prof)
            {
                foreach (var c in prof.components) if (c) Destroy(c);
                Destroy(prof);
            }
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();

        void Update()
        {
            var p = PlayerController.Instance;
            UpdateBlur(p);
            float dt = Time.deltaTime;
            if (!p || dt <= 0f || FrontEnd.MenuOpen) return;
            if (p != player)
            {
                player = p;
                motor = p.GetComponent<CharacterMotor>();
                combat = p.GetComponent<Combatant>();
            }

            saveT += Time.unscaledDeltaTime;
            if (saveT > 30f) { saveT = 0f; if (dirty) Save(); }

            var mode = p.CurrentMode;
            bool dead = mode == PlayerController.Mode.Dead;
            // woke up again (the hospital, Mall Road): fed enough not to faint straight away
            if (wasDead && !dead && hunger < 0.45f) { hunger = 0.45f; warned = 0; dirty = true; }
            wasDead = dead;
            if (dead) return;

            // ---- what the player is doing
            float hs = motor ? motor.HorizontalVelocity.magnitude : 0f;
            bool normal = mode == PlayerController.Mode.Normal;
            bool swim = normal && p.InWater;
            bool onFoot = normal && !p.InWater && !Jetpack.Flying;
            bool sprint = onFoot && GameInput.SprintHeld && !exhausted && hs > p.runSpeed + 0.3f;
            bool run = onFoot && !sprint && hs > p.walkSpeed + 0.5f;
            bool walk = onFoot && !sprint && !run && hs > 0.25f;
            bool fastSwim = swim && GameInput.SprintHeld && !breathOut && hs > p.swimSpeed * 1.05f;
            var boat = mode == PlayerController.Mode.Boat ? BoatController.Driving : null;
            bool rowing = boat && boat.Speed > 0.5f;
            bool seated = mode == PlayerController.Mode.Seated || mode == PlayerController.Mode.Vehicle || mode == PlayerController.Mode.Bike ||
                          mode == PlayerController.Mode.Mount || (mode == PlayerController.Mode.Boat && !rowing);

            // ---- the stamina bar
            if (!Working)
            {
                float drain = sprint ? 1f : run ? 1f / 12f : walk ? 1f / 30f : swim ? (fastSwim ? 1f / 3f : 1f / 8f) : rowing ? 1f / 10f : 0f;
                if (drain > 0f) energy -= drain / SprintSeconds * dt;
                else
                {
                    float refill = seated ? 1f / 12f : 1f / 22f;   // full in 12 s sitting, 22 s standing still
                    if (hunger < HungryBelow) refill *= Mathf.Lerp(0.3f, 1f, hunger / HungryBelow);
                    energy += refill * dt;
                }
                energy = Mathf.Clamp01(energy);
            }
            if (energy <= 0f) exhausted = true;
            else if (exhausted && energy >= 0.25f) exhausted = false;
            if (exhausted && onFoot && GameInput.SprintHeld && hs > 0.5f && Time.time > tiredToastAt)
            {
                tiredToastAt = Time.time + 8f;
                GameEvents.ShowToast("Saans phool gayi! Ruk ke saans lo", 2f);
            }

            // ---- breath (a fast swim)
            if (fastSwim) breath -= dt / BreathSeconds;
            else breath += dt / (swim ? 8f : 3f);
            breath = Mathf.Clamp01(breath);
            if (breath <= 0f && !breathOut) { breathOut = true; GameEvents.ShowToast("Saans phool gayi - dheere tairo", 2f); }
            else if (breathOut && breath >= 0.35f) breathOut = false;

            // ---- hunger
            float hm = sprint ? 1.8f : run ? 1.25f : swim ? 1.6f : rowing ? 1.4f : 1f;
            hunger = Mathf.Max(0f, hunger - hm * dt / HungerSeconds);
            dirty = true;

            // ---- skills and fat by doing the thing (a few minutes of each shows)
            if (sprint) { Train(Stat.Stamina, dt / 20f); Add(Stat.Fat, -dt / 30f); }
            else if (run) { Train(Stat.Stamina, dt / 150f); Add(Stat.Fat, -dt / 150f); }
            else if (walk) Add(Stat.Fat, -dt / 600f);
            if (swim) { Train(Stat.Swimming, dt / 15f * (fastSwim ? 1.5f : 1f)); Train(Stat.Stamina, dt / 90f); Add(Stat.Fat, -dt / 60f); }
            if (rowing) { Train(Stat.Stamina, dt / 120f); Train(Stat.Muscle, dt / 300f); Add(Stat.Fat, -dt / 150f); }
            var car = mode == PlayerController.Mode.Vehicle ? CarController.Driving : null;
            if (car && !car.wrecked && car.Speed > 1.5f && car.Speed < 60f) Train(Stat.Driving, car.Speed * dt * 0.004f);   // ~4 a km at the start
            var bike = mode == PlayerController.Mode.Bike ? PlayerBike.Riding : null;
            if (bike && bike.Bike && bike.Bike.Speed > 1f && bike.Bike.Speed < 40f) Train(Stat.Bike, bike.Bike.Speed * dt * 0.005f);   // ~5 a km
            if (hunger <= 0f)   // starving: the fat goes first, then the muscle
            {
                if (vals[(int)Stat.Fat] > 0f) Add(Stat.Fat, -dt / 60f);
                else Add(Stat.Muscle, -dt / 90f);
            }

            // ---- health: slower regen, none when hungry, falling when hungry
            var h = p.Health;
            if (h)
            {
                h.regenPerSecond = RegenPerSecond;
                h.regenDelay = RegenDelay;
                float sp = StarvePerSecond;
                if (sp > 0f && !h.IsDead && !h.invulnerable)
                {
                    if (h.current - sp * dt <= 0.5f)
                    {
                        GameEvents.ShowToast("Bhookh se behosh ho gaye...", 3f);
                        h.Damage(h.current + 1f, p.transform.position + Vector3.up * 3f, null);
                    }
                    else h.current -= sp * dt;
                }
            }
            if (combat) combat.damageMultiplier = PunchScale;

            // ---- hunger warnings
            int lvl = hunger <= 0f ? 3 : hunger < 0.15f ? 2 : hunger < HungryBelow ? 1 : 0;
            if (lvl > warned || (lvl >= 2 && Time.time > warnRepeat))
            {
                warned = lvl;
                warnRepeat = Time.time + 120f;
                GameEvents.ShowToast(lvl == 3 ? "Bhookh se health gir rahi hai - jaldi kuch khao!" :
                                     lvl == 2 ? "Bahut bhookh lagi hai! Aankhon ke aage dhundla ho raha hai" :
                                                "Bhookh lag rahi hai - kuch kha lo", 3.5f);
            }
            else if (lvl < warned) warned = lvl;
        }

        // ---------------------------------------------------------------- hunger blur (a runtime URP Volume)
        Volume vol;
        VolumeProfile prof;
        float blurShown;

        /// <summary>Gaussian depth of field (the cheap URP mode, only on while hungry), a darker vignette and less colour,
        /// ramping in from 30 % hunger to empty, with a slow woozy swell under 15 %. The profile is set as the volume's
        /// own (Volume.profile), not sharedProfile, so GameSettings.ApplyGraphics (which turns the depth of field of
        /// shared profiles off below High) leaves it alone.</summary>
        void UpdateBlur(PlayerController p)
        {
            float target = 0f;
            if (p && !FrontEnd.MenuOpen && p.CurrentMode != PlayerController.Mode.Dead)
                target = Mathf.Clamp01((HungryBelow - hunger) / HungryBelow);
            blurShown = Mathf.MoveTowards(blurShown, target, Time.unscaledDeltaTime * 0.5f);
            if (blurShown > 0.001f)
            {
                if (!vol) MakeVolume();
                float w = Mathf.SmoothStep(0f, 1f, blurShown);
                if (blurShown > 0.5f) w += 0.12f * (blurShown - 0.5f) * 2f * Mathf.Sin(Time.unscaledTime * 1.3f);
                vol.weight = Mathf.Clamp01(w);
                if (!vol.enabled) vol.enabled = true;
            }
            else if (vol && vol.enabled)
            {
                vol.weight = 0f;
                vol.enabled = false;
            }
        }

        void MakeVolume()
        {
            var go = new GameObject("Hunger Blur");
            go.transform.SetParent(transform, false);
            vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 40f;
            vol.weight = 0f;
            prof = ScriptableObject.CreateInstance<VolumeProfile>();
            prof.name = "HungerBlur";
            var dof = prof.Add<DepthOfField>(false);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0.3f);
            dof.gaussianEnd.Override(5f);
            dof.gaussianMaxRadius.Override(1.5f);
            dof.highQualitySampling.Override(false);
            var vig = prof.Add<Vignette>(false);
            vig.intensity.Override(0.48f);
            vig.smoothness.Override(0.6f);
            var col = prof.Add<ColorAdjustments>(false);
            col.saturation.Override(-40f);
            vol.profile = prof;
        }
    }
}

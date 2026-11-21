using System.Globalization;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Fuel (owner, 21 Nov 2026, FREEROAM #4: "a gauge on the HUD; a car / bike stops when empty; refuelling costs money").
    /// Added to a vehicle the first time the player drives or rides it: a tank sized for its model (VehicleInfo.model),
    /// 30-75 % full. It burns only while the PLAYER drives (none parked, none for the town's traffic), more under the
    /// pedal; a full tank lasts about 15-20 minutes of town driving (12 flat out, ~48 rolling gently). Empty: the pedal no
    /// longer pulls (a pedal against the motion still brakes), a toast once; a warning once at 15 %. The electric ones
    /// (the tirri e-rickshaw, an electric scooty) never run dry and show no gauge.
    /// Refuelling: stop inside a <see cref="FuelStation"/> forecourt and it fills over ~3 s at Rs 100 a litre, as much as
    /// the player can pay for (GameState); driving off half way pays for what went in.
    /// Hooks: CarController.FixedUpdate and PlayerBike.FixedUpdate pass the player's pedal through <see cref="Drive"/>;
    /// the HUD's FuelGauge reads <see cref="Player"/>.
    /// </summary>
    public class VehicleFuel : MonoBehaviour
    {
        /// <summary>Rs a litre, petrol and diesel alike (the pumps' boards say the same).</summary>
        public const int PricePerLitre = 100;
        /// <summary>Seconds of fuel a full tank holds at a burn of 1 (see Burn): at town driving (~0.6 pedal) the burn is
        /// 0.3 + 0.9 x 0.6 = 0.84, so a tank lasts 857 / 0.84 = 1020 s = 17 min.</summary>
        const float TankSeconds = 857f, IdleBurn = 0.3f, LoadBurn = 0.9f;
        const float FillSeconds = 3f, LowAt = 0.15f;

        public float capacity = 35f, litres = 20f;
        /// <summary>Battery: never runs dry, no gauge.</summary>
        public bool electric;

        public float Fraction => electric || capacity <= 0f ? 1f : Mathf.Clamp01(litres / capacity);
        public bool Empty => !electric && litres <= 0.0001f;
        /// <summary>Being filled at a pump now (the gauge rises).</summary>
        public bool Filling { get; private set; }

        CarController car;
        PlayerBike bike;
        bool warnedLow, toldDry, doneHere;
        FuelStation lastStation;
        float still, fillT, fillFrom, fillTo;
        bool fillShort;

        // ------------------------------------------------------------------ making one
        /// <summary>The fuel of a vehicle (its CarController or PlayerBike), made the first time: tank by model, 30-75 % full.</summary>
        public static VehicleFuel For(Component vehicle)
        {
            if (!vehicle) return null;
            var f = vehicle.GetComponent<VehicleFuel>();
            if (f) return f;
            f = vehicle.gameObject.AddComponent<VehicleFuel>();
            f.Setup();
            return f;
        }

        /// <summary>The fuel of what the player drives or rides now, or null on foot.</summary>
        public static VehicleFuel Player
        {
            get
            {
                if (CarController.Driving) return For(CarController.Driving);
                if (PlayerBike.Riding) return For(PlayerBike.Riding);
                return null;
            }
        }

        void Setup()
        {
            car = GetComponent<CarController>();
            bike = GetComponent<PlayerBike>();
            var info = GetComponent<VehicleInfo>();
            string n = (info && !string.IsNullOrEmpty(info.model) ? info.model : name).ToLowerInvariant();
            electric = n.Contains("electric") || n.Contains("tirri") || n.Contains("erickshaw") || n.Contains("e-rickshaw");
            capacity = TankLitres(n, car ? car.type : CarType.Hatchback, bike != null);
            litres = capacity * Random.Range(0.30f, 0.75f);
        }

        /// <summary>A tank for the model (litres): the library's names (Resources/VehicleLibrary), else the kind's.</summary>
        static float TankLitres(string n, CarType type, bool twoWheeler)
        {
            if (twoWheeler || n.StartsWith("bike") || n.StartsWith("scooty"))
            {
                if (n.Contains("scooty")) return 5.5f;
                if (n.Contains("dualsport")) return 13f;
                if (n.Contains("160")) return 12f;
                if (n.Contains("125")) return 10.5f;
                return 9f;
            }
            if (n.Contains("bus")) return 160f;
            if (n.Contains("truck")) return 60f;
            if (n.Contains("tuktuk") || n.Contains("auto")) return 8f;
            if (n.Contains("pickup") || n.Contains("suv") || n.Contains("mpv_large") || n.Contains("ambulance")) return 55f;
            if (n.Contains("mpv")) return 45f;
            if (n.Contains("van")) return 40f;
            if (n.Contains("sedan")) return 37f;
            if (n.Contains("hatch") || n.Contains("tallboy")) return 32f;
            switch (type)
            {
                case CarType.Bus: return 160f;
                case CarType.Suv:
                case CarType.Pickup: return 55f;
                case CarType.Taxi: return 45f;
                case CarType.Sedan: return 37f;
                default: return 32f;
            }
        }

        // ------------------------------------------------------------------ driving (the hook)
        /// <summary>
        /// The player's pedal this physics step (-1..1) through the tank: burns fuel and returns the pedal the vehicle may
        /// use - none, forwards or backwards, once the tank is dry; a pedal against the motion is a brake and always passes.
        /// forwardSpeed: m/s along the nose (a bike's speed is never negative).
        /// </summary>
        public static float Drive(Component vehicle, float pedal, float forwardSpeed, float dt)
        {
            var f = For(vehicle);
            return f ? f.Burn(pedal, forwardSpeed, dt) : pedal;
        }

        float Burn(float pedal, float fwd, float dt)
        {
            if (electric) return pedal;
            bool braking = Mathf.Abs(fwd) > 0.5f && pedal * fwd < 0f;
            float load = braking ? 0f : Mathf.Abs(pedal);
            if (litres > 0f && (Mathf.Abs(fwd) > 0.5f || load > 0.05f))
                litres = Mathf.Max(0f, litres - capacity * (IdleBurn + load * LoadBurn) / TankSeconds * dt);
            if (litres > 0f || braking) return pedal;
            return 0f;
        }

        // ------------------------------------------------------------------ warnings and the pumps
        bool IsPlayers => (car && CarController.Driving == car) || (bike && PlayerBike.Riding == bike);
        float Speed => car ? car.Speed : bike && bike.Bike ? bike.Bike.Speed : 0f;

        void Update()
        {
            if (!IsPlayers)
            {
                if (Filling) EndFill(true);   // got out half way: pay for what went in
                lastStation = null;
                return;
            }
            float dt = Time.deltaTime;

            if (!electric)
            {
                if (Fraction > LowAt + 0.1f) warnedLow = false;
                if (litres > 0f) toldDry = false;
                if (!warnedLow && litres > 0f && Fraction <= LowAt)
                {
                    warnedLow = true;
                    GameEvents.ShowToast("Petrol kam hai (" + Mathf.Max(1, Mathf.RoundToInt(Fraction * 100f)) + "%) - petrol pump dhoondo (map pe)", 4.5f);
                    Haptics.Light();
                }
                if (!toldDry && Empty)
                {
                    toldDry = true;
                    var near = FuelStation.Nearest(transform.position, out float d);
                    GameEvents.ShowToast(near ? "Petrol khatam! Gaadi nahi chalegi. Sabse paas ka pump " + Mathf.RoundToInt(d) + " m door"
                                              : "Petrol khatam! Gaadi nahi chalegi", 5f);
                    Haptics.Medium();
                }
            }

            var station = FuelStation.At(transform.position);
            if (station != lastStation)
            {
                if (Filling) EndFill(true);
                lastStation = station;
                still = 0f;
                doneHere = false;
                if (station)
                {
                    if (electric) GameEvents.ShowToast("Ye electric hai - petrol nahi chahiye, chalte raho", 3f);
                    else if (Fraction >= 0.97f) GameEvents.ShowToast("Tanki pehle se full hai", 2.5f);
                    else GameEvents.ShowToast("Petrol pump: gaadi roko, tanki bhar denge (Rs " + PricePerLitre + " / litre)", 3.5f);
                }
            }
            if (!station || electric) return;

            if (Filling)
            {
                if (Speed > 1.5f) { EndFill(true); return; }   // drove off half way
                fillT += dt;
                float k = Mathf.Clamp01(fillT / FillSeconds);
                litres = Mathf.Lerp(fillFrom, fillTo, Mathf.SmoothStep(0f, 1f, k));
                if (k >= 1f) EndFill(false);
                return;
            }
            if (doneHere) return;
            still = Speed < 0.6f ? still + dt : 0f;
            if (still > 0.8f) StartFill();
        }

        void StartFill()
        {
            doneHere = true;
            float need = capacity - litres;
            if (need < capacity * 0.03f) { GameEvents.ShowToast("Tanki pehle se full hai", 2.5f); return; }
            float afford = GameState.Money / (float)PricePerLitre;
            if (afford < 0.1f) { GameEvents.ShowToast("Paise nahi - petrol nahi milega", 3f); Haptics.Light(); return; }
            fillFrom = litres;
            fillTo = litres + Mathf.Min(need, afford);
            fillShort = afford < need;
            fillT = 0f;
            Filling = true;
            GameEvents.ShowToast("Petrol bhar raha hai...", FillSeconds);
        }

        /// <summary>The fill is over (full, or cut short): pay for the litres that went in.</summary>
        void EndFill(bool cut)
        {
            Filling = false;
            doneHere = true;
            float got = Mathf.Max(0f, litres - fillFrom);
            int cost = Mathf.CeilToInt(got * PricePerLitre - 0.01f);
            if (cost <= 0) return;
            if (!GameState.TrySpend(cost)) GameState.AddMoney(-GameState.Money);   // (the money changed while it filled)
            string l = got.ToString("0.0", CultureInfo.InvariantCulture);
            if (cut) GameEvents.ShowToast("Aadhe mein chal diye: " + l + " litre, Rs " + cost, 3.5f);
            else if (fillShort) GameEvents.ShowToast("Paise kam the: " + l + " litre bhara, Rs " + cost, 4f);
            else GameEvents.ShowToast("Tanki full: " + l + " litre, Rs " + cost, 3.5f);
            Haptics.Success();
        }
    }
}

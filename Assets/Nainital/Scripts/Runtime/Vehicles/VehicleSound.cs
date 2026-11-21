using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The sound of one vehicle (owner, 22 Nov 2026: "sound effect sahi kro...har vehicle ka accleartion, braking..hit
    /// wgera tgda sound..like real..horn random kyun bjta har bar..har gaadi ka assign krdo..or dipper ke saath horn bhi
    /// bjva do"). Each kind has its own engine recording (Resources/Audio/veh, Tools/fetch_vehicle_sounds.py): a petrol
    /// car, a diesel SUV / pickup / van, a bus, a truck, a commuter bike, a 350 single, a scooty, an electric scooty, an
    /// auto rickshaw. The engine is pitched through simulated gears (it climbs in a gear, drops at the change, a scooty's
    /// CVT just rises), louder under load, quieter coasting, idling at a stop. Tyres squeal when they slide
    /// (CarController.Slip); a bus or truck hisses its air brakes as it stops, a car's brakes squeak now and then; a knock
    /// sounds by how hard it was (a clank, a crunch, a crash with glass); a two-wheeler going down scrapes. Every vehicle
    /// keeps ONE horn for its whole life (a car one of eight, a bus one of four, a bike / scooty / auto a small horn at its
    /// own pitch). The engine plays only near the listener (voices on a phone).
    /// </summary>
    public class VehicleSound : MonoBehaviour
    {
        public enum Kind { Car, Diesel, Bus, Truck, Bike, BikeBig, Scooty, ScootyElectric, Auto }

        class Profile
        {
            public string clip; public float idle, top, loud; public float[] gears; public bool cvt, electric;
        }

        static readonly Dictionary<Kind, Profile> profiles = new Dictionary<Kind, Profile>
        {
            // gears: the speed (m/s) at the top of each gear
            { Kind.Car, new Profile { clip = "eng_car_a", idle = 1f, top = 1.85f, loud = 1f, gears = new[] { 5.5f, 10f, 15.5f, 22f, 32f } } },
            { Kind.Diesel, new Profile { clip = "eng_diesel", idle = 1f, top = 1.65f, loud = 1.05f, gears = new[] { 5f, 9.5f, 14.5f, 21f, 29f } } },
            { Kind.Bus, new Profile { clip = "eng_bus", idle = 1f, top = 1.5f, loud = 1.25f, gears = new[] { 3.5f, 7f, 11f, 16f, 22f } } },
            { Kind.Truck, new Profile { clip = "eng_truck", idle = 1f, top = 1.5f, loud = 1.2f, gears = new[] { 4f, 8f, 12.5f, 18f, 24f } } },
            { Kind.Bike, new Profile { clip = "eng_bike", idle = 1f, top = 2.1f, loud = 0.85f, gears = new[] { 4.5f, 8.5f, 12.5f, 16.5f, 22f } } },
            { Kind.BikeBig, new Profile { clip = "eng_bike_big", idle = 1f, top = 1.75f, loud = 0.95f, gears = new[] { 5f, 9.5f, 14f, 19f, 27f } } },
            { Kind.Scooty, new Profile { clip = "eng_scooty", idle = 1f, top = 1.8f, loud = 0.8f, gears = new[] { 14f }, cvt = true } },
            { Kind.ScootyElectric, new Profile { clip = "eng_scooty_e", idle = 0.55f, top = 1.6f, loud = 0.55f, gears = new[] { 13f }, cvt = true, electric = true } },
            { Kind.Auto, new Profile { clip = "eng_auto", idle = 1f, top = 1.55f, loud = 0.95f, gears = new[] { 3.5f, 6.5f, 10f, 14f } } },
        };

        public Kind kind;
        Profile pf;
        CarController car;
        BikeRide bike;
        VehicleDamage damage;
        AudioSource eng, skid;
        AudioClip hornClip;
        float hornPitch, hornVol, pitchJitter, lastSpeed, load, rpm, shiftT, stopCheck, brakeCd, hornCd, timer, peakSpeed;
        int gear;
        bool near;

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioClip Clip(string n)
        {
            if (!clips.TryGetValue(n, out var c)) { c = Resources.Load<AudioClip>("Audio/veh/" + n); clips[n] = c; }
            return c;
        }

        static readonly string[] CarHorns = { "horn_car_0", "horn_car_1", "horn_car_2", "horn_car_3", "horn_car_4", "horn_car_5", "horn_car_6", "horn_car_7" };
        static readonly string[] BusHorns = { "horn_bus_0", "horn_bus_1", "horn_bus_2", "horn_bus_3" };
        static readonly string[] SmallHorns = { "horn_small_0", "horn_small_1" };

        /// <summary>Which kind of vehicle this is, from the library model's name (VehicleInfo.model), else the old kinds.</summary>
        public static Kind KindOf(GameObject go)
        {
            var info = go.GetComponent<VehicleInfo>();
            string m = ((info && !string.IsNullOrEmpty(info.model) ? info.model : go.name) ?? "").ToLowerInvariant();
            if (m.Contains("scooty") || m.Contains("scooter")) return m.Contains("electric") ? Kind.ScootyElectric : Kind.Scooty;
            if (m.Contains("tuktuk") || m.Contains("auto") || m.Contains("rickshaw")) return Kind.Auto;
            if (m.Contains("bike") || m.Contains("moto")) return m.Contains("350") || m.Contains("retro") || m.Contains("bullet") ? Kind.BikeBig : Kind.Bike;
            if (m.Contains("truck")) return m.Contains("light") ? Kind.Diesel : Kind.Truck;   // a light truck (pickup size): a diesel
            if (m.Contains("bus")) return Kind.Bus;
            if (m.Contains("van") || m.Contains("pickup") || m.Contains("suv") || m.Contains("mpv_large") || m.Contains("jeep") || m.Contains("ambulance")) return Kind.Diesel;
            var cc = go.GetComponent<CarController>();
            if (cc)
            {
                var tc = go.GetComponent<TrafficCar>();
                var t = tc ? tc.type : CarType.Sedan;
                if (t == CarType.Bus) return Kind.Bus;
                if (t == CarType.Suv || t == CarType.Pickup) return Kind.Diesel;
                return Kind.Car;
            }
            return go.GetComponent<BikeRide>() ? Kind.Bike : Kind.Car;
        }

        void Start()
        {
            car = GetComponent<CarController>();
            bike = GetComponent<BikeRide>();
            damage = GetComponent<VehicleDamage>();
            kind = KindOf(gameObject);
            pf = profiles[kind];
            pitchJitter = Random.Range(0.96f, 1.04f);   // no two engines quite alike

            // the engine's source: the factory's (VehicleFX gives it up), else a new one
            var fx = GetComponent<VehicleFX>();
            if (fx && fx.engine) { eng = fx.engine; fx.engine = null; }
            else
            {
                var go = new GameObject("Engine audio");
                go.transform.SetParent(transform, false);
                eng = go.AddComponent<AudioSource>();
                eng.loop = true;
                eng.playOnAwake = false;
                eng.spatialBlend = 1f;
                eng.rolloffMode = AudioRolloffMode.Linear;
                eng.dopplerLevel = 0.3f;
            }
            eng.minDistance = kind == Kind.Bus || kind == Kind.Truck ? 6f : 4f;
            eng.maxDistance = kind == Kind.Bus || kind == Kind.Truck ? 70f : 55f;
            eng.volume = 0f;
            var c = Clip(pf.clip);
            if (kind == Kind.Car && Random.value < 0.5f) c = Clip("eng_car_b") ?? c;   // two kinds of petrol car
            eng.clip = c;

            // this vehicle's own horn, for good
            string[] set = kind == Kind.Bus || kind == Kind.Truck ? BusHorns : kind == Kind.Car || kind == Kind.Diesel ? CarHorns : SmallHorns;
            hornClip = Clip(set[Random.Range(0, set.Length)]);
            switch (kind)
            {
                case Kind.Bike: hornPitch = Random.Range(1.28f, 1.45f); break;
                case Kind.BikeBig: hornPitch = Random.Range(1.12f, 1.25f); break;
                case Kind.Scooty: case Kind.ScootyElectric: hornPitch = Random.Range(1.45f, 1.65f); break;
                case Kind.Auto: hornPitch = Random.Range(1.02f, 1.15f); break;
                default: hornPitch = Random.Range(0.96f, 1.04f); break;
            }
            hornVol = kind == Kind.Bus || kind == Kind.Truck ? 0.9f : kind == Kind.Car || kind == Kind.Diesel ? 0.8f : 0.7f;
        }

        bool Driven => (car && CarController.Driving == car) || (bike && PlayerBike.Riding && PlayerBike.Riding.gameObject == gameObject);

        bool Running
        {
            get
            {
                if (damage && (damage.Burning || damage.Wrecked)) return false;
                if (car) return !car.wrecked && (car.ai || car.HasDriver);
                if (bike) return !bike.Crashed && (GetComponent<TrafficBike>() || GetComponent<PoliceBike>() || Driven);
                return false;
            }
        }

        float Speed => car ? Mathf.Abs(car.ForwardSpeed) : bike ? bike.Speed : 0f;

        void Update()
        {
            if (pf == null || !eng) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var am = AudioManager.Instance;
            float master = am ? am.masterVolume : 0.8f;
            bool driven = Driven;
            float speed = Speed;
            float accel = (speed - lastSpeed) / dt;
            lastSpeed = speed;

            timer -= dt;
            if (timer <= 0f)
            {
                timer = 0.25f;
                var cam = Camera.main;
                float r = eng.maxDistance + 5f;
                near = Running && cam && (driven || (cam.transform.position - transform.position).sqrMagnitude < r * r);
                if (near && !eng.isPlaying && eng.clip) { eng.time = Random.Range(0f, eng.clip.length * 0.9f); eng.Play(); }
                else if (!near && eng.isPlaying) eng.Stop();
                eng.spatialBlend = driven ? 0.75f : 1f;
            }

            // load: the pedal (the player's, the AI's), else how hard it is pulling away
            float want;
            if (driven) want = Mathf.Clamp01(GameInput.UiBlocking ? 0f : GameInput.Move.y);
            else if (car) want = Mathf.Clamp01(car.Throttle);
            else want = Mathf.Clamp01(accel / 2f);
            if (car && car.Braking) want = 0f;
            load = Mathf.MoveTowards(load, want, dt * (want > load ? 4f : 2.5f));

            if (eng.isPlaying)
            {
                float pitch;
                float shiftDip = 1f;
                if (pf.cvt)
                {
                    float s01 = Mathf.Clamp01(speed / pf.gears[0]);
                    pitch = Mathf.Lerp(pf.idle, pf.top, s01 * 0.8f + load * 0.2f);
                }
                else
                {
                    // gears: up near the top of one, down a little below the one before (hysteresis)
                    var g = pf.gears;
                    if (gear < g.Length - 1 && speed > g[gear] * 0.97f) { gear++; shiftT = 0.22f; }
                    else if (gear > 0 && speed < g[gear - 1] * 0.8f) gear--;
                    float lo = gear == 0 ? 0f : g[gear - 1] * 0.8f;
                    float r01 = Mathf.Clamp01((speed - lo) / Mathf.Max(0.5f, g[gear] - lo));
                    rpm = speed < 0.4f && load < 0.1f ? 0f : Mathf.Lerp(0.18f, 1f, r01 * 0.8f + load * 0.2f);
                    pitch = Mathf.Lerp(pf.idle, pf.top, rpm);
                    if (shiftT > 0f) { shiftT -= dt; shiftDip = 0.72f; pitch *= 0.93f; }   // the clutch: a dip, then the next gear
                }
                pitch *= pitchJitter;
                float coast = speed > 2f && load < 0.05f ? 0.7f : 1f;
                float vol = pf.electric ? Mathf.Clamp01(0.15f + speed / 6f) * (0.6f + 0.4f * load)
                                        : (0.55f + 0.45f * load) * coast;
                vol *= 0.42f * pf.loud * master * shiftDip * (driven ? 0.85f : 1f);
                eng.pitch = Mathf.MoveTowards(eng.pitch, pitch, dt * (shiftT > 0f ? 6f : 2.2f));
                eng.volume = Mathf.Lerp(eng.volume, vol, dt * 6f);
            }

            Skid(dt, speed, master);
            Brakes(dt, speed, accel, master);
            if (hornCd > 0f) hornCd -= dt;
        }

        void Skid(float dt, float speed, float master)
        {
            float s = car && near ? Mathf.Clamp01((car.Slip - 0.2f) / 0.6f) * Mathf.Clamp01((car.Speed - 1.5f) / 5f) : 0f;   // the whole speed: sliding sideways the forward part is small
            if (s > 0.02f && !skid)
            {
                var c = Clip("skid");
                if (!c) return;
                var go = new GameObject("Skid audio");
                go.transform.SetParent(transform, false);
                skid = go.AddComponent<AudioSource>();
                skid.clip = c;
                skid.loop = true;
                skid.playOnAwake = false;
                skid.spatialBlend = 1f;
                skid.minDistance = 5f;
                skid.maxDistance = 70f;
                skid.rolloffMode = AudioRolloffMode.Linear;
                skid.dopplerLevel = 0f;
                skid.volume = 0f;
            }
            if (!skid) return;
            skid.volume = Mathf.MoveTowards(skid.volume, s * 0.75f * master, dt * 6f);
            skid.pitch = 0.9f + s * 0.2f;
            if (skid.volume > 0.005f) { if (!skid.isPlaying) { skid.time = Random.Range(0f, skid.clip.length * 0.8f); skid.Play(); } }
            else if (skid.isPlaying) skid.Stop();
        }

        /// <summary>Coming to a stop: a bus / truck hisses its air brakes, a car's brakes squeak now and then.</summary>
        void Brakes(float dt, float speed, float accel, float master)
        {
            brakeCd -= dt;
            peakSpeed = speed > 3f ? Mathf.Max(peakSpeed, speed) : peakSpeed;
            if (speed < 0.35f && peakSpeed > 3f)
            {
                peakSpeed = 0f;
                if (!near || brakeCd > 0f) return;
                var am = AudioManager.Instance;
                if (!am) return;
                if (kind == Kind.Bus || kind == Kind.Truck)
                {
                    if (Random.value < 0.8f) { am.PlayAt(Clip("brake_air"), transform.position, 0.6f, Random.Range(0.95f, 1.05f), 60f); brakeCd = 6f; }
                }
                else if (car && Random.value < 0.15f) { am.PlayAt(Clip("brake_squeal"), transform.position, 0.3f, Random.Range(0.95f, 1.1f), 40f); brakeCd = 15f; }
            }
        }

        /// <summary>This vehicle's own horn.</summary>
        public void Horn()
        {
            if (hornCd > 0f || !hornClip) return;
            hornCd = 0.3f;
            var am = AudioManager.Instance;
            if (am) am.PlayAt(hornClip, transform.position + Vector3.up, hornVol, hornPitch, kind == Kind.Bus || kind == Kind.Truck ? 200f : 160f);
        }

        /// <summary>The vehicle's own horn, or the old shared one for anything without a VehicleSound.</summary>
        public static void HornOf(GameObject go, Vector3 at, bool big)
        {
            var vs = go ? go.GetComponent<VehicleSound>() : null;
            if (vs && vs.pf != null) vs.Horn();
            else if (AudioManager.Instance) AudioManager.Instance.HornAt(at, big);
        }

        /// <summary>Knocks sounded so far and the last one (checks: VehicleTest.DriveSound / SoundReport).</summary>
        public static int Knocks;
        public static string LastKnock = "-";

        /// <summary>A knock (VehicleDamage): a clank under ~4 m/s, a crunch to ~8, a crash above, glass too when it is
        /// hard (or against another vehicle at speed).</summary>
        public void Knock(float dv, Vector3 at, bool vehicle)
        {
            var am = AudioManager.Instance;
            if (!am) return;
            string n; float v;
            Knocks++;
            if (dv < 4f) { n = "hit_light_" + Random.Range(1, 4); v = 0.35f + dv * 0.06f; }
            else if (dv < 8f) { n = "hit_med_" + Random.Range(1, 3); v = 0.7f + (dv - 4f) * 0.05f; }
            else { n = "hit_heavy_" + Random.Range(1, 3); v = 1f; }
            float big = kind == Kind.Bus || kind == Kind.Truck ? 0.85f : kind >= Kind.Bike && kind <= Kind.ScootyElectric ? 1.12f : 1f;
            am.PlayAt(Clip(n) ?? am.Clip("crash_1"), at, Mathf.Clamp01(v), Random.Range(0.92f, 1.06f) * big, dv < 4f ? 50f : 110f);
            LastKnock = $"{name}: {n} at {dv:F1} m/s";
            if (dv > 10f || (vehicle && dv > 7f)) am.PlayAt(Clip("glass_1"), at + Vector3.up * 0.5f, 0.6f, Random.Range(0.95f, 1.1f), 60f);
        }

        /// <summary>A two-wheeler knocked over: the crash and the scrape along the road.</summary>
        public void Fall(float dv, Vector3 at)
        {
            var am = AudioManager.Instance;
            if (!am) return;
            am.PlayAt(Clip("bike_fall"), at, 0.85f, Random.Range(0.95f, 1.08f), 80f);
            Knock(Mathf.Max(4f, dv * 0.6f), at, true);
            if (eng && eng.isPlaying) eng.Stop();
        }
    }
}

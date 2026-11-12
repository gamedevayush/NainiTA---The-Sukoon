using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// What a running vehicle gives off (user, 2026-11-12: "vehicle ka khud ka accelerating, brakes, smoke, lights"):
    /// its own engine sound, pitched and louder with speed and throttle; exhaust smoke, a few puffs at idle and a stream
    /// when it pulls away; tail lamps that flare when it brakes, day or night. Works for AI traffic (TrafficCar) and for
    /// the car the player drives (CarController); a parked car stays silent. The engine sound plays only within ~55 m.
    /// </summary>
    public class VehicleFX : MonoBehaviour
    {
        public ParticleSystem exhaust;
        public Renderer tail;
        public AudioSource engine;

        TrafficCar traffic;
        CarController car;
        Rigidbody rb;
        float lastSpeed, brakeGlow, timer;
        MaterialPropertyBlock mpb;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color TailNight = new Color(3f, 0.18f, 0.1f), TailBrake = new Color(7f, 0.35f, 0.18f);

        void Start()
        {
            traffic = GetComponent<TrafficCar>();
            car = GetComponent<CarController>();
            rb = GetComponent<Rigidbody>();
            mpb = new MaterialPropertyBlock();
            if (engine && AudioManager.Instance) engine.clip = AudioManager.Instance.engineClip;
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (!traffic) traffic = GetComponent<TrafficCar>();
            bool driven = car && CarController.Driving == car;
            bool running = traffic || driven;
            float speed = traffic ? traffic.Speed : (rb && !rb.isKinematic ? rb.linearVelocity.magnitude : 0f);
            float accel = (speed - lastSpeed) / dt;
            lastSpeed = speed;
            float throttle = driven ? Mathf.Abs(GameInput.Move.y) : Mathf.Clamp01(accel / 2.5f);
            bool braking = running && (driven ? (car.Braking) : accel < -1.6f);
            brakeGlow = Mathf.MoveTowards(brakeGlow, braking ? 1f : 0f, dt * (braking ? 10f : 3f));

            // tail lamps: on at night (VehicleLamps), and bright whenever braking
            if (tail)
            {
                var dn = DayNightCycle.Instance;
                bool night = dn && dn.NightFactor > 0.3f;
                bool show = running && (night || brakeGlow > 0.05f);
                if (tail.enabled != show) tail.enabled = show;
                if (show)
                {
                    mpb.SetColor(ColorId, Color.Lerp(night ? TailNight : TailNight * 0.6f, TailBrake, brakeGlow));
                    tail.SetPropertyBlock(mpb);
                }
            }

            // exhaust: idle puffs, more when pulling away
            if (exhaust)
            {
                var em = exhaust.emission;
                em.rateOverTime = running ? Mathf.Lerp(3f, 22f, throttle) * (speed > 14f ? 0.6f : 1f) : 0f;
            }

            // engine: only near the listener, to keep the voice count low on a phone
            timer -= dt;
            if (engine && timer <= 0f)
            {
                timer = 0.25f;
                var cam = Camera.main;
                bool near = running && cam && (cam.transform.position - transform.position).sqrMagnitude < 55f * 55f && !driven;   // the player's own car has AudioManager's engine
                if (near && !engine.isPlaying && engine.clip) engine.Play();
                else if (!near && engine.isPlaying) engine.Stop();
            }
            if (engine && engine.isPlaying)
            {
                float m = AudioManager.Instance ? AudioManager.Instance.masterVolume : 0.8f;
                engine.pitch = Mathf.Lerp(engine.pitch, 0.7f + Mathf.Clamp01(speed / 18f) * 0.8f + throttle * 0.25f, dt * 4f);
                engine.volume = Mathf.Lerp(engine.volume, (0.22f + throttle * 0.25f) * m, dt * 4f);
            }
        }
    }
}

using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Drivable arcade car: raycast suspension per wheel, tyre grip, 4WD drive, brakes, handbrake drift,
    /// auto-upright after a crash. Parked cars sleep as kinematic bodies until someone gets in.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : Interactable
    {
        public static CarController Driving { get; private set; }

        public CarType type;
        public Transform[] wheels;     // FL, FR, RL, RR
        public Transform seat;
        public Light[] headlights;

        public float Speed => rb ? rb.linearVelocity.magnitude : 0f;
        public float ForwardSpeed => rb ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;

        // AI traffic drives the same physics as the player (TrafficCar sets these every step): throttle -1..1 (against
        // the motion = brake), steer in degrees, handbrake 0/1. Each car has its own mass, so a crash is a real one.
        [System.NonSerialized] public bool ai;
        [System.NonSerialized] public float aiThrottle, aiSteer, aiHandbrake;
        /// <summary>Burnt out (VehicleDamage): no engine, nobody can drive it.</summary>
        [System.NonSerialized] public bool wrecked;
        public bool HasDriver => driver != null;
        public VehicleFactory.Spec Spec => spec;
        /// <summary>The driver is braking: pedal against the motion, or the handbrake (VehicleFX lights the tail lamps).</summary>
        public bool Braking { get; private set; }
        public float SpeedKmh => Speed * 3.6f;

        VehicleFactory.Spec spec;
        Rigidbody rb;
        Vector3[] rest;
        float[] wheelOffset, spin;
        float steer, flipTimer, sleepTimer;
        PlayerController driver;
        const float TravelUp = 0.25f, TravelDown = 0.12f;
        int groundMask;

        public override string Prompt => driver ? "Gaadi se utro" : (type == CarType.Bus ? "Bus chalao" : "Gaadi chalao");
        public override bool CanInteract(PlayerController p) => !driver && Driving == null && !ai && !wrecked;
        public override Vector3 Point => transform.TransformPoint(new Vector3(spec.width * 0.5f + 0.3f, 0.5f, 0f));

        bool initialised;

        public void Setup(CarType t, Transform[] w, Transform s, Light[] lights)
        {
            type = t;
            wheels = w;
            seat = s;
            headlights = lights;
            Init();
        }

        void Start() => Init();

        void Init()
        {
            if (initialised || wheels == null || wheels.Length < 4) return;
            initialised = true;
            spec = VehicleFactory.GetSpec(type);
            var info = GetComponent<VehicleInfo>();
            if (info) { spec.wheelRadius = info.wheelRadius; spec.width = info.width; spec.length = info.length; spec.height = info.height; }
            radius = spec.width * 0.5f + 2f;
            rb = GetComponent<Rigidbody>();
            rb.mass = spec.mass;
            rb.centerOfMass = new Vector3(0, type == CarType.Bus ? 0.8f : 0.35f, 0.05f);
            rb.linearDamping = 0.02f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;   // parked
            rest = new Vector3[4];
            wheelOffset = new float[4];
            spin = new float[4];
            for (int i = 0; i < 4; i++) rest[i] = wheels[i].localPosition;
            groundMask = ~((1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9) | (1 << 2) | (1 << 4));
        }

        // ------------------------------------------------------------------ enter / exit
        public override void Interact(PlayerController p)
        {
            if (!initialised) return;
            if (!driver)
            {
                driver = p;
                Driving = this;
                rb.isKinematic = false;
                rb.WakeUp();
                p.EnterVehicle(this);
                if (AudioManager.Instance) AudioManager.Instance.CarEnter(transform.position);
                if (GetComponent<VehicleInfo>() && p.Rig) p.Rig.gameObject.SetActive(false);   // library models: the driver is inside, unseen
                GameEvents.ShowToast("W/S: race-brake, A/D: steering, Space: handbrake, E: utro", 3.5f);
                return;
            }
            Exit();
        }

        void Exit()
        {
            var p = driver;
            if (!p) return;
            Vector3 right = transform.right;
            Vector3[] tries =
            {
                transform.position + right * (spec.width * 0.5f + 0.9f) + Vector3.up * 0.3f,
                transform.position - right * (spec.width * 0.5f + 0.9f) + Vector3.up * 0.3f,
                transform.position + Vector3.up * (spec.height + 0.4f),
            };
            Vector3 at = tries[2];
            foreach (var t in tries)
                if (!Physics.CheckCapsule(t + Vector3.up * 0.4f, t + Vector3.up * 1.5f, 0.3f, ~((1 << 8) | (1 << 2)), QueryTriggerInteraction.Ignore)) { at = t; break; }
            driver = null;
            Driving = null;
            if (p.Rig && !p.Rig.gameObject.activeSelf) p.Rig.gameObject.SetActive(true);
            if (AudioManager.Instance) AudioManager.Instance.CarExit(transform.position);
            p.ExitVehicle(at);
            sleepTimer = 0f;
        }

        public void ForceExit() { if (driver) Exit(); }

        /// <summary>A car that has gone through the ground (or is flying off faster than any car can) is put back on top,
        /// at rest: never a black screen with the speed climbing.</summary>
        void Rescue()
        {
            var t = Terrain.activeTerrain;
            if (!t) return;
            Vector3 p = rb.position;
            float g = t.SampleHeight(p) + t.transform.position.y;
            bool under = p.y < g - 1.5f;
            bool wild = rb.linearVelocity.sqrMagnitude > 75f * 75f;
            if (!under && !wild) return;
            float top = g;
            if (Physics.Raycast(new Vector3(p.x, g + 30f, p.z), Vector3.down, out var hit, 40f, groundMask, QueryTriggerInteraction.Ignore)) top = Mathf.Max(top, hit.point.y);
            rb.position = new Vector3(p.x, top + 0.6f, p.z);
            Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            rb.rotation = Quaternion.LookRotation(f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward, Vector3.up);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Debug.Log($"[Nainital] car rescued ({(under ? "under the ground" : "too fast")}) at {rb.position:F1}");
        }

        // ------------------------------------------------------------------ physics
        void FixedUpdate()
        {
            if (!initialised || rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            Rescue();
            bool player = driver != null && !GameInput.UiBlocking && !wrecked;
            bool driving = player || (ai && !wrecked);
            Vector2 mv = player ? GameInput.Move : ai && !wrecked ? new Vector2(0f, aiThrottle) : Vector2.zero;
            bool handbrake = ai && !wrecked ? aiHandbrake > 0.5f : (!driving || GameInput.JumpHeld);
            Vector3 up = transform.up;
            float fwdSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            float speed01 = Mathf.Clamp01(Mathf.Abs(fwdSpeed) / spec.maxSpeed);
            Braking = driving && ((Mathf.Abs(mv.y) > 0.05f && Mathf.Sign(mv.y) != Mathf.Sign(fwdSpeed) && Mathf.Abs(fwdSpeed) > 0.5f) || (player ? GameInput.JumpHeld : handbrake));
            float steerTarget = ai && !player ? Mathf.Clamp(aiSteer, -spec.steer, spec.steer) : mv.x * Mathf.Lerp(spec.steer, spec.steer * 0.35f, speed01);
            steer = Mathf.MoveTowards(steer, steerTarget, 110f * dt);

            float massPerWheel = spec.mass * 0.25f;
            float springK = massPerWheel * 9.81f / TravelDown;
            float damper = 2f * 0.45f * Mathf.Sqrt(springK * massPerWheel);
            int grounded = 0;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                Vector3 origin = transform.TransformPoint(rest[i] + Vector3.up * TravelUp);
                float maxLen = TravelUp + spec.wheelRadius + TravelDown;
                if (Physics.Raycast(origin, -up, out var hit, maxLen, groundMask, QueryTriggerInteraction.Ignore))
                {
                    grounded++;
                    float x = maxLen - hit.distance;
                    float vUp = Vector3.Dot(rb.GetPointVelocity(origin), up);
                    float springF = Mathf.Max(0f, x * springK - vUp * damper);
                    rb.AddForceAtPosition(up * springF, origin);

                    Quaternion sr = front ? Quaternion.AngleAxis(steer, up) : Quaternion.identity;
                    Vector3 fwd = Vector3.ProjectOnPlane(sr * transform.forward, hit.normal).normalized;
                    Vector3 right = Vector3.ProjectOnPlane(sr * transform.right, hit.normal).normalized;
                    Vector3 pv = rb.GetPointVelocity(hit.point);
                    float vLat = Vector3.Dot(pv, right), vLong = Vector3.Dot(pv, fwd);
                    float mu = (!front && handbrake && driving) ? 0.38f : 1.15f;
                    float maxF = mu * springF;

                    float latF = -vLat * massPerWheel / 0.09f;
                    float longF = -vLong * massPerWheel * 0.04f;                       // rolling resistance
                    float throttle = mv.y;
                    if (Mathf.Abs(throttle) > 0.05f)
                    {
                        bool braking = Mathf.Sign(throttle) != Mathf.Sign(fwdSpeed) && Mathf.Abs(fwdSpeed) > 1f;
                        if (braking) longF += -Mathf.Sign(vLong) * massPerWheel * 9f;
                        else
                        {
                            float limit = throttle > 0 ? spec.maxSpeed : spec.maxSpeed * 0.3f;
                            float power = 1f - Mathf.Clamp01(Mathf.Abs(fwdSpeed) / limit);
                            longF += throttle * spec.engine * 0.25f * power;
                        }
                    }
                    if (handbrake && (!driving || !front)) longF += -Mathf.Clamp(vLong * 4f, -1f, 1f) * massPerWheel * 7f;

                    Vector2 f = Vector2.ClampMagnitude(new Vector2(latF, longF), maxF);
                    rb.AddForceAtPosition(right * f.x + fwd * f.y, hit.point + up * (spec.wheelRadius * 0.6f));
                    wheelOffset[i] = TravelUp - (hit.distance - spec.wheelRadius);
                    spin[i] += vLong / spec.wheelRadius * Mathf.Rad2Deg * dt;
                }
                else
                {
                    wheelOffset[i] = Mathf.MoveTowards(wheelOffset[i], -TravelDown, dt * 2f);
                }
            }

            if (grounded == 0) rb.AddTorque(-rb.angularVelocity * spec.mass * 0.3f);   // calmer in the air
            else rb.AddForce(-up * rb.linearVelocity.magnitude * spec.mass * 0.03f);   // a little downforce

            // auto-upright after rolling over
            if (Vector3.Dot(up, Vector3.up) < 0.35f && rb.linearVelocity.magnitude < 2.5f) flipTimer += dt; else flipTimer = 0f;
            if (flipTimer > 2f)
            {
                flipTimer = 0f;
                Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                rb.position += Vector3.up * 1.2f;
                rb.rotation = Quaternion.LookRotation(f.sqrMagnitude > 0.01f ? f : Vector3.forward, Vector3.up);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // go back to sleep when abandoned and still (an AI car never sleeps: it is driving)
            if (!driver && !ai)
            {
                sleepTimer = rb.linearVelocity.magnitude < 0.25f ? sleepTimer + dt : 0f;
                if (sleepTimer > 1.5f) rb.isKinematic = true;
            }
        }

        void Update()
        {
            if (!initialised) return;
            for (int i = 0; i < 4; i++)
            {
                if (!wheels[i]) continue;
                wheels[i].localPosition = rest[i] + Vector3.up * wheelOffset[i];
                wheels[i].localRotation = Quaternion.Euler(0, i < 2 ? steer : 0f, 0) * Quaternion.Euler(spin[i], 0, 0);
            }
            if (driver && GameInput.HornPressed) GameEvents.RaiseHorn(transform.position);
            if (headlights != null)
            {
                var dn = DayNightCycle.Instance;
                bool on = driver && dn && dn.NightFactor > 0.35f;
                foreach (var l in headlights) if (l && l.enabled != on) l.enabled = on;
            }
        }

        // ------------------------------------------------------------------ hitting people
        void OnCollisionEnter(Collision c)
        {
            if (!driver) return;
            float rel = c.relativeVelocity.magnitude;
            int layer = c.collider.gameObject.layer;
            if (rel < 4.5f || (layer != 9)) return;
            Physics.IgnoreCollision(GetComponent<Collider>(), c.collider, true);
            StartCoroutine(Restore(c.collider));
            Vector3 push = rb.linearVelocity * 0.8f + Vector3.up * 4f;
            var npc = c.collider.GetComponent<NpcWalker>();
            if (npc) npc.Knock(push);
            var h = c.collider.GetComponent<Health>();
            if (h) h.Damage(rel * 4f, transform.position, gameObject);
            var m = c.collider.GetComponent<CharacterMotor>();
            if (m && !npc) m.impulse += push;
        }

        System.Collections.IEnumerator Restore(Collider other)
        {
            yield return new WaitForSeconds(1.5f);
            if (other) Physics.IgnoreCollision(GetComponent<Collider>(), other, false);
        }
    }
}

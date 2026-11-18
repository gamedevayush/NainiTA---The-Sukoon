using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A constable on a Splendor with a small red-blue light bar and the siren (1 star: "bike se aayega"; also one of the
    /// 3-star units). Rides along the roads to the player; close to a player on foot he puts the bike on its stand and
    /// goes on foot (PoliceOfficer); the player off in a car, he runs back, rides after it and fires one-handed when
    /// close. Wanted level gone: he rides away and is gone out of sight. Knocked off by the player's car: the bike
    /// tumbles, he is thrown (hurt, maybe killed: a cop).
    /// </summary>
    [RequireComponent(typeof(BikeRide))]
    public class PoliceBike : PoliceRide
    {
        public enum Mode { Ride, Parked, Leave }
        public Mode Current { get; private set; } = Mode.Ride;

        BikeRide bike;
        readonly List<Vector3> route = new List<Vector3>();
        int ri;
        float replan, think, fireCd = 1f, modeT, stuckT;
        Vector3 goal, leaveGoal;
        static int obstacleMask = -1;

        public override bool Bike => true;
        public override Transform SeatParent => bike && bike.model ? bike.model : transform;
        public override bool Usable => bike && !bike.Crashed;

        public void Begin()
        {
            bike = GetComponent<BikeRide>();
            if (obstacleMask < 0) obstacleMask = (1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9);
            bike.OnCrash += Thrown;
        }

        public override Vector3 SeatLocal(int seat) => bike.SeatLocal;
        public override Vector3 Door(int seat) => transform.TransformPoint(new Vector3(0.75f, 0.1f, -0.2f));

        void Set(Mode m)
        {
            Current = m;
            modeT = 0f;
            if (lights) { lights.lights = m != Mode.Leave; lights.siren = m == Mode.Ride; }
            if (m == Mode.Parked) bike.Park();
            if (m == Mode.Leave) { leaveGoal = FarPoint(); replan = 0f; }
        }

        Vector3 FarPoint()
        {
            var p = PlayerController.Instance;
            Vector3 c = p ? p.transform.position : transform.position;
            Vector3 away = transform.position - c;
            away.y = 0f;
            if (away.sqrMagnitude < 1f) away = transform.forward;
            int k = RoadRoute.ReadyAny() ? RoadRoute.Nearest(transform.position + away.normalized * 240f, 200f) : -1;
            return k >= 0 ? RoadRoute.Pos(k) : transform.position + away.normalized * 240f;
        }

        void Update()
        {
            if (!bike) return;
            float dt = Time.deltaTime;
            modeT += dt;
            fireCd -= dt;
            var ws = WantedSystem.Instance;
            int stars = ws ? ws.Stars : 0;
            var p = PlayerController.Instance;
            if (!p) return;
            if (bike.Crashed || Living == 0) { if (lights) lights.siren = false; Gone(150f, 30f); return; }
            bool pInCar = p.CurrentMode == PlayerController.Mode.Vehicle && p.Car;
            Vector3 pp = pInCar ? p.Car.transform.position : p.transform.position;
            float dist = Vector3.Distance(transform.position, pp);

            switch (Current)
            {
                case Mode.Ride:
                    if (stars == 0) { Set(Mode.Leave); break; }
                    goal = pp;
                    if (dist < 70f && pathClear(pp)) ws.Spotted();
                    bool blockedLong = stuckT > 2.5f && dist < 70f;
                    bool carStopped = pInCar && p.Car.Speed < 1.5f && dist < 15f;   // the player's car stands: off the bike
                    if (!pInCar && p.CurrentMode != PlayerController.Mode.Dead && ((dist < 13f && bike.Speed < 4f) || blockedLong) && AnyAboard || carStopped && AnyAboard && bike.Speed < 4f)
                    {
                        Set(Mode.Parked);
                        Unload();
                        break;
                    }
                    if (pInCar && stars >= 2 && fireCd <= 0f && bike.Speed > 1.5f && p.Car.Speed > 2f)
                    {
                        fireCd = 0.5f;
                        var s = Shooter;
                        if (s && dist < 20f && s.SeatSees(p, out _))
                        {
                            s.Fire(p, stars, true);
                            fireCd = stars >= 3 ? Random.Range(0.9f, 1.5f) : Random.Range(1.3f, 2.1f);
                        }
                    }
                    break;
                case Mode.Parked:
                    if (modeT > 1.5f && AllAboard) Set(stars > 0 ? Mode.Ride : Mode.Leave);
                    break;
                case Mode.Leave:
                    if (stars > 0 && dist < 120f && AllAboard) { Set(Mode.Ride); break; }
                    goal = leaveGoal;
                    Gone(130f, 50f);
                    break;
            }
        }

        void FixedUpdate()
        {
            if (!bike || bike.Crashed) return;
            if (Current == Mode.Parked || !AllAboard) return;
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            replan -= dt;
            if (replan <= 0f)
            {
                replan = Random.Range(2.4f, 3.2f);
                ri = 0;
                if (!RoadRoute.ReadyAny() || !RoadRoute.Find(pos, goal, route)) route.Clear();
            }
            while (ri < route.Count - 1)
            {
                Vector3 to = route[ri] - pos;
                to.y = 0f;
                if (to.magnitude < 3f || (to.magnitude < 9f && Vector3.Dot(to, bike.Forward) < 0f)) ri++;
                else break;
            }
            Vector3 toGoal = goal - pos;
            toGoal.y = 0f;
            bool direct = route.Count < 2 || toGoal.magnitude < 28f || ri >= route.Count - 1;
            Vector3 aim = direct ? goal : route[Mathf.Min(ri + 1, route.Count - 1)];
            float target = Mathf.Min(15f, Mathf.Max(Current == Mode.Ride ? 0f : 2f, (toGoal.magnitude - (Current == Mode.Ride ? 9f : 0f)) * 0.55f));   // pulls up ~9 m short of the player
            Vector3 ahead = aim - pos;
            ahead.y = 0f;
            target = Mathf.Lerp(target, 4f, Mathf.Clamp01(Vector3.Angle(bike.Forward, ahead) / 80f));
            float speed = bike.Speed;
            bool blockedAhead = false;
            Vector3 from = pos + Vector3.up * 0.8f + bike.Forward * (bike.length * 0.5f + 0.2f);
            if (Physics.SphereCast(from, 0.45f, bike.Forward, out var hit, 4f + speed, obstacleMask, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(transform))
            {
                bool playerThing = hit.collider.gameObject.layer == 8 || (CarController.Driving && hit.collider.transform.IsChildOf(CarController.Driving.transform));
                if (!(playerThing && Current == Mode.Ride && hit.distance > 2.5f))
                {
                    target = hit.distance < 2.5f ? 0f : Mathf.Min(target, (hit.distance - 2f) * 0.8f);
                    blockedAhead = !playerThing;
                }
            }
            bike.Drive(aim, target, dt);
            if (speed < 0.3f && (target > 1f || blockedAhead)) stuckT += dt; else stuckT = 0f;
            if (stuckT > 6f && !Seen() && route.Count > ri + 3)   // a jam: hop on along the route out of sight
            {
                stuckT = 0f;
                int j = Mathf.Min(route.Count - 1, ri + 4);
                bike.Place(route[j], route[Mathf.Min(route.Count - 1, j + 1)] - route[j]);
                ri = j;
            }
        }

        static int losMask = -1;
        float lookT;
        bool losNow;

        /// <summary>The rider can see the player (checked twice a second).</summary>
        bool pathClear(Vector3 pp)
        {
            lookT -= Time.deltaTime;
            if (lookT > 0f) return losNow;
            lookT = 0.5f;
            if (losMask < 0) losMask = ~((1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer) | (1 << 2) | (1 << 4));
            losNow = !Physics.Linecast(transform.position + Vector3.up * 1.6f, pp + Vector3.up * 1.2f, losMask, QueryTriggerInteraction.Ignore);
            return losNow;
        }

        bool Seen()
        {
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            return vp.z > 0f && vp.z < 120f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }

        void Gone(float far, float maxT)
        {
            var p = PlayerController.Instance;
            float d = p ? Vector3.Distance(p.transform.position, transform.position) : 999f;
            if (d > far && (!Seen() || modeT > maxT)) Destroy(gameObject);
        }

        /// <summary>Knocked off by the player's car.</summary>
        void Thrown(Vector3 carVelocity, GameObject by)
        {
            if (lights) { lights.siren = false; lights.lights = false; }
            foreach (var c in crew.ToArray())
            {
                if (!c || c.Dead || !c.Seated) continue;
                c.GetOut();
                var m = c.GetComponent<CharacterMotor>();
                if (m) m.impulse += carVelocity * 0.6f + Vector3.up * 4f;
                var h = c.GetComponent<Health>();
                if (h) h.Damage(carVelocity.magnitude * 4f, transform.position, by);
            }
        }
    }
}

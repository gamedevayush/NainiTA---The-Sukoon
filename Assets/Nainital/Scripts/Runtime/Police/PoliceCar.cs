using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The police Bolero (white, "UTTARAKHAND POLICE" on the sides, red-blue light bar, siren) with two constables
    /// (2-3 stars). Chases the player along the roads; when the player is on foot and close it pulls up and both men get
    /// out; when the player drives off they run back, get in and chase again. From the car the passenger fires at a
    /// fleeing player within ~25 m. At an accident (Scene) it stops by the body and one man stands guard until the
    /// ambulance has gone. Wanted level gone: lights off, it drives away and is gone once out of sight.
    /// </summary>
    [RequireComponent(typeof(RouteDriver))]
    public class PoliceCar : PoliceRide
    {
        public enum Mode { Chase, Deployed, Scene, Leave }
        public Mode Current { get; private set; } = Mode.Chase;
        public Vector3 scenePoint;
        public bool sceneDone;

        RouteDriver drive;
        CarController car;
        VehicleInfo info;
        VehicleDamage damage;
        float think, fireCd = 1.5f, modeT, sirenT, lookT, bestLeft = float.MaxValue, progressT, boxedT;
        bool unloaded;
        static int losMask = -1;
        Vector3 leaveGoal;

        public override bool Usable => car && !car.wrecked && (!damage || !damage.Burning);
        public RouteDriver Driver => drive;

        public void Begin(Mode m, Vector3 point)
        {
            drive = GetComponent<RouteDriver>();
            car = GetComponent<CarController>();
            info = GetComponent<VehicleInfo>();
            damage = GetComponent<VehicleDamage>();
            drive.Begin();
            drive.cruise = 16f;
            drive.offRoad = true;
            drive.ram = true;
            scenePoint = point;
            Set(m);
        }

        void Set(Mode m)
        {
            Current = m;
            modeT = 0f;
            switch (m)
            {
                case Mode.Chase:
                    drive.stop = false; drive.offRoad = true; drive.ram = true; bestLeft = float.MaxValue; progressT = 0f;
                    foreach (var c in crew) if (c) c.guarding = false;   // a scene car called to a chase: nobody stays on guard
                    break; drive.arriveDist = 6f; if (lights) { lights.lights = lights.siren = true; } break;
                case Mode.Deployed: drive.stop = true; if (lights) lights.lights = true; sirenT = 2.5f; unloaded = false; break;
                case Mode.Scene: drive.stop = false; drive.offRoad = false; drive.arriveDist = 9f; drive.SetGoal(scenePoint, true); if (lights) { lights.lights = lights.siren = true; } break;
                case Mode.Leave:
                    drive.stop = false;
                    drive.offRoad = false;
                    drive.ram = false;
                    drive.arriveDist = 10f;
                    if (lights) { lights.siren = false; lights.lights = false; }
                    leaveGoal = FarPoint();
                    drive.SetGoal(leaveGoal, true);
                    break;
            }
        }

        Vector3 FarPoint()
        {
            var p = PlayerController.Instance;
            Vector3 c = p ? p.transform.position : transform.position;
            Vector3 away = transform.position - c;
            away.y = 0f;
            if (away.sqrMagnitude < 1f) away = transform.forward;
            int k = RoadRoute.ReadyAny() ? RoadRoute.Nearest(transform.position + away.normalized * 260f, 200f) : -1;
            return k >= 0 ? RoadRoute.Pos(k) : transform.position + away.normalized * 260f;
        }

        public override Vector3 SeatLocal(int seat) => VehicleSeats.Root(gameObject, seat);

        Vector3 OldSeatLocal(int seat)
        {
            float W = info ? info.width : 1.8f, H = info ? info.height : 1.9f, R = info ? info.wheelRadius : 0.36f;
            float hips = R + (H > 1.7f ? 0.36f : 0.22f);
            return new Vector3((seat == 0 ? 1f : -1f) * W * 0.22f, hips - 0.51f, 0.05f);   // right-hand drive
        }

        public override Vector3 Door(int seat)
        {
            float W = info ? info.width : 1.8f;
            return transform.TransformPoint(new Vector3((seat == 0 ? 1f : -1f) * (W * 0.5f + 0.75f), 0.2f, 0.1f));
        }

        void Update()
        {
            if (!drive) return;
            float dt = Time.deltaTime;
            modeT += dt;
            fireCd -= dt;
            think -= dt;
            var ws = WantedSystem.Instance;
            int stars = ws ? ws.Stars : 0;
            var p = PlayerController.Instance;
            if (!p) return;
            bool pInCar = p.CurrentMode == PlayerController.Mode.Vehicle && p.Car;
            Vector3 pp = pInCar ? p.Car.transform.position : p.transform.position;
            float dist = Vector3.Distance(transform.position, pp);

            if (!Usable)
            {
                if (lights) lights.siren = false;
                Unload();
                return;
            }
            if (Living == 0) { if (lights) lights.siren = false; drive.stop = true; Gone(dist, 40f); return; }

            switch (Current)
            {
                case Mode.Chase:
                    if (stars == 0) { Set(sceneDone || scenePoint == Vector3.zero ? Mode.Leave : Mode.Scene); break; }
                    if (think <= 0f) { think = 0.4f; drive.SetGoal(pp); }
                    // the men in the car see the player (the chase is on: WantedSystem.Engaged)
                    lookT -= dt;
                    if (lookT <= 0f)
                    {
                        lookT = 0.5f;
                        if (losMask < 0) losMask = ~((1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer) | (1 << 2) | (1 << 4));
                        if (dist < 70f && !Physics.Linecast(transform.position + Vector3.up * 1.7f, pp + Vector3.up * 1.2f, losMask, QueryTriggerInteraction.Ignore)) ws.Spotted();
                        // getting no nearer (held up, or the road ends): note it
                        if (drive.RouteLeft < bestLeft - 2f) { bestLeft = drive.RouteLeft; progressT = 0f; } else progressT += 0.5f;
                    }
                    // the player on foot and close, or the car is stuck / gets no nearer: stop, everybody out
                    bool onFoot = !pInCar && p.CurrentMode != PlayerController.Mode.Dead;
                    if (onFoot && (dist < 20f || (dist < 45f && drive.Arrived) || (dist < 75f && (drive.Blocked > 3f || progressT > 6f))))
                    {
                        Set(Mode.Deployed);
                        break;
                    }
                    // the player's car has stopped close by: out, and get him out of it
                    float pv = pInCar ? p.Car.Speed : 99f;
                    boxedT = pInCar && pv < 1.5f && dist < 14f ? boxedT + dt : 0f;
                    if (boxedT > 2.5f) { Set(Mode.Deployed); break; }
                    if (pInCar && stars >= 2 && fireCd <= 0f && pv > 2f && Mathf.Abs(drive.Speed) > 1.5f) WindowShot(p, stars, dist);
                    break;

                case Mode.Deployed:
                    if (!unloaded && modeT > 0.6f && Mathf.Abs(drive.Speed) < 1.5f) { Unload(); unloaded = true; }   // once: back in = staying in
                    if (stars == 0 && (sceneDone || scenePoint == Vector3.zero)) foreach (var c in crew) if (c) c.guarding = false;
                    sirenT -= dt;
                    if (lights && sirenT <= 0f) lights.siren = false;
                    // everyone back in: chase again, or go
                    if (modeT > 2f && AllAboard)
                        Set(stars > 0 ? Mode.Chase : sceneDone || scenePoint == Vector3.zero ? Mode.Leave : Mode.Scene);
                    break;

                case Mode.Scene:
                    if (stars > 0 && dist < 120f) { Set(Mode.Chase); break; }
                    if (sceneDone && AllAboard) { Set(Mode.Leave); break; }
                    if (!sceneDone && !drive.stop && Vector3.Distance(transform.position, scenePoint) < 35f && drive.Blocked > 2.5f) drive.stop = true;   // held-up traffic: walk the rest
                    if ((drive.Arrived || drive.stop) && !sceneDone)
                    {
                        if (lights) lights.siren = false;
                        // one man out to stand by the body, facing the road
                        foreach (var c in crew)
                            if (c && !c.Dead && c.Seated && c.seat == 1 && !c.guarding)
                            {
                                c.guarding = true;
                                Vector3 side = scenePoint - transform.position;
                                side.y = 0f;
                                c.guardSpot = scenePoint - (side.sqrMagnitude > 0.01f ? side.normalized : transform.forward) * 2.2f;
                                c.guardFace = -side;
                                c.GetOut();
                            }
                    }
                    if (sceneDone)
                        foreach (var c in crew)
                            if (c && !c.Dead && c.guarding) { c.guarding = false; }   // he walks back (Return)
                    break;

                case Mode.Leave:
                    if (stars > 0 && dist < 150f && AllAboard) { Set(Mode.Chase); break; }
                    if (!AllAboard) { drive.stop = true; break; }
                    drive.stop = false;
                    Gone(dist, 150f);
                    if (drive.Arrived) leaveGoal = FarPoint();
                    break;
            }
        }

        /// <summary>Remove the car once it is far and out of sight (or far and gone a long while).</summary>
        void Gone(float dist, float far)
        {
            if (dist < far) return;
            var cam = Camera.main;
            Vector3 vp = cam ? cam.WorldToViewportPoint(transform.position) : Vector3.back;
            bool seen = vp.z > 0f && vp.z < 200f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
            if (!seen || modeT > 60f) Destroy(gameObject);
        }

        /// <summary>The passenger fires at the player's car through the window.</summary>
        void WindowShot(PlayerController p, int stars, float dist)
        {
            fireCd = 0.5f;   // look again soon
            if (dist > 26f) return;
            var s = Shooter;
            if (!s || !s.SeatSees(p, out _)) return;
            s.Fire(p, stars, true);
            fireCd = stars >= 3 ? Random.Range(0.8f, 1.4f) : Random.Range(1.2f, 2.0f);
        }
    }
}

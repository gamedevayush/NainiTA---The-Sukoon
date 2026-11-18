using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A Uttarakhand Police constable (CharacterLibrary police_1 / police_2), on foot or in his vehicle.
    /// What he does follows the wanted level (WantedSystem):
    /// 1 star: runs up (or rides up), points his pistol at the player from a few metres and holds it there. No shooting.
    /// 2 stars: closes in; face to face with a player standing still he arrests (BUSTED). If the player runs, drives,
    ///          fights back or has hurt a policeman, he shoots.
    /// 3 stars: shoots on sight.
    /// The player in a car far off: back to his own vehicle to chase. Wanted level gone: back to the vehicle and away, or
    /// (came on foot) walks off and is gone. At an accident with no one wanted he stands guard by the body.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor), typeof(Health))]
    public class PoliceOfficer : MonoBehaviour
    {
        public static readonly List<PoliceOfficer> All = new List<PoliceOfficer>();

        public enum State { Seated, Respond, Aim, Arrest, Shoot, Return, Leave, Guard, Dead }
        public State Current { get; private set; } = State.Respond;
        public PoliceRide ride;
        public int seat;
        public string displayName = "Constable";
        public Vector3 guardSpot, guardFace;
        public bool guarding;

        public bool Dead => health && health.IsDead;
        public bool Seated => Current == State.Seated;
        public HumanoidRig Rig => rig;

        CharacterMotor motor;
        Health health;
        HumanoidRig rig;
        Pistol gun;
        CharacterController cc;
        GameObject lastHurtBy;
        float scan, fireCd, arrestT, stateT, stuckT, shoutCd, lostT, leaveT;
        bool los, chaseReturn;
        float dist = 999f, pSpeed;
        Vector3 toP, pVel, lastPPos, lastPos;
        static float scareAt, nextAnyShot;   // all the police together fire at most one shot each 0.35 s
        static readonly Collider[] near = new Collider[24];
        static int losMask = -1, shotMask = -1;

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            health = GetComponent<Health>();
            cc = GetComponent<CharacterController>();
            rig = GetComponentInChildren<HumanoidRig>(true);
            gun = GetComponent<Pistol>();
            if (!gun) gun = Pistol.Give(gameObject, rig);
            health.Damaged += OnHurt;
            health.Died += OnDied;
            if (losMask < 0)
            {
                losMask = ~((1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer) | (1 << 2) | (1 << 4));
                shotMask = ~((1 << 2) | (1 << 4));
            }
            scan = Random.Range(0f, 0.25f);
            fireCd = Random.Range(0.6f, 1.4f);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Set(State s)
        {
            if (Current == s) return;
            Current = s;
            stateT = 0f;
            arrestT = 0f;
            lostT = 0f;
            if (s != State.Return) chaseReturn = false;
        }

        // ------------------------------------------------------------------ in and out of the vehicle
        public void SitIn(PoliceRide r, int s)
        {
            ride = r;
            seat = s;
            if (!r.crew.Contains(this)) r.crew.Add(this);
            Set(State.Seated);
            if (!cc) cc = GetComponent<CharacterController>();
            cc.enabled = false;
            transform.SetParent(r.SeatParent, false);
            transform.localPosition = r.SeatLocal(s);
            transform.localRotation = Quaternion.identity;
            if (!rig) rig = GetComponentInChildren<HumanoidRig>(true);
            rig.seated = !r.Bike;
            rig.driving = !r.Bike && s == 0;
            rig.riding = r.Bike;
            rig.aiming = false;
            rig.speed = 0f;
            if (gun) gun.drawn = false;
        }

        public void GetOut()
        {
            if (!ride) return;
            Vector3 door = ride.Door(seat);
            Vector3 away = door - ride.transform.position;
            away.y = 0f;
            transform.SetParent(PoliceManager.Instance ? PoliceManager.Instance.transform : null, true);
            transform.SetPositionAndRotation(Ground(door), Quaternion.LookRotation(away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward));
            Physics.SyncTransforms();
            cc.enabled = true;
            rig.seated = rig.riding = rig.driving = false;
            motor.velocity = Vector3.zero;
            Set(guarding ? State.Guard : State.Respond);
        }

        static Vector3 Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            float top = t ? t.SampleHeight(p) + t.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, top) + 2f, p.z), Vector3.down, out var hit, 6f, losMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return new Vector3(p.x, Mathf.Max(p.y, top) + 0.05f, p.z);
        }

        // ------------------------------------------------------------------ hurt / killed
        void OnHurt(float amount, Vector3 from, GameObject src)
        {
            lastHurtBy = src;   // first: a killing blow has already set Health to 0
            if (Dead) return;
            if (WantedSystem.ByPlayer(src))
            {
                var ws = WantedSystem.Ensure();
                ws.Assault(transform.position, true);
                if (!Seated) Set(State.Shoot);
            }
            if (rig && !Seated) rig.PlayHit();
        }

        void OnDied()
        {
            bool wasSeated = Seated;
            Current = State.Dead;
            if (wasSeated && ride)
            {
                // shot or crushed in the seat: he falls out beside the vehicle
                transform.SetParent(PoliceManager.Instance ? PoliceManager.Instance.transform : null, true);
                transform.position = Ground(ride.Door(seat));
                cc.enabled = true;
            }
            if (ride) ride.crew.Remove(this);
            rig.seated = rig.riding = rig.aiming = false;
            rig.dead = true;
            if (gun) gun.drawn = false;
            if (WantedSystem.ByPlayer(lastHurtBy)) WantedSystem.Ensure().Kill(transform.position, true);
            GameEvents.ShowToast("Police wala gir gaya!", 2f);
            IncidentManager.Report(gameObject, rig, () => { if (this) Destroy(gameObject); });
        }

        // ------------------------------------------------------------------ senses
        void Sense(PlayerController p, float since)
        {
            Vector3 pp = PlayerPos(p);
            toP = pp - transform.position;
            toP.y = 0f;
            dist = toP.magnitude;
            if (since > 0f)
            {
                pVel = (pp - lastPPos) / since;
                pVel.y = 0f;
                pSpeed = pVel.magnitude;
            }
            lastPPos = pp;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            los = dist < 90f && !Physics.Linecast(eye, pp + Vector3.up * 1.2f, losMask, QueryTriggerInteraction.Ignore);
        }

        static Vector3 PlayerPos(PlayerController p) => p.CurrentMode == PlayerController.Mode.Vehicle && p.Car ? p.Car.transform.position : p.transform.position;

        bool ShouldShoot(int stars, bool pInCar, PlayerController p, WantedSystem ws)
        {
            if (stars >= 3) return true;
            if (stars < 2) return false;
            if (pInCar) return stars >= 3 || pSpeed > 2f;   // 2 stars and the car has stopped: "get out" first (Arrest)
            bool fleeing = pSpeed > 3.2f && Vector3.Dot(pVel, toP) > 0f;
            if (fleeing && dist > 5f) return true;
            if (Time.time - ws.LastCopAssault < 8f) return true;
            var pc = p.GetComponent<Combatant>();
            return pc && pc.IsAttacking && dist < 4f;
        }

        // ------------------------------------------------------------------ think + move
        void Update()
        {
            if (!rig) return;
            float dt = Time.deltaTime;
            if (Dead)
            {
                rig.dead = true;
                rig.speed = 0f;
                rig.aiming = false;
                if (cc && cc.enabled) motor.Move(Vector3.zero, 20f, dt);
                return;
            }
            if (Seated)
            {
                rig.speed = 0f;
                rig.grounded = true;
                rig.aiming = false;
                return;
            }

            var ws = WantedSystem.Instance;
            int stars = ws ? ws.Stars : 0;
            var p = PlayerController.Instance;
            bool pValid = p && p.CurrentMode != PlayerController.Mode.Dead;
            scan -= dt;
            if (scan <= 0f && p) { Sense(p, 0.25f - scan); scan = 0.25f; }
            if (los && pValid && stars > 0 && dist < 70f) ws.Spotted();
            stateT += dt;
            fireCd -= dt;
            shoutCd -= dt;
            bool pInCar = p && p.CurrentMode == PlayerController.Mode.Vehicle;

            // what the wanted level asks for
            if (Current == State.Guard && !guarding) Set(ride && ride.Usable ? State.Return : State.Leave);   // the accident is cleared
            if (stars == 0 || !pValid)
            {
                if (Current != State.Guard && Current != State.Return && Current != State.Leave)
                {
                    if (guarding) Set(State.Guard);
                    else if (ride && ride.Usable && (ride.Door(seat) - transform.position).sqrMagnitude < 90f * 90f) Set(State.Return);
                    else Set(State.Leave);
                    if (stars == 0 && pValid && dist < 25f && Random.value < 0.5f) Shout("\"Theek hai, jao. Dobara mat karna!\"");
                }
            }
            else if (Current == State.Guard || Current == State.Leave || (Current == State.Return && !chaseReturn))
            {
                if (stars >= 2 || dist < 50f) Set(State.Respond);
            }
            else if (Current == State.Aim && stars >= 2) Set(State.Respond);

            Vector3 desired = Vector3.zero;
            Vector3 face = transform.forward;
            bool aim = false;
            const float run = 4.7f;
            Vector3 dirP = dist > 0.01f ? toP / dist : transform.forward;

            switch (Current)
            {
                case State.Respond:
                    if (stars >= 2 && pInCar && (dist > 45f || (pSpeed > 3f && dist > 15f)) && ride && ride.Usable) { Set(State.Return); chaseReturn = true; break; }
                    if (stars == 1)
                    {
                        if (dist < 9f && los) { Set(State.Aim); Shout("\"Ruk ja! Haath upar!\""); break; }
                    }
                    else
                    {
                        bool shoot = ShouldShoot(stars, pInCar, p, ws);
                        if (los && dist < 32f && shoot) { Set(State.Shoot); Shout(stars >= 3 ? "\"Goli maar do!\"" : "\"Ruk ja, warna goli chala dunga!\""); break; }
                        if (!shoot && dist < 7f && (!pInCar || pSpeed < 1.2f)) { Set(State.Arrest); Shout(pInCar ? "\"Gaadi se bahar niklo! Haath upar!\"" : "\"Haath upar! Hilna mat!\""); break; }
                        aim = dist < 14f;
                    }
                    desired = dirP * run;
                    face = toP;
                    break;

                case State.Aim:   // 1 star: the warning at gunpoint
                    aim = true;
                    face = toP;
                    if (los) ws.Aimed();
                    if (dist > 14f) desired = dirP * 2.4f;
                    else if (dist < 3.5f) desired = -dirP * 1.2f;
                    if (dist > 24f) Set(State.Respond);
                    break;

                case State.Arrest:
                    aim = true;
                    face = toP;
                    if (pInCar ? pSpeed > 2f : ShouldShoot(stars, pInCar, p, ws)) { Set(State.Shoot); break; }
                    if (pInCar && stateT > 4.5f && dist < 9f) { Set(State.Shoot); Shout("\"Bahar nahi niklega? Goli chalao!\""); break; }
                    float reach = pInCar ? 6f : 2.4f;   // a car: stands back a little at its door, gun on the driver
                    if (dist > reach - 0.6f) desired = dirP * (dist > 5f ? 3.8f : 1.5f);
                    bool held = p.CurrentMode == PlayerController.Mode.Normal;
                    if (dist < reach && pSpeed < 1.6f && held)
                    {
                        arrestT += dt;
                        if (arrestT > 1.5f) { ws.Busted(); Set(State.Respond); }
                    }
                    else arrestT = Mathf.Max(0f, arrestT - dt);
                    if (dist > 12f) Set(State.Respond);
                    break;

                case State.Shoot:
                    aim = true;
                    face = toP;
                    if (!los) { lostT += dt; if (lostT > 2.5f) { Set(State.Respond); break; } }
                    else lostT = 0f;
                    if (dist > 22f) desired = dirP * 3.4f;
                    else if (dist < 4.5f && !pInCar) desired = -dirP * 1.4f;
                    if (los && fireCd <= 0f && stateT > 0.8f && Time.time >= nextAnyShot) Fire(p, stars, pInCar);
                    if (stars == 2 && !pInCar && dist < 9f && pSpeed < 0.8f && !ShouldShoot(stars, pInCar, p, ws)) { Set(State.Arrest); break; }
                    if (stars >= 2 && pInCar && (dist > 45f || (pSpeed > 3f && dist > 15f)) && ride && ride.Usable) { Set(State.Return); chaseReturn = true; }
                    break;

                case State.Return:
                    if (!ride || !ride.Usable) { Set(stars > 0 ? State.Respond : State.Leave); break; }
                    Vector3 to = ride.Door(seat) - transform.position;
                    to.y = 0f;
                    if (to.magnitude < 1.5f || (stateT > 25f && !OnScreen()) || (dist > 120f && !OnScreen() && stateT > 4f)) { SitIn(ride, seat); return; }
                    desired = to.normalized * (stars > 0 ? run : 1.7f);
                    face = to;
                    if (stars > 0 && !pInCar && dist < 20f) Set(State.Respond);
                    break;

                case State.Leave:   // came on foot: walks off and is gone
                    leaveT += dt;
                    desired = (dist > 0.01f ? -dirP : transform.forward) * 1.6f;
                    face = desired;
                    if (leaveT > 8f && (dist > 40f || !OnScreen())) { Destroy(gameObject); return; }
                    break;

                case State.Guard:
                    Vector3 g = guardSpot - transform.position;
                    g.y = 0f;
                    if (g.magnitude > 1f) { desired = g.normalized * 1.6f; face = g; }
                    else if (guardFace.sqrMagnitude > 0.01f) face = guardFace;
                    break;
            }

            // never into the lake after a player who swims off
            if (desired.sqrMagnitude > 0.01f && LakeArea.WaterY > -1000f)
            {
                var t = Terrain.activeTerrain;
                Vector3 ahead = transform.position + desired.normalized * 1.4f;
                if (t && t.SampleHeight(ahead) + t.transform.position.y < LakeArea.WaterY - 0.25f) desired = Vector3.zero;
            }
            // unstick: pushing but not moving, hop sideways
            if (desired.sqrMagnitude > 1f && (transform.position - lastPos).sqrMagnitude < 0.0004f) stuckT += dt; else stuckT = 0f;
            lastPos = transform.position;
            if (stuckT > 0.7f) { desired += Vector3.Cross(Vector3.up, desired.normalized) * 3f; if (motor.Grounded) motor.velocity.y = 5f; stuckT = 0f; }

            motor.Move(desired, 14f, dt);
            motor.FaceTowards(face, aim ? 620f : 480f, dt);
            rig.speed = motor.HorizontalVelocity.magnitude;
            rig.grounded = motor.Grounded || motor.AirTime < 0.15f;
            rig.verticalSpeed = motor.velocity.y;
            rig.aiming = aim;
            if (aim && p)
            {
                Vector3 target = PlayerPos(p) + Vector3.up * (pInCar ? 1.0f : 1.2f);
                Vector3 d = target - (transform.position + Vector3.up * 1.45f);
                rig.aimPitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            }
            if (gun) gun.drawn = aim;
        }

        bool OnScreen()
        {
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 vp = cam.WorldToViewportPoint(transform.position + Vector3.up);
            return vp.z > 0f && vp.z < 120f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        void Shout(string line)
        {
            if (shoutCd > 0f) return;
            shoutCd = 6f;
            GameEvents.ShowToast(displayName + ": " + line, 2.2f);
        }

        // ------------------------------------------------------------------ the pistol
        /// <summary>Fire at the player (also used from a vehicle seat: PoliceCar / PoliceBike call it).</summary>
        public void Fire(PlayerController p, int stars, bool pInCar)
        {
            if (!p || !gun) return;
            Vector3 muzzle = gun.drawn ? gun.Muzzle : transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;
            Vector3 target = PlayerPos(p) + Vector3.up * (pInCar ? 1.0f : 1.25f);
            float d = Vector3.Distance(muzzle, target);
            float chance = Mathf.Lerp(0.45f, 0.08f, Mathf.InverseLerp(4f, 40f, d));
            if (pSpeed > 4f) chance *= 0.6f;
            if (pInCar) chance *= 0.75f;
            if (Seated) chance *= 0.7f;    // from a moving vehicle
            if (stars >= 3) chance *= 1.1f;
            nextAnyShot = Time.time + 0.35f;
            bool hit = Random.value < chance;
            Vector3 end = target;
            if (!hit)
            {
                Vector3 miss = target + Random.onUnitSphere * Random.Range(0.7f, 1.8f);
                Vector3 dir = (miss - muzzle).normalized;
                end = Physics.Raycast(muzzle, dir, out var h, 80f, shotMask, QueryTriggerInteraction.Ignore) && !h.collider.transform.IsChildOf(transform)
                    ? h.point : muzzle + dir * 80f;
            }
            gun.Shoot(end);
            if (rig && !Seated) rig.PlayShoot();
            if (hit)
            {
                p.Health.Damage(pInCar ? 5f : 7f, muzzle, gameObject);
                if (pInCar && p.Car && Random.value < 0.3f)
                {
                    var vd = p.Car.GetComponent<VehicleDamage>();
                    if (vd) vd.Hit(1);
                }
            }
            fireCd = stars >= 3 ? Random.Range(0.9f, 1.6f) : Random.Range(1.3f, 2.2f);
            Scare(muzzle);
        }

        /// <summary>Shots send the crowd running.</summary>
        static void Scare(Vector3 at)
        {
            if (Time.time - scareAt < 0.6f) return;
            scareAt = Time.time;
            int n = Physics.OverlapSphereNonAlloc(at, 26f, near, 1 << 9, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var w = near[i].GetComponent<NpcWalker>();
                if (!w) continue;
                Vector3 away = w.transform.position - at;
                away.y = 0f;
                w.Flee(away.sqrMagnitude > 0.01f ? away : Random.insideUnitSphere, Random.Range(3.5f, 6f));
            }
        }

        /// <summary>Sense + face from a vehicle seat (PoliceCar / PoliceBike: the passenger fires from the window).</summary>
        public bool SeatSees(PlayerController p, out float d)
        {
            Sense(p, 0.25f);
            d = dist;
            return los;
        }
    }
}

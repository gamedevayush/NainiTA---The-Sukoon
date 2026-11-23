using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Tourist / local: walks a path (Mall Road, sidewalks) or idles/sits at a spot (temple, bench, stall).
    /// Can be talked to; gets knocked over by careless drivers and stands up again.
    ///
    /// Street sense (user, 2026-11-11: "crowd aapas mein phans ke ruk jaate hain; gaadi aa rahi ho toh bhaag ke cross kar
    /// lenge"): walkers keep to the left of the line they follow, so people going opposite ways pass each other; they
    /// step aside for someone right in front (and walkers do not physically block each other: PedestrianManager turns off
    /// walker-walker collisions); anyone standing in the path of a moving car runs out of its way to the nearer side.
    /// At night: sent home by PedestrianManager they walk to the door of the nearest house and go in; a local at a post
    /// (homeAtNight) is gone from midnight till six.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class NpcWalker : Interactable
    {
        public Vector3[] path;
        public int index;
        public int direction = 1;
        public float speed = 1.2f;
        public string npcName = "Tourist";
        public bool sit;              // idle seated (bench / temple)
        public bool idle;             // idle standing (shopkeeper, boatman)
        public Vector3 faceDirection; // for idle NPCs
        [TextArea] public string[] lines;
        public bool homeAtNight;      // a local at a post: not there from midnight till six
        public float laneOffset = -1f;   // how far left of the path line this walker keeps (set at random if < 0)

        public bool GoingHome { get; private set; }
        /// <summary>Driving an AI car (TrafficCar): seated, no collider, carried by the car.</summary>
        public bool InCar { get; private set; }
        public bool Home { get; private set; }   // at the door: PedestrianManager takes this walker away
        /// <summary>From PedestrianManager's pool (goes back to it when its body is taken away), not a placed local.</summary>
        [System.NonSerialized] public bool pooled;
        /// <summary>The CharacterLibrary look (its id) this walker wears: PedestrianManager seats only the looks that fit a
        /// vehicle's seat (VehicleSetup.PickLook).</summary>
        [System.NonSerialized] public string lookId;
        bool onBike;
        // in a vehicle of the unbranded set: its parts and the seat's pose (VehicleParts.Sit)
        VehicleParts seatParts;
        SeatPose seatPose;
        Vector3 postPos, watchAt;
        Quaternion postRot;
        float watchTimer, shoutCd;
        bool watchClose;
        GameObject lastHurtBy;

        CharacterMotor motor;
        HumanoidRig rig;
        CharacterController cc;
        float pause, talkTimer, knockTimer, scanTimer, fleeTimer, sideTimer, homeTimer;
        Vector3 fleeDir, sideStep, homeDoor, lastDir;
        // out of a car's way: kept out of its strip until it has passed, and to the other side where a wall is in the way
        Vector3 holdDir, fleeFrom;
        float holdTimer, fleeShutT, fleeOtherT;
        bool fleeMayCross;
        /// <summary>How far in front of a traffic vehicle's middle people clear its way. A car matches the pace of someone
        /// walking ahead of it and settles 9-11 m behind (TrafficCar's gap): 7 m never reached them, and the 22 Nov 2026
        /// route test crawled up Upper Mall at 1.1 m/s for six minutes.</summary>
        const float TrafficReach = 14f;
        bool hidden;
        Health health;
        static readonly Collider[] hits = new Collider[16];

        static readonly string[] DefaultLines =
        {
            "Bhaiya, Naina Devi mandir yahan se seedha Mallital ki taraf hai.",
            "Shaam ko Mall Road pe lights dekhna, jheel mein reflection kamaal ka lagta hai!",
            "Boating Tallital aur Mallital dono jagah se hoti hai.",
            "Snow View tak ropeway jaata hai, upar se Himalaya dikhta hai.",
            "Thandi Sadak pe shaam ko walk karna, bahut shanti hai.",
            "Bada Bazar mein candles aur woollen kapde zaroor dekhna.",
            "Tiffin Top (Dorothy's Seat) tak trek karo, view mast hai.",
            "Raat mein gali-mohallon mein sambhal ke, kuch gunde ghoomte hain!",
            "Momos khaye? Mall Road pe best milte hain.",
            "Yeh jheel aankh ki shape ki hai, isliye Naini naam pada.",
            "Parking ki bahut dikkat hai yahan, gaadi Tallital pe hi laga do.",
            "Tibetan market mein sasti jackets milti hain, dekh lena.",
            "Bandaron se bach ke, chips cheen ke bhaag jaate hain!",
            "Season mein yahan itni bheed hoti hai ki Mall Road pe chalna mushkil.",
        };

        public override string Prompt => npcName + " se baat karo";

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            rig = GetComponentInChildren<HumanoidRig>();
            health = GetComponent<Health>();
            if (!health)   // everyone can be hurt (2026-11-18: police and ambulance system)
            {
                health = gameObject.AddComponent<Health>();
                health.max = health.current = 60f;
            }
            health.Damaged += OnHurt;
            health.Died += OnDied;
            postPos = transform.position;
            postRot = transform.rotation;
            cc = GetComponent<CharacterController>();
            pause = Random.Range(0f, 3f);
            if (laneOffset < 0f) laneOffset = Random.Range(0.35f, 0.9f);
            scanTimer = Random.Range(0f, 0.25f);
        }

        public override bool CanInteract(PlayerController p) => (!health || !health.IsDead) && knockTimer <= 0f && !hidden && !InCar;

        // ------------------------------------------------------------------ hurt / killed (WantedSystem, IncidentManager)
        void OnHurt(float amount, Vector3 from, GameObject src)
        {
            lastHurtBy = src;   // first: Health is already at 0 when a blow kills (Damaged fires before Died)
            if (health.IsDead) return;
            bool byPlayer = WantedSystem.ByPlayer(src);
            if (byPlayer) WantedSystem.Ensure().Assault(transform.position, false);
            if (InCar) return;
            if (rig && knockTimer <= 0f) rig.PlayHit();
            Vector3 away = transform.position - from;
            away.y = 0f;
            Flee(away.sqrMagnitude > 0.01f ? away : -transform.forward, Random.Range(4f, 7f));
            watchTimer = 0f;
            talkTimer = 0f;
            if (shoutCd <= 0f && knockTimer <= 0f)
            {
                shoutCd = 5f;
                string[] shouts = { "Bachao! Police!", "Arey maar kyun raha hai?!", "Koi police ko bulao!", "Hai Ram! Bachao!" };
                GameEvents.ShowToast(npcName + ": \"" + shouts[Random.Range(0, shouts.Length)] + "\"", 2f);
            }
        }

        void OnDied()
        {
            if (rig) { rig.dead = true; rig.speed = 0f; }
            fleeTimer = watchTimer = talkTimer = 0f;
            if (WantedSystem.ByPlayer(lastHurtBy)) WantedSystem.Ensure().Kill(transform.position, false);
            if (!InCar) IncidentManager.Report(gameObject, rig, Collected);
        }

        /// <summary>The ambulance took the body: a pooled walker goes back to the pool, a local comes back to his post
        /// in two minutes.</summary>
        void Collected()
        {
            if (!this) return;
            if (pooled && PedestrianManager.Instance) { PedestrianManager.Instance.Release(this); return; }
            gameObject.SetActive(false);
            var im = IncidentManager.Instance;
            if (im) im.Later(120f, () =>
            {
                if (!this) return;
                transform.SetParent(null, true);
                ResetState();
                transform.SetPositionAndRotation(postPos, postRot);
                gameObject.SetActive(true);
                if (cc) cc.enabled = true;
            });
        }

        /// <summary>Stop and stare at something (an accident) for a while; some walk up closer.</summary>
        public void Watch(Vector3 at, float seconds)
        {
            if (health && health.IsDead) return;
            watchAt = at;
            watchTimer = seconds;
            watchClose = Random.value < 0.35f;
        }

        /// <summary>Sit astride a two-wheeler (TrafficBike): like a car's driving seat, the riding pose.</summary>
        public void EnterBike(Transform bike, Vector3 seatLocal)
        {
            EnterCar(bike, seatLocal);
            onBike = true;
            if (rig) { rig.seated = false; rig.driving = false; rig.riding = true; }
        }

        /// <summary>Sit in a car's driving seat (PedestrianManager.MakeDriver).</summary>
        public void EnterCar(Transform car, Vector3 seatLocal)
        {
            InCar = true;
            seatParts = null;
            if (!rig) rig = GetComponentInChildren<HumanoidRig>(true);
            if (!cc) cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            transform.SetParent(car, false);
            transform.localPosition = seatLocal;
            transform.localRotation = Quaternion.identity;
            GoingHome = Home = false;
            path = null;
            sit = true;
            idle = false;
            if (rig) { rig.seated = true; rig.driving = true; rig.speed = 0f; rig.dead = false; }
        }

        /// <summary>Sit in seat n of a vehicle of the unbranded set: its pose, hands on the wheel / grips, feet on the floor /
        /// pegs, head under its roof (VehicleParts.Sit).</summary>
        public void EnterSeat(VehicleParts parts, int seat)
        {
            var s = parts ? parts.SeatData(seat) : null;
            if (s == null) return;
            InCar = true;
            if (!rig) rig = GetComponentInChildren<HumanoidRig>(true);
            if (!cc) cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            GoingHome = Home = false;
            path = null;
            sit = true;
            idle = false;
            seatParts = parts;
            seatPose = s.pose;
            onBike = s.pose == SeatPose.Ride || s.pose == SeatPose.Pillion;
            if (rig) rig.dead = false;
            parts.Sit(transform, rig, seat);
        }

        /// <summary>Standing in a moving vehicle (a bus conductor at the door): parented to it at `local` / `rot`, on his
        /// feet, still.</summary>
        public void EnterStanding(Transform vehicle, Vector3 local, Quaternion rot)
        {
            InCar = true;
            standing = true;
            seatParts = null;
            onBike = false;
            if (!rig) rig = GetComponentInChildren<HumanoidRig>(true);
            if (!cc) cc = GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            transform.SetParent(vehicle, false);
            transform.localPosition = local;
            transform.localRotation = rot;
            GoingHome = Home = false;
            path = null;
            sit = true;   // no walking about
            idle = false;
            if (rig) { rig.seated = rig.driving = rig.riding = rig.pillion = false; rig.speed = 0f; rig.dead = false; rig.grounded = true; }
        }

        bool standing;

        /// <summary>Out of the car at the door, then off on foot to the nearest house (the car has been taken).</summary>
        public void LeaveCar(Vector3 door, Vector3 face)
        {
            if (seatParts) { seatParts.Leave(transform); seatParts = null; }
            standing = false;
            InCar = false;
            onBike = false;
            var pm = PedestrianManager.Instance;
            transform.SetParent(pm ? pm.transform : null, true);
            transform.SetPositionAndRotation(door, Quaternion.LookRotation(new Vector3(face.x, 0f, face.z).sqrMagnitude > 0.01f ? new Vector3(face.x, 0f, face.z) : Vector3.forward));
            Physics.SyncTransforms();
            if (cc) cc.enabled = true;
            sit = false;
            if (rig) { rig.seated = false; rig.riding = false; rig.driving = false; rig.pillion = false; }
            pause = 0.6f;
            GoHome(pm ? pm.NearestDoor(door) : door + face * 25f);
            fleeDir = new Vector3(face.x, 0f, face.z).normalized;   // first a few steps clear of the car
            fleeTimer = 1.1f;
            if (pm) pm.Adopt(this);
        }

        /// <summary>Run this way for a while (a car on fire).</summary>
        public void Flee(Vector3 dir, float seconds)
        {
            fleeDir = new Vector3(dir.x, 0f, dir.z).normalized;
            fleeTimer = seconds;
        }

        public override void Interact(PlayerController p)
        {
            var src = lines != null && lines.Length > 0 ? lines : DefaultLines;
            GameEvents.ShowDialog(npcName, src[Random.Range(0, src.Length)]);
            talkTimer = 5f;
            if (rig) rig.PlayGesture("Talk", 4f);
        }

        public void Knock(Vector3 impulse)
        {
            if (knockTimer > 0f) return;
            knockTimer = 3.5f;
            if (rig) { if (rig.Full) rig.knocked = true; else rig.dead = true; }   // the falling-down clip, then getting up
            if (motor) motor.impulse += impulse;
            string[] shouts = { "Arey bhai dekh ke chala!", "Oye! Andha hai kya?", "Police bulaunga!", "Hai Ram!" };
            GameEvents.ShowToast(npcName + ": \"" + shouts[Random.Range(0, shouts.Length)] + "\"", 2f);
        }

        /// <summary>Walk to this door and go in (PedestrianManager, at night).</summary>
        public void GoHome(Vector3 door)
        {
            GoingHome = true;
            Home = false;
            homeDoor = door;
            homeTimer = 0f;
            idle = false;
            sit = false;
        }

        /// <summary>Fresh state for a walker taken from the pool.</summary>
        public void ResetState()
        {
            GoingHome = false;
            Home = false;
            fleeTimer = sideTimer = 0f;
            talkTimer = 0f;
            watchTimer = 0f;
            onBike = false;
            seatParts = null;
            standing = false;
            knockTimer = 0f;
            if (health && health.IsDead) health.Revive();
            if (!rig) rig = GetComponentInChildren<HumanoidRig>(true);
            if (rig) { rig.Revive(); rig.riding = false; rig.carrying = false; }
            var cas = GetComponent<Casualty>();
            if (cas) Destroy(cas);
            lastHurtBy = null;
        }

        void SetHidden(bool h)
        {
            hidden = h;
            if (rig && rig.gameObject.activeSelf == h) rig.gameObject.SetActive(!h);
            if (cc) cc.enabled = !h;
        }

        /// <summary>A moving vehicle about to reach this spot: run to the nearer side of its path.</summary>
        void ScanTraffic()
        {
            Vector3 pos = transform.position;
            int n = Physics.OverlapSphereNonAlloc(pos, 18f, hits, 1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = hits[i];
                var rb = col.attachedRigidbody;
                Transform car = rb ? rb.transform : col.transform;
                Vector3 v = Vector3.zero;
                var tc = car.GetComponent<TrafficCar>();
                var br = car.GetComponent<BikeRide>();   // a two-wheeler moves itself (kinematic): its own speed
                bool traffic = tc || car.GetComponent<TrafficBike>();
                if (tc) v = car.forward * tc.Speed;
                else if (br) v = br.Forward * br.Speed;
                else if (rb && !rb.isKinematic) v = rb.linearVelocity;
                v.y = 0f;
                float sp = v.magnitude;
                Vector3 fwd;
                float reach;
                if (sp >= 1.5f) { fwd = v / sp; reach = 4f + sp * 2.2f; }
                // a traffic vehicle waiting with us in its way: it will not come on until we move, so we move (owner,
                // 21 Nov 2026: "NPC ke pass gaadi aaye to vo side hojaaye varna jam lagjayega")
                else if (traffic) { fwd = new Vector3(car.forward.x, 0f, car.forward.z).normalized; reach = TrafficReach; }
                else continue;
                if (traffic) reach = Mathf.Max(reach, TrafficReach);
                Vector3 rel = pos - car.position;
                rel.y = 0f;
                float ahead = Vector3.Dot(rel, fwd);
                if (ahead < -1.5f || ahead > reach) continue;   // behind it, or more than ~2 s away
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                float lat = Vector3.Dot(rel, right);
                var info = car.GetComponent<VehicleInfo>();
                float half = (info ? info.width : 1.8f) * 0.5f + 1.2f;
                if (Mathf.Abs(lat) > half)
                {
                    // just out of its way: along beside it, not back into the lane before it has passed
                    if (traffic && Mathf.Abs(lat) < half + 1.5f) { holdDir = right * Mathf.Sign(lat); holdTimer = 0.6f; }
                    continue;
                }
                if (fleeOtherT <= 0f) fleeDir = right * (lat >= 0f ? 1f : -1f);   // (else: that side was shut, see Update)
                if (fleeTimer <= 0f) { fleeShutT = 0f; fleeFrom = pos; }
                fleeMayCross = sp < 3f;   // over to the other side only in front of one that waits or crawls
                fleeTimer = Mathf.Max(fleeTimer, 1.2f);
                talkTimer = 0f;
                return;
            }
        }

        /// <summary>A wall, railing or trunk within a step that way.</summary>
        bool WallBeside(Vector3 way)
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.7f, way, out var hit, 0.9f,
                                   ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer)), QueryTriggerInteraction.Ignore)
                   && hit.normal.y < 0.6f;
        }

        /// <summary>Someone right in front, coming the other way or standing: step to the left for a moment.</summary>
        void ScanCrowd()
        {
            if (lastDir.sqrMagnitude < 0.01f) return;
            Vector3 pos = transform.position;
            int n = Physics.OverlapSphereNonAlloc(pos + lastDir * 0.9f + Vector3.up * 0.9f, 0.55f, hits, (1 << 9) | (1 << 8), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].transform == transform || hits[i].transform.IsChildOf(transform)) continue;
                sideStep = Vector3.Cross(lastDir, Vector3.up);   // left of the way we walk
                sideTimer = 0.7f;
                return;
            }
        }

        void Update()
        {
            if (!rig) rig = GetComponentInChildren<HumanoidRig>(true);
            if (!rig) return;
            float dt = Time.deltaTime;
            if (InCar && standing) { rig.seated = rig.driving = rig.riding = rig.pillion = false; rig.speed = 0f; rig.grounded = true; return; }   // a conductor on his feet
            if (InCar && seatParts) { VehicleSetup.Pose(rig, seatPose); return; }
            if (InCar) { rig.seated = !onBike; rig.driving = !onBike; rig.riding = onBike; rig.speed = 0f; rig.grounded = true; return; }
            shoutCd -= dt;
            if (homeAtNight)
            {
                bool away = !TownClock.LocalsOut(TownClock.Hour);
                if (away != hidden) SetHidden(away);
                if (away) return;
            }
            if (health && health.IsDead)
            {
                rig.dead = true;
                rig.speed = 0;
                if (cc && cc.enabled && knockTimer > 0f) { knockTimer -= dt; motor.Move(Vector3.zero, 6f, dt); }   // thrown: lands first
                return;
            }
            if (knockTimer > 0f)
            {
                knockTimer -= dt;
                motor.Move(Vector3.zero, 6f, dt);
                if (knockTimer <= 0f) rig.Revive();
                return;
            }
            if (!sit)
            {
                scanTimer -= dt;
                if (scanTimer <= 0f) { scanTimer = 0.25f; ScanTraffic(); ScanCrowd(); }
            }

            Vector3 desired = Vector3.zero;
            Vector3 face = transform.forward;
            float run = 1f;
            if (fleeOtherT > 0f) fleeOtherT -= dt;
            if (fleeTimer > 0f)   // out of a car's way first, whatever else
            {
                fleeTimer -= dt;
                // half a second of running and hardly moved: a wall / railing that side, so over to the other
                fleeShutT += dt;
                if (fleeShutT > 0.5f)
                {
                    Vector3 moved = transform.position - fleeFrom;
                    moved.y = 0f;
                    // (hardly moved is not enough: whoever walked the other way has only just turned - a wall must be there)
                    if (moved.sqrMagnitude < 0.36f && fleeOtherT <= 0f && fleeMayCross && WallBeside(fleeDir)) { fleeDir = -fleeDir; fleeOtherT = 2.5f; }
                    fleeShutT = 0f;
                    fleeFrom = transform.position;
                }
                desired = fleeDir * 3.8f;
                face = fleeDir;
            }
            else if (GoingHome)
            {
                Vector3 to = homeDoor - transform.position;
                to.y = 0f;
                homeTimer += dt;
                if (to.magnitude < 1.3f || homeTimer > 45f) { Home = true; return; }
                desired = to.normalized * speed * 1.15f;
                face = to;
            }
            else if (watchTimer > 0f)   // an accident: stop and look (a few walk up to the edge of it)
            {
                watchTimer -= dt;
                Vector3 to = watchAt - transform.position;
                to.y = 0f;
                if (watchClose && to.magnitude > 5f) { desired = to.normalized * speed; face = to; }
                else face = to;
            }
            else if (talkTimer > 0f)
            {
                talkTimer -= dt;
                var p = PlayerController.Instance;
                if (p) face = p.transform.position - transform.position;
            }
            else if (sit || idle)
            {
                if (faceDirection.sqrMagnitude > 0.01f) face = faceDirection;
            }
            else if (path != null && path.Length > 1)
            {
                if (pause > 0f) pause -= dt;
                else
                {
                    index = Mathf.Clamp(index, 0, path.Length - 1);
                    Vector3 to = path[index] - transform.position;
                    to.y = 0;
                    if (to.magnitude < 1.2f + laneOffset)
                    {
                        index += direction;
                        if (index >= path.Length || index < 0)
                        {
                            direction = -direction;
                            index = Mathf.Clamp(index + 2 * direction, 0, path.Length - 1);
                        }
                        if (Random.value < 0.06f) pause = Random.Range(2f, 6f);
                        to = path[index] - transform.position;
                        to.y = 0f;
                    }
                    // keep left of the line (India): people walking the other way pass on the other side
                    Vector3 d = to.sqrMagnitude > 0.001f ? to.normalized : transform.forward;
                    Vector3 left = Vector3.Cross(d, Vector3.up);
                    Vector3 aim = to + left * laneOffset;
                    desired = aim.normalized * speed;
                    face = aim;
                }
            }
            if (holdTimer > 0f)
            {
                holdTimer -= dt;
                float into = Vector3.Dot(desired, holdDir);
                if (fleeTimer <= 0f && into < 0f) desired -= holdDir * into;
            }
            if (sideTimer > 0f && fleeTimer <= 0f && desired.sqrMagnitude > 0.01f)
            {
                sideTimer -= dt;
                desired = (desired.normalized + sideStep * 0.9f).normalized * desired.magnitude;
            }
            if (desired.sqrMagnitude > 0.01f) lastDir = new Vector3(desired.x, 0f, desired.z).normalized;
            if (!sit) motor.Move(desired * run, fleeTimer > 0f ? 16f : 6f, dt);   // out of a car's way: off at once
            motor.FaceTowards(face, fleeTimer > 0f ? 720f : 300f, dt);
            rig.speed = sit ? 0f : motor.HorizontalVelocity.magnitude;
            rig.seated = sit;
            rig.grounded = true;
        }
    }
}

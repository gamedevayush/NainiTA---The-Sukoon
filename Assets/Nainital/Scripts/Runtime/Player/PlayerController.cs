using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Third-person player: walk / run / sprint, jump (coyote + buffer), swim in Naini Lake and climb
    /// out on ledges, punch combo, kick, block, dodge roll, sit on benches, row boats, die & respawn.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor), typeof(Combatant), typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Speeds (m/s)")]
        public float walkSpeed = 1.8f;
        public float runSpeed = 4.4f;
        public float sprintSpeed = 7.2f;
        public float swimSpeed = 2.2f;
        public float acceleration = 14f;
        public float turnSpeed = 720f;
        public float jumpHeight = 1.25f;

        public enum Mode { Normal, Seated, Boat, Dead, Vehicle, Mount, Bike }
        public Mode CurrentMode { get; private set; } = Mode.Normal;
        public bool InWater { get; private set; }
        public HumanoidRig Rig => rig;
        public Health Health => health;
        public Vector3 RespawnPoint { get; set; }
        /// <summary>Where the next revive puts the player instead of RespawnPoint (the hospital, the police station).</summary>
        [System.NonSerialized] public Vector3? NextRespawn;
        public bool Arrested { get; private set; }

        CharacterMotor motor;
        Combatant combat;
        Health health;
        HumanoidRig rig;
        float coyote, jumpBuffer, deadTimer, fallPeakY;
        Transform seatAnchor;
        BoatController boat;
        CarController car;
        public CarController Car => car;

        public void EnterVehicle(CarController c)
        {
            StopBoarding();
            car = c;
            CurrentMode = Mode.Vehicle;
            motor.Controller.enabled = false;
            transform.SetParent(c.seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            rig.seated = true;
            rig.driving = true;
            rig.speed = 0;
            // the unbranded set: the setup's driver's seat - hands on the wheel / grips, feet on the pedals, head under the
            // roof - and the driver's door opens (VehicleParts)
            var vp = c.GetComponent<VehicleParts>();
            if (vp && vp.HasSetup) { vp.Sit(transform, rig, 0); vp.OpenDoorFor(0); }
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(c.transform, c.type == CarType.Bus ? 13f : 7.5f);
        }

        /// <summary>Astride a bike / scooty (PlayerBike): the setup's rider seat - hands on the grips, feet on the pegs,
        /// the riding pose (VehicleParts.Sit). The leg swung over is BoardBike's (the riding pack's Mount clip).</summary>
        public void EnterBike(PlayerBike b)
        {
            StopBoarding();
            CurrentMode = Mode.Bike;
            motor.Controller.enabled = false;
            var vp = b.Bike.Parts;
            if (vp && vp.HasSetup) vp.Sit(transform, rig, 0);
            else
            {
                transform.SetParent(b.Bike.model ? b.Bike.model : b.transform, false);
                transform.localPosition = b.Bike.SeatLocal;
                transform.localRotation = Quaternion.identity;
                rig.riding = true;
            }
            rig.speed = 0;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(b.transform, 5f);
        }

        /// <summary>Off the bike at `at` (on foot again), at once (thrown, died; his own E goes through AlightBike).</summary>
        public void ExitBike(Vector3 at)
        {
            StopBoarding();
            var vp = PlayerBike.Riding ? PlayerBike.Riding.Bike.Parts : GetComponentInParent<VehicleParts>();
            if (vp) vp.Leave(transform);
            transform.SetParent(null, true);
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized + Vector3.forward * 1e-4f, Vector3.up);
            rig.riding = false; rig.seated = false; rig.pillion = false; rig.driving = false; rig.lean = 0f;
            CurrentMode = Mode.Normal;
            motor.Controller.enabled = true;
            motor.Teleport(at);
            fallPeakY = at.y;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(null, 0f);
        }

        public void EnterMount(Transform seatT, Transform camTarget)
        {
            CurrentMode = Mode.Mount;
            motor.Controller.enabled = false;
            transform.SetParent(seatT, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            rig.seated = true;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(camTarget, 5.5f);
        }

        public void ExitMount(Vector3 at)
        {
            transform.SetParent(null, true);
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized + Vector3.forward * 1e-4f, Vector3.up);
            rig.seated = false;
            CurrentMode = Mode.Normal;
            motor.Controller.enabled = true;
            motor.Teleport(at);
            fallPeakY = at.y;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(null, 0f);
        }

        /// <summary>Out of the car at `at`, at once (died, arrested, the menu; his own E goes through AlightVehicle).</summary>
        public void ExitVehicle(Vector3 at)
        {
            StopBoarding();
            var vp = car ? car.GetComponent<VehicleParts>() : null;
            if (vp) { vp.Leave(transform); vp.OpenDoorFor(0); }
            transform.SetParent(null, true);
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized + Vector3.forward * 1e-4f, Vector3.up);
            rig.seated = false;
            rig.driving = false;
            CurrentMode = Mode.Normal;
            motor.Controller.enabled = true;
            motor.Teleport(at);
            fallPeakY = at.y;
            car = null;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(null, 0f);
        }

        // ------------------------------------------------------------------ getting in and out, seen (owner, 22 Nov 2026:
        // "any vehicle se entry exit ka animation bhi nhi chlra" - it was a jump straight onto the seat and back)
        /// <summary>Walking up to a vehicle or climbing in / out right now: the vehicle takes no input (CarController,
        /// PlayerBike) and Update leaves the rig to the routine.</summary>
        public bool Boarding { get; private set; }
        Coroutine boardCo;

        void StopBoarding()
        {
            if (boardCo != null) { StopCoroutine(boardCo); boardCo = null; }
            Boarding = false;
        }

        /// <summary>His E at a car (CarController.Interact): a few steps to the driver's door (the setup's entry point, the
        /// other side when that one is blocked), the door opens, the Mixamo "Entering Car" clip while he slides onto the seat
        /// (no doors - an auto: sitting down), then EnterVehicle.</summary>
        public void BoardVehicle(CarController c)
        {
            var vp = c.GetComponent<VehicleParts>();
            bool setup = vp && vp.HasSetup, door = setup && OpenCab(vp) == false;
            car = c;
            CurrentMode = Mode.Vehicle;
            Vector3 entry = setup ? vp.WayOut(0, out _) : c.seat.position + c.transform.right * 1.1f;
            Begin(BoardRoutine(entry, () => setup ? vp.SeatWorld(0) : c.seat.position, () => setup ? vp.SeatRotWorld(0) : c.seat.rotation,
                               door ? "CarEnter" : "StandToSit", door ? 1.8f : 1.1f, door ? 1.5f : 1.0f, setup ? vp.PoseOf(0) : SeatPose.Drive,
                               () => { if (door) vp.OpenDoorFor(0, 2.6f); }, () => EnterVehicle(c)));
        }

        /// <summary>His E at a bike / scooty (PlayerBike.Mount): to its side, the leg swung over (the riding pack's Mount),
        /// onto the seat, then EnterBike.</summary>
        public void BoardBike(PlayerBike b)
        {
            var vp = b.Bike.Parts;
            bool setup = vp && vp.HasSetup;
            CurrentMode = Mode.Bike;
            Vector3 entry = setup ? vp.WayOut(0, out _) : b.transform.position - b.transform.right * 0.9f;
            Transform seatOn = b.Bike.model ? b.Bike.model : b.transform;
            Begin(BoardRoutine(entry, () => setup ? vp.SeatWorld(0) : seatOn.TransformPoint(b.Bike.SeatLocal), () => setup ? vp.SeatRotWorld(0) : b.transform.rotation,
                               rig.HasRideClips ? "Mount" : "StandToSit", 1.1f, 0.9f, SeatPose.Ride, null, () => EnterBike(b)));
        }

        /// <summary>His E in a car (CarController.Exit): the door opens, the Mixamo "Exiting Car" clip (no doors: standing
        /// up) while he slides out to `at`, then ExitVehicle.</summary>
        public void AlightVehicle(Vector3 at)
        {
            var vp = car ? car.GetComponent<VehicleParts>() : null;
            bool door = vp && vp.HasSetup && OpenCab(vp) == false;
            if (vp) { vp.Leave(transform); if (door) vp.OpenDoorFor(0, 2.4f); }
            Begin(AlightRoutine(at, door ? "CarExit" : "SitToStand", door ? 1.6f : 1.0f, door ? 1.3f : 0.9f, () => ExitVehicle(at)));
        }

        /// <summary>His E on a bike (PlayerBike.Exit): the leg swung back over (Dismount) while he steps off to `at`.</summary>
        public void AlightBike(PlayerBike b, Vector3 at)
        {
            var vp = b ? b.Bike.Parts : null;
            if (vp) vp.Leave(transform);
            Begin(AlightRoutine(at, rig.HasRideClips ? "Dismount" : "SitToStand", 1.2f, 0.9f, () => ExitBike(at)));
        }

        void Begin(System.Collections.IEnumerator r)
        {
            if (boardCo != null) StopCoroutine(boardCo);
            Boarding = true;
            boardCo = StartCoroutine(r);
        }

        System.Collections.IEnumerator BoardRoutine(Vector3 entry, System.Func<Vector3> seatPos, System.Func<Quaternion> seatRot, string clip,
                                                    float clipFor, float slideFor, SeatPose pose, System.Action openDoor, System.Action seated)
        {
            motor.Controller.enabled = false;
            transform.SetParent(null, true);
            entry = GroundUnder(entry);
            // 1. a few steps up to the door / the bike's side
            Vector3 a = transform.position, way = entry - a;
            way.y = 0f;
            float d = way.magnitude;
            if (d > 0.15f)
            {
                rig.seated = rig.driving = rig.riding = rig.pillion = false;
                rig.grounded = true;
                rig.speed = 1.5f;
                Quaternion r0 = transform.rotation, rw = Quaternion.LookRotation(way / d);
                float tw = Mathf.Clamp(d / 1.5f, 0.2f, 1.6f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / tw)
                {
                    transform.position = Vector3.Lerp(a, entry, t);
                    transform.rotation = Quaternion.Slerp(r0, rw, Mathf.Clamp01(t * 3f));
                    yield return null;
                }
                rig.speed = 0f;
            }
            // 2. the door, the clip, and onto the seat
            openDoor?.Invoke();
            if (!string.IsNullOrEmpty(clip)) rig.PlayGesture(clip, clipFor);
            VehicleSetup.Pose(rig, pose);
            Vector3 p0 = transform.position;
            Quaternion q0 = transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / slideFor)
            {
                float s = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(p0, seatPos(), s);
                transform.rotation = Quaternion.Slerp(q0, seatRot(), s);
                yield return null;
            }
            boardCo = null;
            Boarding = false;
            seated();
        }

        System.Collections.IEnumerator AlightRoutine(Vector3 at, string clip, float clipFor, float slideFor, System.Action done)
        {
            var ik = GetComponent<HandIK>();
            if (ik) ik.enabled = false;   // the hands leave the wheel / the grips
            transform.SetParent(null, true);
            if (!string.IsNullOrEmpty(clip)) rig.PlayGesture(clip, clipFor);
            Vector3 p0 = transform.position, way = at - p0;
            way.y = 0f;
            Quaternion q0 = transform.rotation;
            Quaternion q1 = way.sqrMagnitude > 0.01f ? Quaternion.LookRotation(way.normalized) : q0;
            for (float t = 0f; t < 1f; t += Time.deltaTime / slideFor)
            {
                float s = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(p0, at, s);
                transform.rotation = Quaternion.Slerp(q0, q1, s);
                if (t > 0.55f) { rig.seated = rig.driving = rig.riding = rig.pillion = false; rig.grounded = true; }   // on his feet
                yield return null;
            }
            boardCo = null;
            Boarding = false;
            done();
        }

        // ------------------------------------------------------------------ jumping out at speed (owner, 22 Nov 2026: "speed
        // me kisi bhi vehicle se exit krte hain to ragdoll bnke girega..health jayegi based on speed")
        /// <summary>Faster than this (m/s, ~9 km/h) E does not climb out: he jumps.</summary>
        public const float BailSpeed = 2.5f;
        /// <summary>Lying as a ragdoll / getting up from it: Update leaves him alone.</summary>
        public bool Ragdolled { get; private set; }
        Ragdoll ragdoll;
        Transform fallCam;

        /// <summary>Health lost jumping off at v m/s: grows with the energy (v^2): nothing under ~11 km/h, ~25 at 30 km/h,
        /// ~50 at 40, ~85 at 50, past 100 (dead) near 55 km/h - an unprotected fall at that speed.</summary>
        public static float BailDamage(float v) => Mathf.Max(0f, 0.45f * v * v - 4f);

        /// <summary>Out of a moving vehicle (CarController / PlayerBike, his E above BailSpeed): off the seat beside it, a
        /// ragdoll with the vehicle's velocity and a push out and up, hurt by the speed; he gets up when the body lies still.
        /// at: beside the vehicle (its way out), out: the way out of it.</summary>
        public void BailOut(Vector3 at, Vector3 vehicleVelocity, Vector3 outward)
        {
            StopBoarding();
            var vp = car ? car.GetComponent<VehicleParts>() : PlayerBike.Riding ? PlayerBike.Riding.Bike.Parts : GetComponentInParent<VehicleParts>();
            if (vp) vp.Leave(transform);
            transform.SetParent(null, true);
            car = null;
            CurrentMode = Mode.Normal;
            motor.Controller.enabled = false;
            rig.seated = rig.driving = rig.riding = rig.pillion = false;
            rig.lean = 0f;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : -transform.right;
            Vector3 flatV = new Vector3(vehicleVelocity.x, 0f, vehicleVelocity.z);
            transform.SetPositionAndRotation(GroundUnder(at) + outward * 0.3f + Vector3.up * 0.05f,
                                             Quaternion.LookRotation(flatV.sqrMagnitude > 0.01f ? flatV.normalized : transform.forward));
            Physics.SyncTransforms();
            float v = flatV.magnitude;
            ragdoll = Ragdoll.Throw(rig, vehicleVelocity + outward * 1.5f + Vector3.up * 1.2f);
            Ragdolled = true;
            // the camera on a steady point over the body, facing the way he flew (on the hips it spun with every tumble)
            if (!fallCam) fallCam = new GameObject("Fall camera").transform;
            fallCam.SetPositionAndRotation(ragdoll ? ragdoll.Hips.position : transform.position, transform.rotation);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(fallCam, 5.5f);
            StartCoroutine(RagdollRoutine());
            float dmg = BailDamage(v);
            Debug.Log($"[Nainital] bailed out at {v:F1} m/s ({v * 3.6f:F0} km/h): -{dmg:F0} health, ragdoll {(ragdoll ? "on" : "missing")}");
            if (dmg > 0f) health.Damage(dmg, transform.position, null);
        }

        System.Collections.IEnumerator RagdollRoutine()
        {
            float t = 0f, still = 0f;
            if (!ragdoll) { rig.knocked = true; yield return new WaitForSeconds(1.8f); }   // no humanoid bones: the knocked-down clip
            while (ragdoll)
            {
                t += Time.deltaTime;
                still = ragdoll.HipsSpeed < 0.35f ? still + Time.deltaTime : 0f;
                if (fallCam) fallCam.position = ragdoll.Hips.position;
                if ((t > 1.5f && still > 0.7f) || t > 8f) break;
                yield return null;
            }
            // where the body came to rest: him there, facing away from his head (he sits up towards his feet)
            Vector3 hp = ragdoll ? ragdoll.Hips.position : transform.position;
            Vector3 toFeet = ragdoll && ragdoll.Head ? hp - ragdoll.Head.position : transform.forward;
            toFeet.y = 0f;
            if (ragdoll) { ragdoll.Remove(); ragdoll = null; }
            transform.SetPositionAndRotation(GroundUnder(hp), Quaternion.LookRotation(toFeet.sqrMagnitude > 0.01f ? toFeet.normalized : transform.forward));
            Physics.SyncTransforms();
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(null, 0f);
            if (health.IsDead)
            {
                rig.LieDead();   // OnDied has set Mode.Dead: UpdateDead takes it from here
                motor.Controller.enabled = true;
                Ragdolled = false;
                yield break;
            }
            rig.GetUpNow();
            yield return new WaitForSeconds(Mathf.Max(0.5f, rig.GetUpLength * 0.9f));
            motor.Controller.enabled = true;
            motor.Teleport(transform.position + Vector3.up * 0.05f);
            fallPeakY = transform.position.y;
            Ragdolled = false;
        }

        /// <summary>An open cab (the auto, the e-rickshaw): he steps in and sits down. Everything else is a car / van / bus /
        /// truck cabin he ducks into ("Entering Car") - the unbranded models have no cut doors (owner, 21 Nov 2026: doors
        /// only if perfect), so the doors' parts cannot tell.</summary>
        static bool OpenCab(VehicleParts vp)
        {
            string m = (vp.model ?? "").ToLowerInvariant();
            return m.Contains("tuktuk") || m.Contains("tirri") || m.Contains("rickshaw") || m.Contains("auto");
        }

        static Vector3 GroundUnder(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var h, 5f, ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer)), QueryTriggerInteraction.Ignore))
                return h.point;
            return p;
        }

        void Awake()
        {
            Instance = this;
            motor = GetComponent<CharacterMotor>();
            combat = GetComponent<Combatant>();
            health = GetComponent<Health>();
            rig = GetComponentInChildren<HumanoidRig>();
            RespawnPoint = transform.position;
            safePos = transform.position;
            fallPeakY = transform.position.y;
            health.Died += OnDied;
        }

        Vector3 CameraRelative(Vector2 input)
        {
            var cam = ThirdPersonCamera.Instance;
            Vector3 f = cam ? cam.FlatForward : transform.forward;
            Vector3 r = new Vector3(f.z, 0, -f.x);
            return f * input.y + r * input.x;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Boarding || Ragdolled) return;   // climbing in / out, a ragdoll: the routine moves him and sets the rig
            switch (CurrentMode)
            {
                case Mode.Dead: UpdateDead(dt); return;
                case Mode.Seated: UpdateSeated(); return;
                case Mode.Boat: UpdateBoat(); return;
                case Mode.Vehicle:
                case Mode.Mount: rig.seated = true; rig.speed = 0; rig.grounded = true; return;
                case Mode.Bike: rig.speed = 0; rig.grounded = true; return;   // the riding pose: VehicleParts.Sit set it
            }
            if (Jetpack.Flying) { fallPeakY = transform.position.y; return; }   // the jetpack (cheat UDAAN) does the moving

            bool ui = GameInput.UiBlocking;
            Vector2 mv = ui ? Vector2.zero : GameInput.Move;
            Vector3 wish = CameraRelative(mv);
            float mag = Mathf.Clamp01(wish.magnitude);
            if (mag > 0.001f) wish /= wish.magnitude;

            // ---------------- water
            float waterY = LakeArea.WaterY;
            bool overLake = LakeArea.Contains(transform.position);
            bool wasInWater = InWater;
            InWater = overLake && transform.position.y < waterY - 0.85f;
            if (InWater && !wasInWater) GameEvents.RaiseSplash(transform.position);
            if (InWater) { UpdateSwim(wish, mag, waterY, dt); return; }
            rig.swimming = false;

            // ---------------- ground movement
            bool grounded = motor.Grounded;
            coyote = grounded ? 0.12f : coyote - dt;
            jumpBuffer = !ui && GameInput.JumpPressed ? 0.15f : jumpBuffer - dt;

            combat.Blocking = !ui && GameInput.BlockHeld && grounded && !combat.IsAttacking && !combat.IsRolling;

            float speed;
            if (combat.Blocking) speed = walkSpeed * 0.8f;
            else if (GameInput.WalkHeld || mag < 0.45f) speed = walkSpeed;
            else if (GameInput.SprintHeld && PlayerStats.CanSprint) speed = sprintSpeed * PlayerStats.SprintScale;   // stamina, fat (PlayerStats)
            else speed = runSpeed;
            Vector3 desired = wish * speed * Mathf.Max(mag, mag > 0 ? 0.35f : 0f);

            if (combat.IsRolling) desired = combat.RollDirection * Combatant.RollSpeed;
            else if (combat.IsStunned) desired = Vector3.zero;
            else if (combat.IsAttacking) desired = desired * 0.2f + transform.forward * combat.LungeSpeed;

            float accel = combat.IsRolling ? 60f : (grounded ? acceleration : acceleration * 0.35f);
            motor.Move(desired, accel, dt);

            // facing
            if (combat.IsAttacking) { }
            else if (combat.IsRolling) motor.FaceTowards(combat.RollDirection, 2000f, dt);
            else if (combat.Blocking && ThirdPersonCamera.Instance) motor.FaceTowards(ThirdPersonCamera.Instance.FlatForward, turnSpeed, dt);
            else if (mag > 0.05f) motor.FaceTowards(wish, turnSpeed, dt);

            // jump
            if (jumpBuffer > 0f && coyote > 0f && !combat.Busy)
            {
                motor.velocity.y = Mathf.Sqrt(2f * -motor.gravity * jumpHeight * PlayerStats.JumpScale);   // muscle, fat
                jumpBuffer = 0f;
                coyote = 0f;
            }

            // fight
            if (!ui)
            {
                if (GameInput.AttackPressed || GameInput.KickPressed)
                {
                    FaceNearestEnemy();
                    combat.TryAttack(GameInput.KickPressed);
                }
                if (GameInput.RollPressed && grounded) combat.TryRoll(mag > 0.1f ? wish : transform.forward);
            }

            // fall damage
            if (grounded)
            {
                float fall = fallPeakY - transform.position.y;
                if (fall > 6f) health.Damage((fall - 6f) * 9f, transform.position + Vector3.up * 5f, null);
                fallPeakY = transform.position.y;
                if (!overLake && motor.HorizontalVelocity.magnitude < 8f) safePos = transform.position;
            }
            else fallPeakY = Mathf.Max(fallPeakY, transform.position.y);

            if (transform.position.y < -100f) motor.Teleport(safePos + Vector3.up);

            rig.grounded = grounded || motor.AirTime < 0.15f;
            rig.verticalSpeed = motor.velocity.y;
            rig.speed = motor.HorizontalVelocity.magnitude * (combat.IsRolling ? 0f : 1f);
            rig.seated = rig.rowing = false;
        }

        Vector3 safePos;

        void UpdateSwim(Vector3 wish, float mag, float waterY, float dt)
        {
            rig.swimming = true;
            rig.grounded = true;
            combat.Blocking = false;
            float targetY = waterY - 1.35f;
            motor.velocity.y = Mathf.Clamp((targetY - transform.position.y) * 3f, -3f, 3f);
            float speed = (GameInput.SprintHeld && PlayerStats.CanSwimFast ? swimSpeed * 1.6f : swimSpeed) * PlayerStats.SwimScale;   // breath, swimming skill
            motor.Move(wish * speed * mag, 5f, dt, useGravity: false);
            if (mag > 0.05f) motor.FaceTowards(wish, 300f, dt);
            rig.speed = motor.HorizontalVelocity.magnitude;
            fallPeakY = transform.position.y;

            if (!GameInput.UiBlocking && GameInput.JumpPressed) TryClimbOut(waterY);
        }

        void TryClimbOut(float waterY)
        {
            for (int i = 0; i < 8; i++)
            {
                Quaternion q = Quaternion.Euler(0, (i % 2 == 0 ? 1 : -1) * (i / 2) * 25f, 0);
                Vector3 dir = q * transform.forward;
                Vector3 probe = transform.position + dir * 1.6f;
                probe.y = waterY + 4f;
                if (Physics.Raycast(probe, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.point.y > waterY + 0.2f && hit.point.y < waterY + 3.4f && hit.normal.y > 0.6f &&
                    !hit.collider.transform.IsChildOf(transform))
                {
                    motor.Teleport(hit.point + Vector3.up * 0.08f);
                    GameEvents.ShowToast("Kinare pe chadh gaye!", 1.5f);
                    return;
                }
            }
            GameEvents.ShowToast("Yahan se nahi chadh sakte. Jetty ya neecha kinara dhundo.", 2f);
        }

        void FaceNearestEnemy()
        {
            float best = 3.5f;
            Transform target = null;
            foreach (var e in EnemyAI.All)
            {
                if (!e || e.Dead) continue;
                Vector3 d = e.transform.position - transform.position;
                d.y = 0;
                float dist = d.magnitude;
                if (dist < best && Vector3.Dot(transform.forward, d.normalized) > -0.2f) { best = dist; target = e.transform; }
            }
            if (target) motor.FaceTowards(target.position - transform.position, 100000f, 1f);
            else if (GameInput.Move.sqrMagnitude > 0.05f) motor.FaceTowards(CameraRelative(GameInput.Move), 100000f, 1f);
        }

        // ---------------------------------------------------------------- seated
        public void SitOn(Transform anchor)
        {
            CurrentMode = Mode.Seated;
            seatAnchor = anchor;
            motor.Teleport(anchor.position);
            transform.rotation = Quaternion.LookRotation(anchor.forward, Vector3.up);
            motor.Controller.enabled = false;
            rig.seated = true;
            rig.speed = 0;
        }

        void UpdateSeated()
        {
            rig.seated = true;
            rig.speed = 0;
            if (seatAnchor) { transform.position = seatAnchor.position; }
            if (!GameInput.UiBlocking && (GameInput.Move.sqrMagnitude > 0.2f || GameInput.JumpPressed || GameInput.InteractPressed))
                StandUp();
        }

        /// <summary>Off the chair / bench, a step in front of it (also used by tests before a teleport).</summary>
        public void StandUp()
        {
            if (CurrentMode != Mode.Seated) return;
            rig.seated = false;
            motor.Controller.enabled = true;
            motor.Teleport(transform.position + transform.forward * 0.6f + Vector3.up * 0.3f);
            CurrentMode = Mode.Normal;
        }

        // ---------------------------------------------------------------- boat
        public void EnterBoat(BoatController b)
        {
            boat = b;
            CurrentMode = Mode.Boat;
            motor.Controller.enabled = false;
            transform.SetParent(b.seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            rig.rowing = true;
            rig.swimming = false;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(b.transform, 7f);
        }

        public void ExitBoat(Vector3 at)
        {
            transform.SetParent(null, true);
            rig.rowing = false;
            CurrentMode = Mode.Normal;
            motor.Controller.enabled = true;
            motor.Teleport(at);
            boat = null;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(null, 0f);
        }

        void UpdateBoat()
        {
            rig.rowing = true;
            rig.speed = boat ? boat.Speed : 0f;
        }

        // ---------------------------------------------------------------- death
        void OnDied()
        {
            if (CurrentMode == Mode.Boat && boat) boat.ForceExit();
            if (CurrentMode == Mode.Vehicle && car) car.ForceExit();
            if (CurrentMode == Mode.Mount && Pony.Ridden) Pony.Ridden.Interact(this);
            if (CurrentMode == Mode.Bike && PlayerBike.Riding) PlayerBike.Riding.ForceExit();
            CurrentMode = Mode.Dead;
            rig.dead = true;
            deadTimer = 3.5f;
            var ws = WantedSystem.Instance;
            if (!(ws && ws.PlayerDied(this)))   // wanted: the hospital and its bill (WantedSystem)
                GameEvents.ShowToast("Aap behosh ho gaye... thodi der mein Mall Road pe hosh aayega.", 3.2f);
        }

        /// <summary>Caught by the police (WantedSystem.Busted): out of any vehicle, hands up, then the police station.</summary>
        public void Arrest(Vector3 at)
        {
            if (CurrentMode == Mode.Dead) return;
            if (CurrentMode == Mode.Boat && boat) boat.ForceExit();
            if (CurrentMode == Mode.Vehicle && car) car.ForceExit();
            if (CurrentMode == Mode.Mount && Pony.Ridden) Pony.Ridden.Interact(this);
            if (CurrentMode == Mode.Bike && PlayerBike.Riding) PlayerBike.Riding.ForceExit();
            if (CurrentMode == Mode.Seated) { rig.seated = false; motor.Controller.enabled = true; }
            if (Jetpack.Flying) Jetpack.Toggle(this);
            CurrentMode = Mode.Dead;
            Arrested = true;
            health.invulnerable = true;
            rig.handsUp = true;
            deadTimer = 4f;
            NextRespawn = at;
        }

        void UpdateDead(float dt)
        {
            rig.speed = 0;
            deadTimer -= dt;
            if (motor.Controller.enabled) motor.Move(Vector3.zero, 20f, dt);
            if (deadTimer > 0f) return;
            health.Revive();
            combat.ResetState();
            rig.Revive();
            rig.handsUp = false;
            if (Arrested) { Arrested = false; health.invulnerable = false; }
            Vector3 at = NextRespawn ?? RespawnPoint;
            NextRespawn = null;
            motor.Teleport(at);
            fallPeakY = at.y;
            CurrentMode = Mode.Normal;
        }
    }
}

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

        public enum Mode { Normal, Seated, Boat, Dead, Vehicle, Mount }
        public Mode CurrentMode { get; private set; } = Mode.Normal;
        public bool InWater { get; private set; }
        public HumanoidRig Rig => rig;
        public Health Health => health;
        public Vector3 RespawnPoint { get; set; }

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
            car = c;
            CurrentMode = Mode.Vehicle;
            motor.Controller.enabled = false;
            transform.SetParent(c.seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            rig.seated = true;
            rig.speed = 0;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(c.transform, c.type == CarType.Bus ? 13f : 7.5f);
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

        public void ExitVehicle(Vector3 at)
        {
            transform.SetParent(null, true);
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized + Vector3.forward * 1e-4f, Vector3.up);
            rig.seated = false;
            CurrentMode = Mode.Normal;
            motor.Controller.enabled = true;
            motor.Teleport(at);
            fallPeakY = at.y;
            car = null;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(null, 0f);
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
            switch (CurrentMode)
            {
                case Mode.Dead: UpdateDead(dt); return;
                case Mode.Seated: UpdateSeated(); return;
                case Mode.Boat: UpdateBoat(); return;
                case Mode.Vehicle:
                case Mode.Mount: rig.seated = true; rig.speed = 0; rig.grounded = true; return;
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
            else if (GameInput.SprintHeld) speed = sprintSpeed;
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
                motor.velocity.y = Mathf.Sqrt(2f * -motor.gravity * jumpHeight);
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
            float speed = GameInput.SprintHeld ? swimSpeed * 1.6f : swimSpeed;
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
            {
                rig.seated = false;
                motor.Controller.enabled = true;
                motor.Teleport(transform.position + transform.forward * 0.6f + Vector3.up * 0.3f);
                CurrentMode = Mode.Normal;
            }
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
            CurrentMode = Mode.Dead;
            rig.dead = true;
            deadTimer = 3.5f;
            GameEvents.ShowToast("Aap behosh ho gaye... thodi der mein Mall Road pe hosh aayega.", 3.2f);
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
            motor.Teleport(RespawnPoint);
            fallPeakY = RespawnPoint.y;
            CurrentMode = Mode.Normal;
        }
    }
}

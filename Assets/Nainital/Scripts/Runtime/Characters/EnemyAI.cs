using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Street thug ("gunda") AI: idles near home, chases the player inside aggro range, circles,
    /// blocks sometimes, attacks with cooldown, gives up beyond leash, respawns after a while.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor), typeof(Combatant), typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        public static readonly List<EnemyAI> All = new List<EnemyAI>();

        public string displayName = "Gunda";
        public float aggroRange = 13f, leashRange = 40f, attackRange = 1.45f;
        public float moveSpeed = 3.6f, walkSpeed = 1.4f;
        public float respawnSeconds = 90f;
        public bool nightOnly;

        public bool Dead => health && health.IsDead;
        public Health Health => health;

        enum State { Idle, Chase, Fight, Return, Dead }
        State state;
        CharacterMotor motor;
        Combatant combat;
        Health health;
        HumanoidRig rig;
        Vector3 home;
        float thinkTimer, attackTimer, strafeDir = 1f, deadTimer, blockTimer, stuckTimer;
        Vector3 lastPos;

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            combat = GetComponent<Combatant>();
            health = GetComponent<Health>();
            rig = GetComponentInChildren<HumanoidRig>();
            home = transform.position;
            health.Died += () =>
            {
                state = State.Dead;
                deadTimer = respawnSeconds;
                rig.dead = true;
                GameEvents.ShowToast(displayName + " dher ho gaya!", 1.8f);
            };
            health.Damaged += (a, from, src) =>
            {
                if (state == State.Idle || state == State.Return) state = State.Chase;
            };
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            var player = PlayerController.Instance;
            if (state == State.Dead) { UpdateDead(dt); return; }

            bool hidden = nightOnly && DayNightCycle.Instance && DayNightCycle.Instance.NightFactor < 0.5f;
            Vector3 toPlayer = Vector3.zero;
            float dist = float.MaxValue;
            bool playerValid = player && player.CurrentMode == PlayerController.Mode.Normal && !player.InWater && !player.Health.IsDead && !hidden;
            if (player)
            {
                toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0;
                dist = toPlayer.magnitude;
            }
            float fromHome = Vector3.Distance(transform.position, home);

            switch (state)
            {
                case State.Idle:
                    if (playerValid && dist < aggroRange)
                    {
                        state = State.Chase;
                        GameEvents.ShowToast(displayName + ": \"Oye! Kahan ja raha hai?\"", 2f);
                    }
                    break;
                case State.Chase:
                    if (!playerValid || fromHome > leashRange) { state = State.Return; break; }
                    if (dist < attackRange + 0.6f) state = State.Fight;
                    break;
                case State.Fight:
                    if (!playerValid || fromHome > leashRange) { state = State.Return; break; }
                    if (dist > attackRange + 1.8f) state = State.Chase;
                    break;
                case State.Return:
                    if (playerValid && dist < aggroRange * 0.7f && fromHome < leashRange * 0.8f) state = State.Chase;
                    else if (fromHome < 2f) state = State.Idle;
                    break;
            }

            Vector3 desired = Vector3.zero;
            Vector3 face = transform.forward;
            combat.Blocking = false;
            if (state == State.Chase)
            {
                desired = toPlayer.normalized * moveSpeed;
                face = toPlayer;
            }
            else if (state == State.Fight)
            {
                face = toPlayer;
                thinkTimer -= dt;
                attackTimer -= dt;
                blockTimer -= dt;
                if (thinkTimer <= 0f) { thinkTimer = Random.Range(0.8f, 2f); strafeDir = Random.value < 0.5f ? -1f : 1f; }
                Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized) * strafeDir;
                float keep = dist - attackRange;
                desired = (toPlayer.normalized * Mathf.Clamp(keep * 3f, -2f, 2f) + side * 0.9f);
                var pc = player ? player.GetComponent<Combatant>() : null;
                if (pc && pc.IsAttacking && blockTimer <= 0f && Random.value < 0.35f) blockTimer = 0.7f;
                combat.Blocking = blockTimer > 0f;
                if (!combat.Blocking && attackTimer <= 0f && dist < attackRange + 0.3f && !combat.Busy)
                {
                    combat.TryAttack(Random.value < 0.2f);
                    attackTimer = Random.Range(1.0f, 1.9f);
                }
            }
            else if (state == State.Return)
            {
                Vector3 h = home - transform.position;
                h.y = 0;
                desired = h.normalized * walkSpeed;
                face = h;
            }

            if (combat.IsStunned) desired = Vector3.zero;
            if (combat.IsAttacking) desired = transform.forward * combat.LungeSpeed;

            // crude unstick: if pushing but not moving, hop sideways
            if (desired.sqrMagnitude > 1f && (transform.position - lastPos).sqrMagnitude < 0.0004f) stuckTimer += dt; else stuckTimer = 0f;
            lastPos = transform.position;
            if (stuckTimer > 0.6f) { desired += Vector3.Cross(Vector3.up, desired.normalized) * 3f; if (motor.Grounded) motor.velocity.y = 5f; stuckTimer = 0f; }

            motor.Move(desired, 12f, dt);
            if (!combat.IsAttacking) motor.FaceTowards(face, 540f, dt);
            rig.speed = motor.HorizontalVelocity.magnitude;
            rig.grounded = motor.Grounded || motor.AirTime < 0.15f;
            rig.verticalSpeed = motor.velocity.y;
            SetVisible(!hidden || state != State.Idle);
        }

        bool visible = true;
        void SetVisible(bool v)
        {
            if (v == visible) return;
            visible = v;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = v;
            motor.Controller.enabled = v;
        }

        void UpdateDead(float dt)
        {
            rig.speed = 0;
            deadTimer -= dt;
            if (motor.Controller.enabled) motor.Move(Vector3.zero, 20f, dt);
            if (deadTimer > respawnSeconds - 6f) return;
            SetVisible(false);
            if (deadTimer > 0f) return;
            health.Revive();
            combat.ResetState();
            rig.Revive();
            motor.Teleport(home);
            state = State.Idle;
            SetVisible(true);
        }
    }
}

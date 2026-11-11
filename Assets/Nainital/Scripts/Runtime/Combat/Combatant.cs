using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Melee fighting shared by the player and AI: 3-hit combo (jab, cross, kick), standalone kick,
    /// block (front hits do 20% damage), dodge roll with i-frames, hit-stun and knockback.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(CharacterMotor))]
    public class Combatant : MonoBehaviour
    {
        public HumanoidRig rig;
        public float damageMultiplier = 1f;
        public LayerMask hitMask = ~0;
        public bool heavyPuncher;   // AI thugs use a slow haymaker

        struct AttackDef
        {
            public HumanoidRig.Attack anim;
            public float duration, impact, damage, range, knock, lunge;
        }

        static readonly AttackDef[] Combo =
        {
            new AttackDef { anim = HumanoidRig.Attack.Jab,   duration = 0.40f, impact = 0.15f, damage = 8f,  range = 1.25f, knock = 2.0f, lunge = 1.6f },
            new AttackDef { anim = HumanoidRig.Attack.Cross, duration = 0.48f, impact = 0.20f, damage = 11f, range = 1.30f, knock = 3.0f, lunge = 2.0f },
            new AttackDef { anim = HumanoidRig.Attack.Kick,  duration = 0.66f, impact = 0.30f, damage = 17f, range = 1.55f, knock = 6.5f, lunge = 2.4f },
        };
        static readonly AttackDef KickDef = Combo[2];
        static readonly AttackDef Haymaker = new AttackDef
            { anim = HumanoidRig.Attack.Haymaker, duration = 0.85f, impact = 0.42f, damage = 14f, range = 1.4f, knock = 5f, lunge = 1.2f };

        public bool Blocking { get; set; }
        public bool IsAttacking => attackTimer > 0f;
        public bool IsRolling => rollTimer > 0f;
        public bool IsStunned => stunTimer > 0f;
        public bool Busy => IsAttacking || IsRolling || IsStunned || health.IsDead;
        public float LungeSpeed => IsAttacking && attackTimer > current.duration - current.impact ? current.lunge : 0f;
        public Vector3 RollDirection { get; private set; }
        public const float RollDuration = 0.55f;
        public const float RollSpeed = 7.5f;

        public event System.Action<Health, float> HitLanded;

        Health health;
        CharacterMotor motor;
        AttackDef current;
        float attackTimer, stunTimer, rollTimer, comboTimer, cooldown;
        int comboIndex;
        bool impactDone;
        readonly Collider[] overlap = new Collider[16];
        readonly HashSet<Health> struck = new HashSet<Health>();

        void Awake()
        {
            health = GetComponent<Health>();
            motor = GetComponent<CharacterMotor>();
            if (!rig) rig = GetComponentInChildren<HumanoidRig>();
            health.Damaged += OnDamaged;
        }

        void OnDestroy() { if (health) health.Damaged -= OnDamaged; }

        public bool TryAttack(bool kick = false)
        {
            if (health.IsDead || IsRolling || IsStunned || cooldown > 0f) return false;
            if (IsAttacking && attackTimer > current.duration * 0.35f) return false;  // allow chaining late in the swing
            AttackDef def;
            if (heavyPuncher) def = Haymaker;
            else if (kick) { def = KickDef; comboIndex = 0; }
            else
            {
                if (comboTimer <= 0f) comboIndex = 0;
                def = Combo[comboIndex];
                comboIndex = (comboIndex + 1) % Combo.Length;
            }
            current = def;
            attackTimer = def.duration;
            impactDone = false;
            comboTimer = def.duration + 0.45f;
            Blocking = false;
            if (rig) rig.PlayAttack(def.anim, def.duration);
            return true;
        }

        public bool TryRoll(Vector3 dir)
        {
            if (health.IsDead || IsRolling || IsStunned) return false;
            dir.y = 0;
            RollDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            rollTimer = RollDuration;
            attackTimer = 0f;
            Blocking = false;
            if (rig) { rig.CancelAttack(); rig.PlayRoll(RollDuration); }
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown -= dt;
            comboTimer -= dt;
            if (stunTimer > 0f) stunTimer -= dt;
            if (rollTimer > 0f)
            {
                rollTimer -= dt;
                float u = 1f - rollTimer / RollDuration;
                health.invulnerable = u > 0.1f && u < 0.75f;
                if (rollTimer <= 0f) health.invulnerable = false;
            }
            if (attackTimer > 0f)
            {
                attackTimer -= dt;
                float elapsed = current.duration - attackTimer;
                if (!impactDone && elapsed >= current.impact)
                {
                    impactDone = true;
                    DoHit();
                }
                if (attackTimer <= 0f) cooldown = heavyPuncher ? 0.2f : 0.05f;
            }
            if (rig) rig.blocking = Blocking && !IsAttacking && !IsRolling;
        }

        void DoHit()
        {
            Vector3 fwd = transform.forward;
            Vector3 center = transform.position + Vector3.up * 1.1f + fwd * (current.range * 0.6f);
            int n = Physics.OverlapSphereNonAlloc(center, 0.8f, overlap, hitMask, QueryTriggerInteraction.Ignore);
            struck.Clear();
            for (int i = 0; i < n; i++)
            {
                var h = overlap[i].GetComponentInParent<Health>();
                if (!h || h == health || h.IsDead || !struck.Add(h)) continue;
                Vector3 to = h.transform.position - transform.position;
                to.y = 0;
                if (to.magnitude > current.range + 0.6f) continue;
                float dmg = current.damage * damageMultiplier;
                var other = h.GetComponent<Combatant>();
                bool blocked = other && other.Blocking && Vector3.Dot(other.transform.forward, -to.normalized) > 0.2f;
                if (blocked) dmg *= 0.2f;
                if (h.Damage(dmg, transform.position, gameObject))
                {
                    var om = h.GetComponent<CharacterMotor>();
                    if (om) om.impulse += to.normalized * (blocked ? current.knock * 0.3f : current.knock);
                    HitLanded?.Invoke(h, dmg);
                    GameEvents.RaiseHit(center, blocked);
                }
            }
        }

        void OnDamaged(float amount, Vector3 from, GameObject src)
        {
            if (Blocking && amount < 6f) return;   // blocked hits do not stagger
            stunTimer = 0.38f;
            attackTimer = 0f;
            if (rig) { rig.CancelAttack(); rig.PlayHit(); }
        }

        public void ResetState()
        {
            attackTimer = stunTimer = rollTimer = 0f;
            comboIndex = 0;
            Blocking = false;
            health.invulnerable = false;
        }
    }
}

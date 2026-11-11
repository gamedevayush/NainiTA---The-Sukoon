using UnityEngine;

namespace Nainital
{
    /// <summary>Nainital's famous cheeky monkeys: wander near temples/viewpoints, snatch snacks, run off when chased.</summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class Monkey : MonoBehaviour
    {
        public float wanderRadius = 9f;
        Vector3 home, target;
        CharacterMotor motor;
        HumanoidRig rig;
        float think, fleeTimer, stealCooldown;

        static readonly string[] Steals =
        {
            "Bandar aapke haath se bhutta cheen ke bhaag gaya!",
            "Bandar ne chips ka packet uda liya!",
            "Bandar aapka chashma kheench ke le gaya... aur wapas phek diya!",
            "Bandar ne momos wala dona cheen liya!",
        };

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            rig = GetComponentInChildren<HumanoidRig>();
            home = transform.position;
            target = home;
            stealCooldown = Random.Range(5f, 20f);
        }

        void Update()
        {
            if (!rig) rig = GetComponentInChildren<HumanoidRig>();
            float dt = Time.deltaTime;
            var p = PlayerController.Instance;
            stealCooldown -= dt;
            Vector3 desired = Vector3.zero;
            float speed = 1.3f;
            if (p && p.CurrentMode == PlayerController.Mode.Normal)
            {
                Vector3 toP = p.transform.position - transform.position;
                toP.y = 0;
                float d = toP.magnitude;
                if (d < 2.2f && stealCooldown <= 0f)
                {
                    stealCooldown = Random.Range(25f, 50f);
                    fleeTimer = 3.5f;
                    GameEvents.ShowToast(Steals[Random.Range(0, Steals.Length)], 3f);
                }
                else if (d < 5f && GameInput.SprintHeld && GameInput.Move.sqrMagnitude > 0.1f)
                {
                    if (fleeTimer <= 0f) GameEvents.RaiseMonkeyScared();
                    fleeTimer = 2.5f;
                }
                if (fleeTimer > 0f)
                {
                    fleeTimer -= dt;
                    desired = -toP.normalized;
                    speed = 5.5f;
                }
            }
            if (fleeTimer <= 0f)
            {
                think -= dt;
                if (think <= 0f)
                {
                    think = Random.Range(2f, 6f);
                    Vector2 r = Random.insideUnitCircle * wanderRadius;
                    target = home + new Vector3(r.x, 0, r.y);
                }
                Vector3 to = target - transform.position;
                to.y = 0;
                if (to.magnitude > 0.8f) desired = to.normalized;
            }
            if ((transform.position - home).sqrMagnitude > 40f * 40f) desired = (home - transform.position).normalized;
            motor.Move(desired * speed, 10f, dt);
            if (desired.sqrMagnitude > 0.01f) motor.FaceTowards(desired, 500f, dt);
            if (rig)
            {
                rig.speed = motor.HorizontalVelocity.magnitude * 1.6f;   // quick little steps
                rig.grounded = motor.Grounded || motor.AirTime < 0.2f;
                rig.seated = desired.sqrMagnitude < 0.01f && think > 3f;  // sits around between wanders
            }
        }
    }
}

using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Rentable rowing boat on Naini Lake. Board it at a boat stand, steer with move input
    /// (forward = row, left/right = turn), dock again at any boat stand jetty.
    /// </summary>
    public class BoatController : Interactable
    {
        public static BoatController Driving { get; private set; }
        public static readonly System.Collections.Generic.List<Transform> Docks = new System.Collections.Generic.List<Transform>();

        public Transform seat;
        public Transform dockExit;          // where the player steps off at the home jetty
        public float maxSpeed = 4.2f, turnRate = 55f;

        public float Speed => Mathf.Abs(speed);
        float speed, bobPhase;
        PlayerController rider;
        Vector3 home;
        Quaternion homeRot;

        void Start()
        {
            home = transform.position;
            homeRot = transform.rotation;
            bobPhase = Random.value * 10f;
            if (dockExit && !Docks.Contains(dockExit)) Docks.Add(dockExit);
        }

        public override string Prompt => rider ? NearestDock(out _) < 14f ? "Naav se utro" : "Utarne ke liye jetty ke paas jao" : "Naav chalao (Rs 100)";
        public override bool CanInteract(PlayerController p) => !rider && Driving == null;

        public override void Interact(PlayerController p)
        {
            if (!rider)
            {
                if (!GameState.TrySpend(100)) { GameEvents.ShowToast("Naav ke liye Rs 100 chahiye!", 2.5f); return; }
                rider = p;
                Driving = this;
                p.EnterBoat(this);
                GameEvents.ShowToast("W/Joystick se chalao, A/D se mudo. Jetty pe E dabake utro.", 4f);
                return;
            }
            float d = NearestDock(out var dock);
            if (d < 14f && dock) Exit(dock.position);
            else GameEvents.ShowToast("Mallital ya Tallital boat stand ki jetty ke paas jao.", 2.5f);
        }

        float NearestDock(out Transform dock)
        {
            dock = null;
            float best = float.MaxValue;
            foreach (var t in Docks)
            {
                if (!t) continue;
                float d = Vector3.Distance(new Vector3(t.position.x, 0, t.position.z), new Vector3(transform.position.x, 0, transform.position.z));
                if (d < best) { best = d; dock = t; }
            }
            return best;
        }

        void Exit(Vector3 at)
        {
            var p = rider;
            rider = null;
            Driving = null;
            p.ExitBoat(at + Vector3.up * 0.2f);
        }

        public void ForceExit()
        {
            if (!rider) return;
            rider.transform.SetParent(null, true);
            rider = null;
            Driving = null;
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(null, 0f);
        }

        /// <summary>
        /// Open water (inside the shrunk safe outline) is always fine. Boats moor at jetties in the
        /// shore band outside it, so there any move is allowed while the bow stays on the lake, and a
        /// boat that is somehow off the water may only move toward the lake.
        /// </summary>
        bool CanMoveTo(Vector3 from, Vector3 to)
        {
            if (LakeArea.ContainsInner(to)) return true;
            Vector3 bow = to + transform.forward * Mathf.Sign(speed) * 1.6f;
            if (LakeArea.Contains(to) && LakeArea.Contains(bow)) return true;
            if (!LakeArea.Contains(from) && LakeArea.Instance)
            {
                Vector3 c = LakeArea.Instance.Centre;
                return (new Vector3(to.x - c.x, 0, to.z - c.z)).sqrMagnitude < (new Vector3(from.x - c.x, 0, from.z - c.z)).sqrMagnitude;
            }
            return false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float waterY = LakeArea.WaterY;
            if (rider)
            {
                Vector2 mv = GameInput.UiBlocking ? Vector2.zero : GameInput.Move;
                float throttle = mv.y;
                speed = Mathf.MoveTowards(speed, throttle * maxSpeed, (Mathf.Abs(throttle) > 0.1f ? 2.2f : 1.0f) * dt);
                float turn = mv.x * turnRate * Mathf.Clamp01(0.35f + Mathf.Abs(speed) / maxSpeed);
                transform.Rotate(0, turn * dt, 0, Space.World);
                Vector3 pos = transform.position;
                Vector3 next = pos + transform.forward * speed * dt;
                if (CanMoveTo(pos, next)) transform.position = next;
                else speed *= -0.3f; // bump the shore
            }
            else if (Driving != this)
            {
                // drift back to the jetty mooring when nobody is rowing
                transform.position = Vector3.Lerp(transform.position, new Vector3(home.x, transform.position.y, home.z), dt * 0.2f);
                transform.rotation = Quaternion.Slerp(transform.rotation, homeRot, dt * 0.2f);
                speed = 0;
            }
            bobPhase += dt;
            Vector3 p = transform.position;
            p.y = waterY + 0.05f + Mathf.Sin(bobPhase * 1.3f) * 0.05f;
            transform.position = p;
            var e = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(Mathf.Sin(bobPhase * 1.1f) * 2f, e.y, Mathf.Sin(bobPhase * 0.9f) * 3f);
        }
    }
}

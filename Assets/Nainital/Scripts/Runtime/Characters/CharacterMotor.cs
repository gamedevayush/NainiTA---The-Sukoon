using UnityEngine;

namespace Nainital
{
    /// <summary>CharacterController wrapper: acceleration, gravity, ground snapping, knockback.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : MonoBehaviour
    {
        public float gravity = -22f;
        public float groundSnap = 0.6f;

        [System.NonSerialized] public Vector3 velocity;
        [System.NonSerialized] public Vector3 impulse;
        public CharacterController Controller { get; private set; }
        public bool Grounded { get; private set; }
        public float AirTime { get; private set; }
        public Vector3 HorizontalVelocity => new Vector3(velocity.x, 0, velocity.z);

        void Awake() => Controller = GetComponent<CharacterController>();

        public void Move(Vector3 desiredHorizontal, float accel, float dt, bool useGravity = true)
        {
            if (!Controller.enabled) return;
            Vector3 h = Vector3.MoveTowards(HorizontalVelocity, desiredHorizontal, accel * dt);
            velocity.x = h.x;
            velocity.z = h.z;
            if (useGravity) velocity.y += gravity * dt;

            Vector3 total = velocity + impulse;
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, 14f * dt);
            bool wasGrounded = Grounded;
            var flags = Controller.Move(total * dt);
            Grounded = (flags & CollisionFlags.Below) != 0;

            // Stay glued to the ground when walking down slopes / steps.
            if (!Grounded && wasGrounded && velocity.y <= 0f && useGravity)
            {
                Vector3 origin = transform.position + Controller.center;
                float castDist = Controller.height * 0.5f - Controller.radius + groundSnap;
                if (Physics.SphereCast(origin, Controller.radius * 0.9f, Vector3.down, out var hit, castDist,
                        ~0, QueryTriggerInteraction.Ignore) && hit.collider.gameObject != gameObject)
                {
                    float drop = hit.distance - (Controller.height * 0.5f - Controller.radius) + Controller.skinWidth;
                    if (drop > 0.001f) Controller.Move(Vector3.down * drop);
                    Grounded = true;
                }
            }

            if (Grounded)
            {
                AirTime = 0f;
                if (velocity.y < 0f) velocity.y = -2f;
            }
            else AirTime += dt;
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
        }

        public void FaceTowards(Vector3 dir, float degPerSec, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degPerSec * dt);
        }

        public void Teleport(Vector3 pos)
        {
            bool en = Controller.enabled;
            Controller.enabled = false;
            transform.position = pos;
            Controller.enabled = en;
            velocity = Vector3.zero;
            impulse = Vector3.zero;
        }
    }
}

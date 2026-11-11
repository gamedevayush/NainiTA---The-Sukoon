using UnityEngine;

namespace Nainital
{
    /// <summary>Over-the-shoulder orbit camera with collision, zoom and sprint FOV kick.</summary>
    [DefaultExecutionOrder(100)]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public static ThirdPersonCamera Instance { get; private set; }

        public Transform target;
        public float distance = 4.3f, minDistance = 1.6f, maxDistance = 10f;
        public float height = 1.55f, shoulder = 0.42f;
        public float pitchMin = -40f, pitchMax = 72f;
        public float yaw, pitch = 12f;
        public float baseFov = 60f;
        public LayerMask collisionMask = ~0;

        Camera cam;
        float currentDist;
        Vector3 pivotSmooth;
        Transform overrideTarget;
        float overrideDistance;
        bool initialised;

        public Vector3 FlatForward => Quaternion.Euler(0, yaw, 0) * Vector3.forward;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
        }

        public void SetOverride(Transform t, float dist)
        {
            overrideTarget = t;
            overrideDistance = dist;
        }

        public void SnapBehind()
        {
            if (target) yaw = target.eulerAngles.y;
            initialised = false;
        }

        void LateUpdate()
        {
            Transform t = overrideTarget ? overrideTarget : target;
            if (!t) return;
            float dt = Time.deltaTime;

            yaw += GameInput.Look.x;
            pitch = Mathf.Clamp(pitch - GameInput.Look.y, pitchMin, pitchMax);
            // In vehicles/boats the camera swings back behind the target when you stop looking around.
            if (overrideTarget && Time.unscaledTime - GameInput.LastLookTime > 1.3f)
            {
                yaw = Mathf.LerpAngle(yaw, overrideTarget.eulerAngles.y, 1f - Mathf.Exp(-2.5f * dt));
                pitch = Mathf.Lerp(pitch, 11f, 1f - Mathf.Exp(-2f * dt));
            }
            distance = Mathf.Clamp(distance - GameInput.Zoom, minDistance, maxDistance);
            float dist = overrideTarget ? overrideDistance : distance;

            Vector3 pivotTarget = t.position + Vector3.up * height;
            if (!initialised) { pivotSmooth = pivotTarget; currentDist = dist; initialised = true; }
            pivotSmooth.x = pivotTarget.x;
            pivotSmooth.z = pivotTarget.z;
            pivotSmooth.y = Mathf.Lerp(pivotSmooth.y, pivotTarget.y, 1f - Mathf.Exp(-14f * dt));

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 shoulderOffset = rot * Vector3.right * (overrideTarget ? 0f : shoulder);
            Vector3 pivot = pivotSmooth;
            if (shoulderOffset.sqrMagnitude > 1e-4f &&
                Physics.SphereCast(pivotSmooth, 0.2f, shoulderOffset.normalized, out var sh, shoulderOffset.magnitude, collisionMask, QueryTriggerInteraction.Ignore))
                shoulderOffset = shoulderOffset.normalized * Mathf.Max(0f, sh.distance - 0.05f);
            pivot += shoulderOffset;

            Vector3 back = -(rot * Vector3.forward);
            float wanted = dist;
            if (Physics.SphereCast(pivot, 0.25f, back, out var hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.4f, hit.distance - 0.05f);
            currentDist = wanted < currentDist ? wanted : Mathf.Lerp(currentDist, wanted, 1f - Mathf.Exp(-5f * dt));

            Vector3 pos = pivot + back * currentDist;
            var terrain = Terrain.activeTerrain;
            if (terrain)
            {
                float ground = terrain.SampleHeight(pos) + terrain.transform.position.y + 0.35f;
                if (pos.y < ground) pos.y = ground;
            }
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pivot - pos + rot * Vector3.forward * 0.01f, Vector3.up));

            if (cam)
            {
                var pc = PlayerController.Instance;
                bool sprinting = pc && pc.CurrentMode == PlayerController.Mode.Normal && GameInput.SprintHeld && GameInput.Move.sqrMagnitude > 0.2f;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + (sprinting ? 8f : 0f), 1f - Mathf.Exp(-4f * dt));
            }
        }
    }
}

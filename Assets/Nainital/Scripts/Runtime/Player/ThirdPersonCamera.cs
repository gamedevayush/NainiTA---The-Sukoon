using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        /// <summary>What the camera follows instead of the player right now (a vehicle, a boat, the gym view), or null.</summary>
        public Transform OverrideTarget => overrideTarget;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            if (cam) near = cam.nearClipPlane;
            GameSettings.ApplyCull(cam);   // far vehicles / people not drawn (25 Nov)
        }

        // ---- through his own eyes in a vehicle (owner, 23 Nov 2026: "bus me ek camera option jisse fpv me rotate krskte"):
        // V / the Camera button while seated (a bus passenger, at the wheel, on a bike); look round with the mouse / a drag,
        // the view turning with the vehicle
        /// <summary>First-person while seated (V / the Camera button flips it).</summary>
        public static bool FirstPerson;
        float fpYaw, fpPitch, near = 0.3f;

        // ---- the ride felt through the eyes (owner, 23 Nov 2026: "jab bus chlri ho to shake wgera aaye FPV camera ko hlke ya
        // tej based on road and speed"): a hum that grows with speed - about 2.4x off the made road (dirt, grass, a lane
        // the network does not know) - and a jolt whenever the vehicle's body is thrown up or down or rocked (a kerb, a
        // pothole, the heightmap's steps: its measured vertical acceleration and pitch / roll rate, so a rough stretch shakes
        // and a smooth one does not). A bike hums most, a bus least (its weight).
        float trauma, vyPrev, surfT, surface = 1f;
        Rigidbody lastBody;
        RoadNetwork roads;
        /// <summary>The shake now: metres of jolt, degrees of jitter, on the made road (tests).</summary>
        public static float ShakeM, ShakeDeg;
        public static bool ShakeOnRoad = true;

        void Ride(Rigidbody body, Transform veh, out Vector3 jolt, out Quaternion jitter)
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-3f);
            float speed = body ? body.linearVelocity.magnitude : 0f;
            if (body != lastBody) { lastBody = body; vyPrev = body ? body.linearVelocity.y : 0f; trauma = 0f; }
            if (body)
            {
                float vy = body.linearVelocity.y;
                float acc = Mathf.Abs(vy - vyPrev) / dt;
                vyPrev = vy;
                Vector3 av = veh.InverseTransformDirection(body.angularVelocity);
                float rock = new Vector2(av.x, av.z).magnitude;   // pitch / roll rate, rad/s
                // (first measure, 23 Nov: a bus on the road never crossed 3 m/s2 / 0.15 rad/s - no jolt in 25 s: lower)
                trauma = Mathf.Max(trauma, Mathf.Clamp01((acc - 1.2f) / 10f) + Mathf.Clamp01((rock - 0.05f) * 1.6f));
            }
            trauma = Mathf.MoveTowards(trauma, 0f, dt * 1.6f);
            if ((surfT -= dt) <= 0f) { surfT = 0.4f; ShakeOnRoad = OnRoad(veh.position); surface = ShakeOnRoad ? 1f : 2.4f; }
            float kind = body && body.GetComponent<BikeRide>() ? 1.6f : body && body.mass > 4000f ? 0.75f : 1f;
            float hum = Mathf.Clamp01(speed / 16f) * surface * kind;
            float t = Time.time;
            float n1 = Mathf.PerlinNoise(t * 17f, 0.3f) - 0.5f, n2 = Mathf.PerlinNoise(0.7f, t * 15f) - 0.5f, n3 = Mathf.PerlinNoise(t * 9f, 5.1f) - 0.5f;
            float amp = 0.022f * hum + 0.05f * trauma * trauma;   // metres (a bus at 9 m/s on the road: ~1 cm)
            float rot = 1.1f * hum + 4f * trauma * trauma;          // degrees
            jolt = veh.up * (n1 * amp * 2f) + veh.right * (n2 * amp);
            jitter = Quaternion.Euler(n2 * rot, n3 * rot * 0.5f, n1 * rot * 0.7f);
            ShakeM = amp;
            ShakeDeg = rot;
        }

        /// <summary>On a road of the network (its line within half its width + 0.7 m).</summary>
        bool OnRoad(Vector3 p)
        {
            if (!roads) { var tm = FindAnyObjectByType<TrafficManager>(); roads = tm ? tm.network : null; }
            if (!roads || roads.roads == null) return true;
            foreach (var r in roads.roads)
            {
                var q = r.pts;
                float lim = r.width * 0.5f + 0.7f, lim2 = lim * lim;
                for (int i = 0; i + 1 < q.Length; i++)
                {
                    Vector3 a = q[i], b = q[i + 1];
                    if (Mathf.Abs(a.x - p.x) > 60f || Mathf.Abs(a.z - p.z) > 60f) continue;
                    Vector2 ab = new Vector2(b.x - a.x, b.z - a.z), ap = new Vector2(p.x - a.x, p.z - a.z);
                    float s = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
                    if ((ap - ab * s).sqrMagnitude < lim2) return true;
                }
            }
            return false;
        }

        static Volume rideClear;

        /// <summary>No camera motion blur from a seat (owner, 23 Nov 2026: "paas me blur hora hai ...like lens"): URP's camera
        /// motion blur takes the world as standing still, so the moving vehicle's inside - riding along with the camera - was
        /// smeared, the nearest the worst. A volume over every other (priority 10) turns it off while he rides.</summary>
        static void ClearRide(bool on)
        {
            if (!on) { if (rideClear && rideClear.enabled) rideClear.enabled = false; return; }
            if (!rideClear)
            {
                var go = new GameObject("Ride Clear (no motion blur)");
                DontDestroyOnLoad(go);
                rideClear = go.AddComponent<Volume>();
                rideClear.isGlobal = true;
                rideClear.priority = 10f;
                var p = ScriptableObject.CreateInstance<VolumeProfile>();
                var mb = p.Add<MotionBlur>(true);
                mb.intensity.Override(0f);
                rideClear.sharedProfile = p;
            }
            if (!rideClear.enabled) rideClear.enabled = true;
        }

        /// <summary>Set the first-person look (degrees from the vehicle's heading; tests).</summary>
        public void LookAround(float yawDeg, float pitchDeg) { fpYaw = yawDeg; fpPitch = pitchDeg; }

        static bool Seated(PlayerController p) =>
            p && !p.Boarding && !p.Ragdolled && (p.CurrentMode == PlayerController.Mode.Mount || p.CurrentMode == PlayerController.Mode.Vehicle || p.CurrentMode == PlayerController.Mode.Bike);

        /// <summary>The eyes: just in front of the face (the head bone a little up and forward), turned by his look round the
        /// vehicle's heading. False: no head to look from.</summary>
        bool EyeView(PlayerController p)
        {
            var a = p.Rig ? p.Rig.Anim : null;
            Transform head = a && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Head) : null;
            var body = p.GetComponentInParent<Rigidbody>();
            Transform veh = body ? body.transform : p.transform.parent ? p.transform.parent : p.transform;
            fpYaw = Mathf.Clamp(fpYaw + GameInput.Look.x, -165f, 165f);
            fpPitch = Mathf.Clamp(fpPitch - GameInput.Look.y, -70f, 60f);
            Vector3 up = veh.up.y > 0.3f ? veh.up : Vector3.up;
            // the view leans with the vehicle on a slope / in a turn (most of it: a horizon held a little by the eyes)
            Quaternion baseRot = Quaternion.Slerp(Quaternion.Euler(0f, veh.eulerAngles.y, 0f), veh.rotation, 0.7f);
            Ride(body, veh, out Vector3 jolt, out Quaternion jitter);
            Quaternion look = baseRot * Quaternion.Euler(0f, fpYaw, 0f) * Quaternion.Euler(fpPitch, 0f, 0f);
            Vector3 eye = (head ? head.position : p.transform.position + Vector3.up * 1.25f) + up * 0.09f + look * Vector3.forward * 0.16f + jolt;
            transform.SetPositionAndRotation(eye, look * jitter);
            // a wide angle (owner, 23 Nov 2026: "andr ka i think wide angle dena pdega camera"): the inside of a bus in view
            if (cam) { cam.nearClipPlane = 0.04f; cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 92f, 1f - Mathf.Exp(-6f * Time.deltaTime)); }
            return true;
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
            var pcv = PlayerController.Instance;
            bool seated = Seated(pcv);
            ClearRide(seated || (pcv && pcv.CurrentMode == PlayerController.Mode.Boat));
            if (seated && GameInput.CameraPressed)
            {
                FirstPerson = !FirstPerson;
                fpYaw = fpPitch = 0f;
                GameEvents.ShowToast(FirstPerson ? "Camera: aankhon se (ghumane ke liye drag / mouse)" : "Camera: peeche se", 1.6f);
            }
            if (FirstPerson && seated && EyeView(pcv)) return;
            if (cam && cam.nearClipPlane != near) cam.nearClipPlane = near;   // back from the eyes

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

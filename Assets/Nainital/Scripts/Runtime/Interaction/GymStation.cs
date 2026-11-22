using System.Collections;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A workout spot at the City Joggers Park open gym on Lower Mall (owner, 21 Nov 2026: "one gym, at a real gym's place;
    /// stats only, no fight moves"). E: a five-second workout at the frame, the stamina bar going down, then muscle and
    /// stamina go up and fat down (PlayerStats). Each frame its own exercise (HandIK on the player for the hands):
    /// "pull" - under the top bar, both hands on it, the body pulled up and let down (feet off the ground); "pedal" - hands
    /// on the handle post, a stepping bob; "twist" - on the disc, the hips turning left and right. Tired (under 20 % of the
    /// stamina bar) or done here a moment ago: a toast instead. Placed by LandmarkBuilder.LowerMallSights (which sets
    /// `along`, the top bar's direction; older builds: worked out from the row of frames).
    /// </summary>
    public class GymStation : Interactable
    {
        /// <summary>"weights", "pull", "pedal", "twist": what the frame is (only the wording and the gains differ).</summary>
        public string kind = "weights";
        /// <summary>The frame's top bar direction (its two posts stand 0.7 m either side along it, the bar 2.05 m up).</summary>
        public Vector3 along;
        float readyAt;
        Transform camRig;   // the workout's camera view (ThirdPersonCamera override) while it runs

        /// <summary>The ground under the frame (this spot is 0.9 m over it).</summary>
        Vector3 Ground => transform.position - Vector3.up * 0.9f;

        Vector3 Along()
        {
            if (along.sqrMagnitude > 0.25f) { var a = along; a.y = 0f; return a.normalized; }
            // the frames stand in a row along the bank: the row's direction is the bars' direction
            GymStation a0 = null, a1 = null;
            float best = 0f;
            var all = FindObjectsByType<GymStation>(FindObjectsSortMode.None);
            foreach (var x in all)
                foreach (var y in all)
                {
                    float d = (x.transform.position - y.transform.position).sqrMagnitude;
                    if (d > best) { best = d; a0 = x; a1 = y; }
                }
            Vector3 dir = a0 && a1 ? a1.transform.position - a0.transform.position : Vector3.right;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.right;
        }
        static bool busy;

        public override string Prompt => kind == "pull" ? "Pull-ups karo (gym)" : kind == "pedal" ? "Cycling karo (gym)" : kind == "twist" ? "Twister pe kasrat karo (gym)" : "Kasrat karo (gym)";

        public override bool CanInteract(PlayerController p) => !busy;

        public override void Interact(PlayerController p)
        {
            if (busy) return;
            if (Time.time < readyAt) { GameEvents.ShowToast("Thoda saans le lo, phir dobara", 2f); return; }
            if (PlayerStats.Energy01 < 0.2f) { GameEvents.ShowToast("Bahut thak gaye ho - pehle aaram karo", 2.5f); return; }
            StartCoroutine(Workout(p));
        }

        IEnumerator Workout(PlayerController p)
        {
            busy = true;
            PlayerStats.Working = true;
            GameInput.UiBlocking = true;
            GameEvents.ShowToast("Kasrat chal rahi hai...", 5f);

            // where to stand and which way to face: under / at the frame, square to its bar, from the side the player came
            Vector3 bar = Along(), g = Ground;
            Vector3 n = Vector3.Cross(Vector3.up, bar);
            if (Vector3.Dot(n, p.transform.position - g) > 0f) n = -n;   // facing the frame from the player's side
            Vector3 stand = kind == "pedal" ? g - n * 0.45f : g;
            var motor = p.GetComponent<CharacterMotor>();
            if (motor) motor.Teleport(stand + Vector3.up * 0.05f);
            p.transform.rotation = Quaternion.LookRotation(n);
            Vector3 right = Vector3.Cross(Vector3.up, n);

            // the camera: the roomiest 3/4 view of the workout (the old one stayed where it was and the frame's posts / the
            // bank's trees pulled it into the jacket or low between the trees - 22 Nov test run 2)
            var tpc = ThirdPersonCamera.Instance;
            float oldPitch = tpc ? tpc.pitch : 0f, oldYaw = tpc ? tpc.yaw : 0f;   // the view the player walked up with: open ground
            if (tpc)
            {
                float lift = kind == "pull" ? 0.3f : 0f;   // the body hangs 0.16-0.5 m up on the bar
                const float viewPitch = 11f;               // what ThirdPersonCamera eases an override's pitch to
                float yaw = PickView(stand + Vector3.up * (tpc.height + lift), n, tpc.collisionMask, viewPitch, out float dist);
                camRig = new GameObject("Gym camera").transform;
                camRig.SetPositionAndRotation(stand + Vector3.up * lift, Quaternion.Euler(0f, yaw, 0f));   // the override's yaw = this view
                tpc.yaw = yaw;
                tpc.pitch = viewPitch;
                tpc.SetOverride(camRig, dist);
            }

            // the hands: on the top bar (pull) or the handle post's top (pedal), through the player's HandIK
            var ik = p.GetComponent<HandIK>();
            bool addedIk = false;
            if (!ik && kind != "twist") { ik = p.gameObject.AddComponent<HandIK>(); addedIk = true; }
            Transform oldL = ik ? ik.left : null, oldR = ik ? ik.right : null;
            float oldW = ik ? ik.weight : 0f;
            Transform oldV = ik ? ik.vehicle : null, oldFL = ik ? ik.footLeft : null, oldFR = ik ? ik.footRight : null;
            Transform hl = null, hr = null;
            if (ik && kind != "twist")
            {
                hl = new GameObject("Gym hand L").transform;
                hr = new GameObject("Gym hand R").transform;
                Vector3 grip = kind == "pull" ? g + Vector3.up * 2.05f : g + Vector3.up * 1.2f;
                float spread = kind == "pull" ? 0.27f : 0.11f;
                hl.position = grip - right * spread;
                hr.position = grip + right * spread;
                ik.left = hl;
                ik.right = hr;
                ik.weight = 1f;
                ik.vehicle = null;                      // elbow hints in the player's own frame
                ik.footLeft = ik.footRight = null;      // the feet as the clip has them
            }

            var rig = p.Rig;
            Transform body = rig ? rig.transform : null;
            Vector3 rest = body ? body.localPosition : Vector3.zero;
            Quaternion restRot = body ? body.localRotation : Quaternion.identity;
            const float T = 5f;
            for (float t = 0f; t < T; t += Time.deltaTime)
            {
                PlayerStats.UseEnergy(Time.deltaTime * 0.06f);
                float k = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / 1.6f);   // 0 -> 1 -> 0 each rep
                if (body)
                {
                    if (kind == "pull")   // hanging from the bar (feet off the ground), pulled up till the chin nears it
                        body.localPosition = rest + Vector3.up * Mathf.Lerp(0.16f, 0.5f, k * k * (3f - 2f * k));
                    else if (kind == "twist")   // on the disc: the hips turn left and right
                        body.localRotation = restRot * Quaternion.Euler(0f, 38f * Mathf.Sin(t * Mathf.PI * 2f / 1.1f), 0f);
                    else   // pedal / weights: stepping
                        body.localPosition = rest + Vector3.up * (0.05f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f / 0.9f)));
                }
                if (rig) rig.speed = 0f;
                if (p.CurrentMode != PlayerController.Mode.Normal || (p.Health && p.Health.IsDead)) break;
                yield return null;
            }
            if (body) { body.localPosition = rest; body.localRotation = restRot; }
            if (ik)
            {
                if (addedIk) Destroy(ik);
                else { ik.left = oldL; ik.right = oldR; ik.weight = oldW; ik.vehicle = oldV; ik.footLeft = oldFL; ik.footRight = oldFR; }
            }
            if (hl) Destroy(hl.gameObject);
            if (hr) Destroy(hr.gameObject);
            if (ReleaseCamera() && tpc) { tpc.pitch = oldPitch; tpc.yaw = oldYaw; }
            float muscle = kind == "pull" ? 3f : kind == "weights" ? 2.5f : 1.2f;
            float stamina = kind == "pedal" ? 2.5f : kind == "twist" ? 1.5f : 1f;
            PlayerStats.Train(PlayerStats.Stat.Muscle, muscle);
            PlayerStats.Train(PlayerStats.Stat.Stamina, stamina);
            PlayerStats.Burn(1.5f);
            PlayerStats.UseFood(0.02f);
            readyAt = Time.time + 25f;
            GameInput.UiBlocking = FrontEnd.MenuOpen;
            PlayerStats.Working = false;
            busy = false;
            GameEvents.ShowToast("Shabash! Muscle aur stamina badhe, fat ghata", 2.5f);
            Haptics.Success();
        }

        /// <summary>The camera back on the player if it still follows this workout's view (a vehicle / boat override set
        /// meanwhile is left alone). True when it did.</summary>
        bool ReleaseCamera()
        {
            if (!camRig) return false;
            var tpc = ThirdPersonCamera.Instance;
            bool ours = tpc && tpc.OverrideTarget == camRig;
            if (ours) tpc.SetOverride(null, 0f);
            Destroy(camRig.gameObject);
            camRig = null;
            return ours;
        }

        /// <summary>The camera's yaw (ThirdPersonCamera.yaw: it looks that way, from behind) and distance for the workout:
        /// of 12 places round the player, the one with the most room - a sphere cast from the camera's pivot the way
        /// ThirdPersonCamera casts - with a 3/4 front view preferred, then the front, the side, the back.</summary>
        static float PickView(Vector3 pivot, Vector3 face, LayerMask mask, float pitch, out float dist)
        {
            const float want = 4f, least = 2.2f;
            float faceYaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
            float bestScore = -1f, bestYaw = faceYaw + 180f, bestRel = 180f;
            dist = want;
            for (int i = 0; i < 12; i++)
            {
                float rel = i * 30f;                                   // where the camera stands, from the player's facing
                float a = Mathf.Abs(Mathf.DeltaAngle(0f, rel));
                float pref = a >= 25f && a <= 65f ? 1f : a < 25f ? 0.85f : a <= 100f ? 0.8f : a <= 155f ? 0.65f : 0.5f;
                float lookYaw = faceYaw + rel + 180f;                  // the camera at faceYaw + rel, looking back at the player
                Vector3 back = -(Quaternion.Euler(pitch, lookYaw, 0f) * Vector3.forward);
                // the camera's own cast (radius 0.25: a fatter sphere starts inside the bank behind the pull-up frame and
                // misses it, 22 Nov) and a ray, which a collider touching the pivot cannot hide
                float free = want + 0.4f;
                if (Physics.SphereCast(pivot, 0.25f, back, out var hit, free, mask, QueryTriggerInteraction.Ignore)) free = hit.distance;
                if (Physics.Raycast(pivot, back, out var ray, free, mask, QueryTriggerInteraction.Ignore)) free = Mathf.Min(free, ray.distance - 0.25f);
                float score = pref * Mathf.Clamp01((free - 0.6f) / (want - 0.6f));
                if (free < least) score *= 0.3f;
                if (score > bestScore) { bestScore = score; bestYaw = lookYaw; bestRel = rel; dist = Mathf.Clamp(free - 0.35f, 1.6f, want); }
            }
            if (Application.isEditor) Debug.Log($"[Nainital] gym camera: {bestRel:F0} deg from the facing, {dist:F1} m (score {bestScore:F2})");
            return bestYaw;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (busy) { busy = false; PlayerStats.Working = false; GameInput.UiBlocking = FrontEnd.MenuOpen; }
            ReleaseCamera();
        }
    }
}

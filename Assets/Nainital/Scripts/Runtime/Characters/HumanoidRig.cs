using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Mid-poly humanoid built from code plus a procedural animator (walk/run/jump/punch/kick/
    /// block/roll/hit/death/sit/swim/row). Works without any imported model or animation clips.
    /// Movement scripts set the public state fields every frame.
    /// When <see cref="skin"/> is set (a rigged character from <see cref="CharacterLibrary"/>), that
    /// model replaces the code-built body and the same procedural animation drives its bones.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(50)]   // after the movement scripts set speed, before the Animator evaluates
    public class HumanoidRig : MonoBehaviour
    {
        [Header("Look (Pal palette indices)")]
        public int skin = Pal.SkinMid;
        public int hair = Pal.HairBlack;
        public int top = Pal.JacketRed;
        public int legs = Pal.Jeans;
        public int shoes = Pal.ShoeBrown;
        public bool female, woolCap, shawl, tail;
        public float scale = 1f;
        public Material material;

        public enum BodyType { Man, Woman, Boy, Girl, OldMan, OldWoman }

        [Header("Body type (posture + gait)")]
        [Tooltip("Drives posture and walk style: stride, cadence, arm swing, hip sway, step width, stoop, bounce.")]
        public BodyType bodyType = BodyType.Man;

        [Header("Rigged model (optional)")]
        [Tooltip("Skinned character prefab from the CharacterLibrary. Replaces the code-built body; this rig's procedural animation drives its bones.")]
        public GameObject skinModel;
        [Tooltip("Arms at rest hang this many degrees out from vertical; negative = use the body type's default.")]
        public float skinArmOut = -1f;

        /// <summary>Posture and gait for one body type. Angles in degrees, others are multipliers.</summary>
        struct Gait
        {
            public float stride, legSwing, kneeSwing, armSwing, bounce;   // locomotion scales
            public float elbowRest, shoulderOut, armOut;                   // arm posture (armOut: rigged models)
            public float hipYaw, hipRoll, stepIn;                          // pelvis sway, feet toward the midline
            public float stoop, neckUp, kneeRest, hipRest;                 // standing posture
            public float breath;                                           // idle chest movement
            public float playback;                                         // mocap clip speed (elders slower)
        }

        static Gait GaitFor(BodyType b)
        {
            switch (b)
            {
                case BodyType.Woman:    // narrower steps placed near the midline, more hip sway, smaller arm swing
                    return new Gait { stride = 0.92f, legSwing = 0.92f, kneeSwing = 0.95f, armSwing = 0.7f, bounce = 0.85f,
                                      elbowRest = -14f, shoulderOut = 2.5f, armOut = 6.5f, hipYaw = 6f, hipRoll = 4.5f, stepIn = 2.5f,
                                      stoop = -1f, neckUp = 0f, kneeRest = 3f, hipRest = 0f, breath = 1.2f, playback = 1.03f };
                case BodyType.Boy:      // short quick strides, big arm swing, bouncy
                    return new Gait { stride = 0.78f, legSwing = 1.08f, kneeSwing = 1.12f, armSwing = 1.35f, bounce = 1.6f,
                                      elbowRest = -8f, shoulderOut = 6f, armOut = 9f, hipYaw = 3f, hipRoll = 1.5f, stepIn = -1f,
                                      stoop = -2f, neckUp = 0f, kneeRest = 2f, hipRest = 0f, breath = 1.6f, playback = 1.1f };
                case BodyType.Girl:
                    return new Gait { stride = 0.76f, legSwing = 1.02f, kneeSwing = 1.08f, armSwing = 1.15f, bounce = 1.5f,
                                      elbowRest = -11f, shoulderOut = 4f, armOut = 8f, hipYaw = 4f, hipRoll = 2.5f, stepIn = 1f,
                                      stoop = -2f, neckUp = 0f, kneeRest = 2f, hipRest = 0f, breath = 1.6f, playback = 1.1f };
                case BodyType.OldMan:   // stooped, short careful steps, little arm swing, knees kept bent
                    return new Gait { stride = 0.8f, legSwing = 0.68f, kneeSwing = 0.7f, armSwing = 0.5f, bounce = 0.55f,
                                      elbowRest = -18f, shoulderOut = 4f, armOut = 9f, hipYaw = 2f, hipRoll = 1.5f, stepIn = -1.5f,
                                      stoop = 11f, neckUp = -9f, kneeRest = 9f, hipRest = -5f, breath = 0.8f, playback = 0.82f };
                case BodyType.OldWoman:
                    return new Gait { stride = 0.76f, legSwing = 0.62f, kneeSwing = 0.65f, armSwing = 0.42f, bounce = 0.5f,
                                      elbowRest = -20f, shoulderOut = 3f, armOut = 8f, hipYaw = 3f, hipRoll = 2.5f, stepIn = 1f,
                                      stoop = 13f, neckUp = -10f, kneeRest = 8f, hipRest = -4f, breath = 0.8f, playback = 0.8f };
                default:                // Man: steady stride, slight shoulder roll, feet a little apart
                    return new Gait { stride = 1f, legSwing = 1f, kneeSwing = 1f, armSwing = 1f, bounce = 1f,
                                      elbowRest = -9f, shoulderOut = 5f, armOut = 10f, hipYaw = 4f, hipRoll = 2f, stepIn = -1.5f,
                                      stoop = 0f, neckUp = 0f, kneeRest = 3f, hipRest = 0f, breath = 1.3f, playback = 1f };
            }
        }

        /// <summary>Typical walking speed (m/s) for a body type; spawners add their own variation.</summary>
        public static float WalkSpeedFor(BodyType b) => b switch
        {
            BodyType.OldMan => 0.85f, BodyType.OldWoman => 0.8f,
            BodyType.Boy => 1.35f, BodyType.Girl => 1.3f,
            BodyType.Woman => 1.15f, _ => 1.25f,
        };

        // ---- state set by controllers
        [System.NonSerialized] public float speed;
        [System.NonSerialized] public float verticalSpeed;
        [System.NonSerialized] public bool grounded = true, blocking, swimming, seated, rowing, dead;
        /// <summary>Police and the 108 crew (2026-11-18): pistol held out in both hands (aimPitch degrees up), hands up when
        /// arrested, astride a two-wheeler, both hands down on a stretcher's handles.</summary>
        [System.NonSerialized] public bool aiming, handsUp, riding, carrying;
        /// <summary>At a vehicle's wheel (the Driving clip); knocked down (the falling-down clip, then getting up).</summary>
        [System.NonSerialized] public bool driving, knocked;
        /// <summary>Astride behind the rider (with riding): legs as the rider's, hands resting on the thighs (Vehicle
        /// Setup tool, 2026-11-19).</summary>
        [System.NonSerialized] public bool pillion;
        /// <summary>Riding: how far the two-wheeler leans, -1 left .. +1 right (the motorbike pack's lean loops).</summary>
        [System.NonSerialized] public float lean;
        [System.NonSerialized] public float aimPitch;

        public enum Attack { Jab = 0, Cross = 1, Kick = 2, Haymaker = 3 }

        enum J { Hips, Spine, Chest, Neck, Head, ShL, ElL, ShR, ElR, HipL, KnL, HipR, KnR, FtL, FtR, Count }
        const int JC = (int)J.Count;

        readonly Transform[] joints = new Transform[JC];
        readonly Vector3[] tgt = new Vector3[JC];
        Transform model;
        bool built;

        float phase, swimPhase, rowPhase;
        float airW, blockW, seatW, swimW, deadT;
        float attackT = -1f, attackDur = 0.5f, hitT = -1f, rollT = -1f, rollDur = 0.6f;
        Attack attackKind;

        // skinned model: each mapped bone follows a procedural joint: bone.rotation = joint.rotation * offset
        struct SkinBone { public Transform bone; public int joint; public Quaternion offset; }
        SkinBone[] skinBones;
        Transform skinHead;
        float motionScale = 1f;   // body bob / sit drop / swim offsets scale with the model's hip height

        // parents before children, so setting world rotations in this order is consistent
        static readonly string[] SkinBoneNames =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "LeftUpperArm", "LeftLowerArm", "RightUpperArm", "RightLowerArm",
            "LeftUpperLeg", "LeftLowerLeg", "RightUpperLeg", "RightLowerLeg", "LeftFoot", "RightFoot",
        };
        static readonly J[] SkinJoints =
        {
            J.Hips, J.Spine, J.Chest, J.Neck, J.Head,
            J.ShL, J.ElL, J.ShR, J.ElR,
            J.HipL, J.KnL, J.HipR, J.KnR, J.FtL, J.FtR,
        };

        // Mixamo skeleton names for the Humanoid names above (G.O.A.N.'s NPCs are Mixamo-rigged)
        static readonly Dictionary<string, string> MixamoNames = new Dictionary<string, string>
        {
            { "Hips", "Hips" }, { "Spine", "Spine" }, { "Chest", "Spine2" }, { "Neck", "Neck" }, { "Head", "Head" },
            { "LeftUpperArm", "LeftArm" }, { "LeftLowerArm", "LeftForeArm" }, { "LeftHand", "LeftHand" },
            { "RightUpperArm", "RightArm" }, { "RightLowerArm", "RightForeArm" }, { "RightHand", "RightHand" },
            { "LeftUpperLeg", "LeftUpLeg" }, { "LeftLowerLeg", "LeftLeg" }, { "LeftFoot", "LeftFoot" },
            { "RightUpperLeg", "RightUpLeg" }, { "RightLowerLeg", "RightLeg" }, { "RightFoot", "RightFoot" },
        };

        public Transform Model => model;
        public Transform Head => skinHead ? skinHead : joints[(int)J.Head];
        /// <summary>The right hand (a skinned model's hand bone, else the code-built forearm: hand ~0.28 m down it).</summary>
        public Transform RightHand => skinHand ? skinHand : joints[(int)J.ElR];
        public bool HandIsBone => skinHand;
        Transform skinHand;
        float poseW;
        public bool IsSkinned => skinBones != null;

        // Built on enable in both edit mode (so characters show in the Scene view) and play mode.
        // Edit-mode geometry is DontSave, so it never ends up inside the scene file.
        void OnEnable() => Build();

        void OnDisable()
        {
            if (Application.isPlaying || !model) return;
            DestroyImmediate(model.gameObject);
            model = null;
            built = false;
        }

        public void PlayAttack(Attack kind, float duration)
        {
            attackKind = kind; attackDur = Mathf.Max(0.1f, duration); attackT = 0f;
            if (Full) Act(kind == Attack.Jab ? "Jab" : kind == Attack.Cross ? "Cross" : kind == Attack.Kick ? "Kick" : "Hook", duration * 1.3f, duration * 1.15f);
        }
        public void PlayHit() { hitT = 0f; if (Full) Act("Hit", 0.55f, 0.6f); }
        public void PlayRoll(float duration) { rollDur = Mathf.Max(0.1f, duration); rollT = 0f; if (Full) Act("Roll", duration * 1.25f, duration * 1.1f); }
        /// <summary>A shout (the Yelling clip), a wave, talking, a nod, a head shake, an angry gesture (Mixamo, full body).</summary>
        public void PlayGesture(string trigger, float seconds = 0f) { if (Full) Act(trigger, seconds, seconds > 0f ? seconds : ClipLen(trigger) * 0.95f); }
        /// <summary>A pistol shot: the recoil over the arms (Upper layer).</summary>
        public void PlayShoot() { if (Full) { anim.SetFloat(ActSpeedId, 1.6f); anim.SetTrigger("Shoot"); upT = 0.45f; } }

        // ------------------------------------------------------------------ the Mixamo character animator (AnimKit)
        static readonly int ActSpeedId = Animator.StringToHash("ActSpeed");
        static readonly string[] Bools = { "Grounded", "Dead", "Knocked", "Rising", "Seated", "Driving", "Swim", "Aim", "Female", "Ride", "Pillion" };
        static readonly int[] BoolIds = System.Array.ConvertAll(Bools, Animator.StringToHash);
        static readonly int LeanId = Animator.StringToHash("Lean");
        /// <summary>The animator has the motorbike pack's Ride / Pillion states (Asset Store, not in git: AnimKit builds
        /// them only when the pack is imported); without them riding stays the procedural pose. gripLayer: the fingers
        /// closed round the wheel / the grips (-1 = none).</summary>
        bool rideAnim;
        int gripLayer = -1;
        float gripW;
        /// <summary>Riding from the pack's clips now.</summary>
        bool RideNow => riding && rideAnim && Full;
        /// <summary>The motorbike pack's clips are in this character's animator (Ride / Pillion, Mount / Dismount).</summary>
        public bool HasRideClips => rideAnim && Full;
        static Dictionary<string, float> clipLen;
        bool full, wasKnocked;
        float actT, actW, upT, upW, risingT;
        Transform skinSpine, skinChest;

        /// <summary>The character plays every move from the Mixamo clips (the procedural pose only adds arms: hands up,
        /// the stretcher, the guard, and the seated poses on a bike or in a boat).</summary>
        public bool Full => full && Mecanim;
        bool IsFemale => female || bodyType == BodyType.Woman || bodyType == BodyType.OldWoman || bodyType == BodyType.Girl;

        float ClipLen(string state)
        {
            if (clipLen == null && anim && anim.runtimeAnimatorController)
            {
                clipLen = new Dictionary<string, float>();
                foreach (var c in anim.runtimeAnimatorController.animationClips) if (c) clipLen[c.name] = c.length;
            }
            string clip = state switch
            {
                "Jab" => "punch_jab", "Cross" => "punch_cross", "Hook" => "punch_hook", "Kick" => "kick_roundhouse", "Hit" => "hit_reaction",
                "Roll" => "roll", "Yell" => "yelling", "Wave" => "waving", "Talk" => "talking", "Nod" => "gesture_head_nod_yes",
                "Shake" => "gesture_shaking_head_no", "Angry" => "gesture_angry_gesture", "GetUp" => "getting_up",
                "CarEnter" => "car_enter", "CarExit" => "car_exit", "PulledOut" => "car_pulled_out", "RemoveDriver" => "car_remove_driver",
                "Honk" => "car_honk", "StandToSit" => "stand_to_sit", "SitToStand" => "sit_to_stand",
                "Mount" => "AS_Mount_Left", "Dismount" => "AS_Dismount_Left", "BikeStart" => "AS_Bike_Start", _ => state,
            };
            return clipLen != null && clipLen.TryGetValue(clip, out var l) ? l : 1f;
        }

        /// <summary>Play an action (layer 1) so that it lasts `playFor` seconds (0: its own length), the layer on for
        /// `hold` seconds.</summary>
        void Act(string trigger, float playFor, float hold)
        {
            float len = ClipLen(trigger);
            anim.SetFloat(ActSpeedId, playFor > 0.05f ? Mathf.Clamp(len / playFor, 0.5f, 3.5f) : 1f);
            anim.SetTrigger(trigger);
            actT = Mathf.Max(actT, hold);
        }

        void DriveFull(float dt)
        {
            bool clipRide = riding && rideAnim;
            bool sittingProc = (riding && !clipRide) || rowing;   // these stay procedural
            anim.SetBool(BoolIds[0], grounded || swimming || seated || sittingProc || driving || clipRide);
            anim.SetBool(BoolIds[1], dead && !knocked);
            anim.SetBool(BoolIds[2], knocked);
            if (wasKnocked && !knocked && !dead) risingT = ClipLen("GetUp") * 0.95f;
            wasKnocked = knocked;
            risingT -= dt;
            anim.SetBool(BoolIds[3], risingT > 0f);
            anim.SetBool(BoolIds[4], seated && !driving && !sittingProc && !clipRide);
            anim.SetBool(BoolIds[5], driving && !clipRide);
            if (rideAnim)
            {
                anim.SetBool(BoolIds[9], clipRide && !pillion && !dead);
                anim.SetBool(BoolIds[10], clipRide && pillion && !dead);
                anim.SetFloat(LeanId, Mathf.Clamp(lean, -1f, 1f), 0.15f, dt);
            }
            if (gripLayer >= 0)
            {
                gripW = Mathf.MoveTowards(gripW, (driving || (riding && !pillion)) && !dead && !knocked ? 1f : 0f, dt * 6f);
                anim.SetLayerWeight(gripLayer, gripW);
            }
            anim.SetBool(BoolIds[6], swimming);
            anim.SetBool(BoolIds[7], aiming && !seated && !driving);
            anim.SetBool(BoolIds[8], IsFemale);
            actT -= dt;
            actW = Mathf.MoveTowards(actW, actT > 0f && !dead ? 1f : 0f, dt * (actT > 0f ? 14f : 5f));
            anim.SetLayerWeight(1, actW);
            upT -= dt;
            upW = Mathf.MoveTowards(upW, upT > 0f ? 1f : 0f, dt * 12f);
            anim.SetLayerWeight(2, upW);
        }

        /// <summary>Seated or at a wheel (the Sit / Driving clips): the hips onto the seat point, 0.51 m over the
        /// character's root, whatever the character's size (VehicleSeats, benches). On a two-wheeler with the pack's riding
        /// clips: 0.40 m, where the procedural riding pose held them (BikeRide.SeatLocal and the saved setups count on it).</summary>
        void SeatLock()
        {
            bool r = RideNow;
            if ((!(seated || driving) && !r) || (riding && !r) || rowing || dead || knocked || !anim) return;
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (!hips) return;
            Vector3 h = transform.InverseTransformPoint(hips.position);
            float y = r ? VehicleSetup.HipsOverRoot(SeatPose.Ride) : VehicleSeats.HipAboveRoot;
            // this character's own body in this pose (owner, 21 Nov: "perfect precision"): the seat places hips for a
            // nominal seat of the trousers; a fuller / thinner one sits that much higher / lower, and under a roof the head
            // stays inside it
            int kind = r ? (pillion ? 3 : 2) : driving ? 0 : 1;
            if (kind != fitKind)
            {
                fitKind = kind; fitSince = Time.time; fit = CachedFit(kind);
                fitAdj = float.IsNaN(fit.x) ? 0f : Mathf.Clamp(fit.x - NominalButt, -MaxFitAdj, MaxFitAdj);
            }
            if (float.IsNaN(fit.x) && Time.time - fitSince > FitSettle) fit = MeasureFit(kind, hips);
            float adj = 0f;
            if (!float.IsNaN(fit.x))
            {
                // a draped kurta / saree hem can hang under the seat line: no more than MaxFitAdj either way
                adj = Mathf.Clamp(fit.x - NominalButt, -MaxFitAdj, MaxFitAdj);
                if (!float.IsNaN(seatRoof)) adj = Mathf.Min(adj, seatRoof - fit.y - HeadClear - y);
            }
            // eased in when the measure lands (no pop), at once when it was cached
            fitAdj = Mathf.MoveTowards(fitAdj, adj, Time.deltaTime * 0.5f);
            Vector3 want = new Vector3(0f, y + fitAdj, 0f);
            model.localPosition += transform.InverseTransformVector(transform.TransformVector(want - h));
        }

        /// <summary>The seat of the trousers that seat heights count on (VehicleSetup, VehicleSeats): the hips this much
        /// over the cushion's top / a two-wheeler seat's top. Each character's own (measured once per pose) moves it.</summary>
        public const float NominalButt = 0.12f;
        /// <summary>The ankle over the floor / the footpeg that foot targets count on: seated 0.08 m, astride 0.14 m (the
        /// riding clips point the toes down). Each character's own sole moves the foot (HandIK, SoleLift).</summary>
        public const float NominalSoleSeated = 0.08f, NominalSoleAstride = 0.14f;
        const float FitSettle = 0.6f, HeadClear = 0.03f, MaxFitAdj = 0.07f;
        /// <summary>The roof's inside over this character's root when it sits under one (the vehicle setups' seats; NaN =
        /// none): SeatLock keeps its head under it.</summary>
        [System.NonSerialized] public float seatRoof = float.NaN;
        // per skin and pose: (hips over the seat of the trousers, head top over the hips, ankle over the sole), measured
        // once from the baked skin (the town's 12 characters 0.06-0.15 m / 0.75-1.0 m / 0.07-0.08 m seated; the Mixamo
        // ones up to 0.21 m: measured 21 Nov, VehicleSetupShots.Offsets)
        static readonly Dictionary<(Mesh, int), Vector3> fitCache = new Dictionary<(Mesh, int), Vector3>();
        int fitKind = -1;
        float fitSince, fitAdj;
        Vector3 fit = new Vector3(float.NaN, float.NaN, float.NaN);

        /// <summary>This character's own (hips over the seat of the trousers, head top over the hips, ankle over the sole)
        /// in the pose it sits in now (NaN until measured).</summary>
        public Vector3 Fit => fit;

        /// <summary>How much higher than its target this character's ankle goes for its sole to stand where a nominal
        /// sole would (0 until measured). HandIK adds it to the foot targets.</summary>
        public float SoleLift => float.IsNaN(fit.z) || fitKind < 0 ? 0f : fit.z - (fitKind >= 2 ? NominalSoleAstride : NominalSoleSeated);

        Vector3 CachedFit(int kind)
        {
            var key = (FitKey(), kind);
            return key.Item1 && fitCache.TryGetValue(key, out var f) ? f : new Vector3(float.NaN, float.NaN, float.NaN);
        }

        Mesh FitKey()
        {
            var sk = model ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            return sk ? sk.sharedMesh : null;
        }

        /// <summary>The skin as posed now: the lowest point of the middle strip under the hips (a rider's thighs and a kurta's
        /// hem hang lower beside / before it), the top of the head, the lowest point under the left ankle.</summary>
        Vector3 MeasureFit(int kind, Transform hips)
        {
            var f = new Vector3(NominalButt, 0.85f, kind >= 2 ? NominalSoleAstride : NominalSoleSeated);
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            var ankle = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (skins.Length == 0 || !ankle) return f;
            float low = float.MaxValue, top = float.MinValue, sole = float.MaxValue;
            Vector3 hp = hips.position, ap = ankle.position, R = transform.right, U = transform.up, F = transform.forward;
            var m = new Mesh();
            var verts = new List<Vector3>();
            foreach (var sk in skins)
            {
                if (!sk.sharedMesh || !sk.enabled) continue;
                sk.BakeMesh(m, false);   // unscaled: TransformPoint below puts the renderer's own scale on
                m.GetVertices(verts);
                var tr = sk.transform;
                foreach (var v in verts)
                {
                    Vector3 w = tr.TransformPoint(v);
                    Vector3 d = w - hp;
                    float dy = Vector3.Dot(d, U);
                    if (dy > top) top = dy;
                    float dx = Vector3.Dot(d, R), dz = Vector3.Dot(d, F);
                    if (dy < 0f && Mathf.Abs(dx) < 0.04f && dz < 0f && dz > -0.10f && dy < low) low = dy;
                    Vector3 da = w - ap;
                    float ay = Vector3.Dot(da, U);
                    if (ay < 0.05f && (da - U * ay).magnitude < 0.14f && ay < sole) sole = ay;
                }
            }
            if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            if (low < 0f && low > -0.4f) f.x = -low;
            if (top > 0.3f && top < 1.5f) f.y = top;
            if (sole < 0f && sole > -0.3f) f.z = -sole;
            var key = FitKey();
            if (key) fitCache[(key, kind)] = f;
            return f;
        }

        /// <summary>Aimed pistol: the chest tips up or down to the target on top of the clip.</summary>
        void ApplyAimBend()
        {
            if (!aiming || !skinChest || Mathf.Abs(aimPitch) < 0.5f) return;
            Vector3 right = model.right;
            float p = Mathf.Clamp(aimPitch, -40f, 40f);
            if (skinSpine) skinSpine.rotation = Quaternion.AngleAxis(-p * 0.45f, right) * skinSpine.rotation;
            skinChest.rotation = Quaternion.AngleAxis(-p * 0.45f, right) * skinChest.rotation;
        }
        public void CancelAttack() { attackT = -1f; }
        public void Revive() { dead = false; deadT = 0f; knocked = false; }

        // ------------------------------------------------------------------ construction
        Transform Joint(J j, Transform parent, Vector3 local)
        {
            var t = new GameObject(j.ToString()).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            if (!Application.isPlaying) t.gameObject.hideFlags = HideFlags.DontSave;
            joints[(int)j] = t;
            return t;
        }

        void Part(Transform parent, MeshBuilder mb, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = mb.ToMeshSmooth(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            if (!Application.isPlaying) { go.hideFlags = HideFlags.DontSave; mesh.hideFlags = HideFlags.DontSave; }
            mb.Clear();
        }

        public void Build()
        {
            if (built) return;
            if (material == null) material = GameAssets.AtlasMaterial;
            built = true;
            var old = transform.Find("Model");          // leftover from an earlier edit-mode build
            if (old) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            model.localScale = skinModel ? Vector3.one : Vector3.one * scale;   // rigged models are already life-size
            if (!Application.isPlaying) model.gameObject.hideFlags = HideFlags.DontSave;
            skinBones = null;
            skinHead = null;
            motionScale = 1f;

            var hips = Joint(J.Hips, model, new Vector3(0, 0.95f, 0));
            var spine = Joint(J.Spine, hips, new Vector3(0, 0.10f, 0));
            var chest = Joint(J.Chest, spine, new Vector3(0, 0.18f, 0));
            var neck = Joint(J.Neck, chest, new Vector3(0, 0.27f, 0));
            var head = Joint(J.Head, neck, new Vector3(0, 0.06f, 0));
            var shL = Joint(J.ShL, chest, new Vector3(-0.205f, 0.225f, 0));
            var elL = Joint(J.ElL, shL, new Vector3(0, -0.29f, 0));
            var shR = Joint(J.ShR, chest, new Vector3(0.205f, 0.225f, 0));
            var elR = Joint(J.ElR, shR, new Vector3(0, -0.29f, 0));
            var hipL = Joint(J.HipL, hips, new Vector3(-0.095f, -0.05f, 0));
            var knL = Joint(J.KnL, hipL, new Vector3(0, -0.43f, 0));
            var ftL = Joint(J.FtL, knL, new Vector3(0, -0.41f, 0));
            var hipR = Joint(J.HipR, hips, new Vector3(0.095f, -0.05f, 0));
            var knR = Joint(J.KnR, hipR, new Vector3(0, -0.43f, 0));
            var ftR = Joint(J.FtR, knR, new Vector3(0, -0.41f, 0));

            if (skinModel)
            {
                BuildSkin();
                Step(0.25f);   // stand in the body type's posture at once (the bind pose is a T-pose)
                return;
            }

            Vector2 uSkin = Atlas.SolidUV(skin), uHair = Atlas.SolidUV(hair), uTop = Atlas.SolidUV(top);
            Vector2 uLegs = Atlas.SolidUV(legs), uShoes = Atlas.SolidUV(shoes), uDark = Atlas.SolidUV(Pal.HairBlack);
            Vector2 uCap = Atlas.SolidUV(Pal.CapWool), uShawl = Atlas.SolidUV(Pal.Shawl);
            var mb = new MeshBuilder();

            Vector2 uWhite = Atlas.SolidUV(Pal.ShirtWhite), uLip = Atlas.SolidUV(Pal.TempleRed), uBelt = Atlas.SolidUV(Pal.WoodDark);
            Vector2 uSole = Atlas.SolidUV(Pal.HairBlack);

            // Pelvis (+ saree/skirt) + belt
            mb.Sphere(new Vector3(0, -0.02f, 0), new Vector3(0.158f, 0.13f, 0.118f), 14, 8, uLegs);
            mb.Cylinder(new Vector3(0, 0.05f, 0), 0.165f, 0.165f, 0.045f, 16, uBelt, false, false, 0.76f);
            if (female) mb.Cylinder(new Vector3(0, -0.66f, 0), 0.29f, 0.165f, 0.62f, 16, uLegs, false, false, 0.8f);
            if (tail)
            {
                mb.Limb(new Vector3(0, -0.05f, -0.13f), new Vector3(0, -0.25f, -0.6f), 0.035f, 0.025f, 6, uLegs);
                mb.Limb(new Vector3(0, -0.25f, -0.6f), new Vector3(0, 0.15f, -0.85f), 0.025f, 0.015f, 6, uLegs);
            }
            Part(hips, mb, "Pelvis");

            mb.Cylinder(new Vector3(0, -0.02f, 0), 0.155f, 0.168f, 0.22f, 14, uTop, false, false, 0.72f);
            Part(spine, mb, "Abdomen");

            // chest with rounded shoulders, collar, zip line
            mb.Cylinder(Vector3.zero, 0.168f, 0.198f, 0.2f, 14, uTop, false, false, 0.7f);
            mb.Sphere(new Vector3(0, 0.2f, 0), new Vector3(0.205f, 0.075f, 0.142f), 14, 6, uTop, 0f, 90f);
            mb.Sphere(new Vector3(-0.19f, 0.215f, 0), new Vector3(0.07f, 0.065f, 0.07f), 10, 6, uTop);
            mb.Sphere(new Vector3(0.19f, 0.215f, 0), new Vector3(0.07f, 0.065f, 0.07f), 10, 6, uTop);
            mb.Cylinder(new Vector3(0, 0.24f, -0.005f), 0.075f, 0.068f, 0.05f, 12, uTop, false, false, 1.05f);   // collar
            if (!female) mb.Box(new Vector3(0, 0.1f, 0.142f), new Vector3(0.012f, 0.24f, 0.01f), uDark);         // zip
            if (shawl) mb.Cylinder(new Vector3(0, 0.12f, 0), 0.215f, 0.12f, 0.16f, 14, uShawl, false, false, 0.75f);
            Part(chest, mb, "Chest");

            mb.Cylinder(new Vector3(0, -0.02f, 0), 0.05f, 0.046f, 0.1f, 10, uSkin, false, false);
            Part(neck, mb, "Neck");

            // head: skull, jaw, ears, brows, eyes (white + iris), nose, lips, hair
            mb.Sphere(new Vector3(0, 0.12f, 0.004f), new Vector3(0.092f, 0.112f, 0.104f), 16, 12, uSkin);
            mb.Sphere(new Vector3(0, 0.06f, 0.02f), new Vector3(0.072f, 0.06f, 0.082f), 12, 8, uSkin);           // jaw
            mb.Sphere(new Vector3(-0.091f, 0.118f, 0f), new Vector3(0.014f, 0.03f, 0.022f), 8, 6, uSkin);        // ears
            mb.Sphere(new Vector3(0.091f, 0.118f, 0f), new Vector3(0.014f, 0.03f, 0.022f), 8, 6, uSkin);
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Sphere(new Vector3(sx * 0.033f, 0.128f, 0.093f), new Vector3(0.017f, 0.011f, 0.008f), 8, 6, uWhite);
                mb.Sphere(new Vector3(sx * 0.033f, 0.128f, 0.1f), new Vector3(0.0075f, 0.0075f, 0.004f), 6, 4, uDark);
                mb.Box(new Vector3(sx * 0.034f, 0.148f, 0.098f), new Vector3(0.03f, 0.007f, 0.01f), Quaternion.Euler(0, 0, sx * -8f), uHair);
            }
            mb.Sphere(new Vector3(0, 0.104f, 0.107f), new Vector3(0.013f, 0.027f, 0.017f), 8, 6, uSkin);          // nose
            mb.Sphere(new Vector3(0, 0.068f, 0.098f), new Vector3(0.024f, 0.007f, 0.008f), 8, 4, uLip);           // lips
            if (woolCap)
            {
                mb.Cylinder(new Vector3(0, 0.155f, -0.004f), 0.099f, 0.096f, 0.1f, 16, uCap, true, false, 1.08f);
                mb.Sphere(new Vector3(0, 0.125f, -0.012f), new Vector3(0.098f, 0.06f, 0.108f), 14, 3, uHair, 0f, 40f);
            }
            else
            {
                mb.Sphere(new Vector3(0, 0.138f, -0.012f), new Vector3(0.099f, 0.108f, 0.112f), 16, 8, uHair, 10f, 90f);
                mb.Sphere(new Vector3(-0.02f, 0.2f, 0.035f), new Vector3(0.07f, 0.035f, 0.07f), 10, 5, uHair);    // side part volume
            }
            if (female)
            {
                mb.Sphere(new Vector3(0, 0.06f, -0.085f), new Vector3(0.09f, 0.16f, 0.05f), 12, 8, uHair);
                mb.Sphere(new Vector3(0, -0.06f, -0.09f), new Vector3(0.04f, 0.05f, 0.04f), 8, 6, uHair);         // bun / plait end
            }
            Part(head, mb, "Head");

            foreach (var (sh, el) in new[] { (shL, elL), (shR, elR) })
            {
                mb.Limb(Vector3.zero, new Vector3(0, -0.29f, 0), 0.058f, 0.048f, 12, uTop);
                Part(sh, mb, "UpperArm");
                mb.Limb(Vector3.zero, new Vector3(0, -0.22f, 0), 0.047f, 0.04f, 12, uTop);
                mb.Cylinder(new Vector3(0, -0.235f, 0), 0.04f, 0.033f, 0.02f, 10, uSkin, false, false);          // wrist
                mb.Sphere(new Vector3(0, -0.285f, 0.006f), new Vector3(0.034f, 0.05f, 0.02f), 10, 8, uSkin);     // palm
                mb.Sphere(new Vector3(0, -0.335f, 0.008f), new Vector3(0.03f, 0.03f, 0.017f), 8, 6, uSkin);      // fingers
                mb.Sphere(new Vector3(0.02f, -0.29f, 0.02f), new Vector3(0.012f, 0.03f, 0.012f), 6, 5, uSkin);   // thumb
                Part(el, mb, "Forearm");
            }
            foreach (var (hp, kn, ft) in new[] { (hipL, knL, ftL), (hipR, knR, ftR) })
            {
                mb.Limb(Vector3.zero, new Vector3(0, -0.43f, 0), 0.085f, 0.064f, 12, uLegs);
                Part(hp, mb, "Thigh");
                mb.Limb(Vector3.zero, new Vector3(0, -0.41f, 0), 0.062f, 0.046f, 12, uLegs);
                mb.Sphere(new Vector3(0, 0, 0.012f), new Vector3(0.062f, 0.05f, 0.06f), 10, 6, uLegs);            // knee
                Part(kn, mb, "Shin");
                mb.Sphere(new Vector3(0, -0.025f, 0.055f), new Vector3(0.05f, 0.042f, 0.13f), 12, 8, uShoes);
                mb.Box(new Vector3(0, -0.062f, 0.055f), new Vector3(0.1f, 0.022f, 0.26f), uSole);
                Part(ft, mb, "Foot");
            }
        }

        /// <summary>
        /// Instantiates the rigged model under <see cref="Model"/> and maps its Humanoid-named bones
        /// to this rig's joints. Each bone's offset is taken in the bind pose, after swinging the
        /// A-pose arms down to the body type's rest angle, so the procedural rest pose (all joint
        /// rotations zero) shows the character standing naturally.
        /// </summary>
        void BuildSkin()
        {
            var inst = Instantiate(skinModel, model, false);
            inst.name = "Skin";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            var bones = new Dictionary<string, Transform>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = gameObject.layer;
                if (!Application.isPlaying) t.gameObject.hideFlags = HideFlags.DontSave;
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            }
            // Humanoid names (our Blender rigs) first, then Mixamo names (with or without "mixamorig:")
            Transform B(string n)
            {
                if (bones.TryGetValue(n, out var t)) return t;
                if (!MixamoNames.TryGetValue(n, out var m)) return null;
                return bones.TryGetValue("mixamorig:" + m, out t) ? t : bones.TryGetValue(m, out t) ? t : null;
            }

            // face +Z: the character's left leg must be on the model's -X side (turn about Y only)
            var ll = B("LeftUpperLeg");
            var rl = B("RightUpperLeg");
            if (ll && rl)
            {
                Vector3 lr = model.InverseTransformVector(ll.position - rl.position);
                lr.y = 0f;
                if (lr.sqrMagnitude > 1e-6f)
                    inst.transform.localRotation = Quaternion.AngleAxis(Vector3.SignedAngle(lr, Vector3.left, Vector3.up), Vector3.up);
            }
            var hipsBone = B("Hips");
            if (hipsBone) motionScale = Mathf.Clamp(model.InverseTransformPoint(hipsBone.position).y / 0.95f, 0.4f, 1.3f);

            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bnd = smr.localBounds;
                bnd.Expand(0.8f);              // room for lying down / swimming without being culled
                smr.localBounds = bnd;
            }

            float armOut = skinArmOut >= 0f ? skinArmOut : GaitFor(bodyType).armOut;
            var list = new List<SkinBone>();
            for (int i = 0; i < SkinBoneNames.Length; i++)
            {
                var b = B(SkinBoneNames[i]);
                if (!b) continue;
                Quaternion k = Quaternion.identity;
                string child = SkinBoneNames[i] switch
                {
                    "LeftUpperArm" => "LeftLowerArm", "LeftLowerArm" => "LeftHand",
                    "RightUpperArm" => "RightLowerArm", "RightLowerArm" => "RightHand",
                    _ => null,
                };
                var c = child != null ? B(child) : null;
                if (c)
                {
                    bool left = SkinBoneNames[i].StartsWith("Left");
                    Vector3 target = model.rotation * (Quaternion.Euler(0, 0, left ? -armOut : armOut) * Vector3.down);
                    k = Quaternion.FromToRotation(c.position - b.position, target);
                }
                var j = joints[(int)SkinJoints[i]];
                list.Add(new SkinBone { bone = b, joint = (int)SkinJoints[i], offset = Quaternion.Inverse(j.rotation) * k * b.rotation });
            }
            skinBones = list.Count > 0 ? list.ToArray() : null;
            skinHead = B("Head");
            skinHand = B("RightHand");
            if (skinBones == null) Debug.LogWarning($"[Nainital] {name}: skin '{skinModel.name}' has no Humanoid-named bones; it will not animate.", this);

            // Humanoid avatar + the G.O.A.N. locomotion controller: mocap idle/walk/run in play mode
            anim = inst.GetComponentInChildren<Animator>(true);
            if (anim && anim.avatar && anim.avatar.isHuman && Locomotion)
            {
                anim.runtimeAnimatorController = Locomotion;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                full = false;
                rideAnim = false;
                foreach (var prm in anim.parameters) { if (prm.name == "Knocked") full = true; if (prm.name == "Ride") rideAnim = true; }
                // the pack's clips may be missing on a PC that has not imported it (not in git): then the states are empty
                bool rideClip = false, gripClip = false;
                foreach (var c in anim.runtimeAnimatorController.animationClips)
                {
                    if (!c) continue;
                    if (c.name == "AS_Idle_Riding") rideClip = true;
                    if (c.name == "Human@ObjectGripHands01") gripClip = true;
                }
                rideAnim = rideAnim && rideClip;
                gripLayer = gripClip ? anim.GetLayerIndex("Grip") : -1;
                skinSpine = B("Spine");
                skinChest = B("Chest");
            }
            else anim = null;
        }

        // ------------------------------------------------------------------ mecanim locomotion
        static RuntimeAnimatorController locomotion;
        static bool lookedForLocomotion;
        static readonly int SpeedId = Animator.StringToHash("Speed");
        const float WalkRef = 1.5f;   // speed (m/s) of the walking clip in the blend tree
        const float RunRef = 4.4f;   // and of the run (Mixamo running / G.O.A.N. fast run)

        /// <summary>Resources/NainitalLocomotion (idle / walk / run from G.O.A.N.), built by the editor.</summary>
        public static RuntimeAnimatorController Locomotion
        {
            get
            {
                if (!lookedForLocomotion)
                {
                    // the Mixamo character animator (AnimKit) when it is built, else the old idle / walk / run one
                    locomotion = Resources.Load<RuntimeAnimatorController>("NainitalCharacter");
                    if (!locomotion) locomotion = Resources.Load<RuntimeAnimatorController>("NainitalLocomotion");
                    lookedForLocomotion = true;
                }
                return locomotion;
            }
        }

        Animator anim;
        float fullW, upperW, kickW;    // how much the procedural pose overrides the mocap pose
        [System.NonSerialized] public bool previewMecanim;   // editor captures: use the Animator outside play mode

        bool Mecanim => anim && anim.isActiveAndEnabled && (Application.isPlaying || previewMecanim);
        public Animator Anim => anim;

        static readonly bool[] UpperBody = MaskOf(J.Spine, J.Chest, J.Neck, J.Head, J.ShL, J.ElL, J.ShR, J.ElR);
        static readonly bool[] KickLeg = MaskOf(J.HipR, J.KnR, J.FtR);

        static bool[] MaskOf(params J[] js)
        {
            var m = new bool[JC];
            foreach (var j in js) m[(int)j] = true;
            return m;
        }

        // drive the Animator after movement scripts have set speed this frame
        void Update()
        {
            if (!Mecanim || !Application.isPlaying) return;
            DriveAnimator(speed, Time.deltaTime);
            if (full) DriveFull(Time.deltaTime);
        }

        /// <summary>Speed (m/s) to blend-tree parameter + playback rate, so slow walkers still take whole steps.</summary>
        public void DriveAnimator(float sp, float dt)
        {
            if (!anim) return;
            float playback = GaitFor(bodyType).playback, param;
            if (sp < 0.15f) param = 0f;
            else if (sp < WalkRef) { param = WalkRef; playback *= Mathf.Max(0.55f, sp / WalkRef); }
            else if (sp <= RunRef) param = sp;
            else { param = RunRef; playback *= sp / RunRef; }
            if (dt > 0f) anim.SetFloat(SpeedId, param, 0.12f, dt);
            else anim.SetFloat(SpeedId, param);
            anim.speed = playback;
        }

        void ApplySkin()
        {
            bool mec = Mecanim;
            for (int i = 0; i < skinBones.Length; i++)
            {
                var s = skinBones[i];
                float w = 1f;
                if (mec)
                {
                    w = fullW;
                    if (UpperBody[s.joint]) w = Mathf.Max(w, upperW);
                    if (KickLeg[s.joint]) w = Mathf.Max(w, kickW);
                }
                if (w <= 0.001f) continue;
                Quaternion proc = joints[s.joint].rotation * s.offset;
                s.bone.rotation = w >= 0.999f ? proc : Quaternion.Slerp(s.bone.rotation, proc, w);
            }
            if (mec) ApplyPosture();
        }

        /// <summary>Body-type posture on top of the mocap clips (the elders' stoop, a raised chin).</summary>
        void ApplyPosture()
        {
            var g = GaitFor(bodyType);
            float k = 1f - fullW;
            if (k <= 0f || (Mathf.Abs(g.stoop) < 0.01f && Mathf.Abs(g.neckUp) < 0.01f)) return;
            Vector3 right = model.right;
            for (int i = 0; i < skinBones.Length; i++)
            {
                var s = skinBones[i];
                float a = s.joint == (int)J.Spine ? g.stoop * 0.55f : s.joint == (int)J.Chest ? g.stoop * 0.45f : s.joint == (int)J.Neck ? g.neckUp : 0f;
                if (a != 0f) s.bone.rotation = Quaternion.AngleAxis(a * k, right) * s.bone.rotation;
            }
        }

        // ------------------------------------------------------------------ animation
        static readonly Vector3[] Guard = Pose(
            (J.ShL, new Vector3(-70, 0, 22)), (J.ElL, new Vector3(-115, 0, 0)),
            (J.ShR, new Vector3(-70, 0, -22)), (J.ElR, new Vector3(-115, 0, 0)),
            (J.Chest, new Vector3(6, 0, 0)), (J.Spine, new Vector3(4, 0, 0)));

        static readonly Vector3[][] Windup =
        {
            Pose((J.ShL, new Vector3(-55, 0, 16)), (J.ElL, new Vector3(-128, 0, 0)), (J.ShR, new Vector3(-70, 0, -22)), (J.ElR, new Vector3(-115, 0, 0)), (J.Chest, new Vector3(6, -12, 0))),
            Pose((J.ShR, new Vector3(-40, 0, -14)), (J.ElR, new Vector3(-122, 0, 0)), (J.ShL, new Vector3(-70, 0, 22)), (J.ElL, new Vector3(-115, 0, 0)), (J.Chest, new Vector3(6, 16, 0)), (J.Spine, new Vector3(2, 8, 0))),
            Pose((J.HipR, new Vector3(-50, 0, 0)), (J.KnR, new Vector3(100, 0, 0)), (J.ShL, new Vector3(-60, 0, 30)), (J.ElL, new Vector3(-110, 0, 0)), (J.ShR, new Vector3(-50, 0, -30)), (J.ElR, new Vector3(-100, 0, 0)), (J.Chest, new Vector3(-4, 0, 0))),
            Pose((J.ShR, new Vector3(-20, 0, 60)), (J.ElR, new Vector3(-80, 0, 0)), (J.ShL, new Vector3(-60, 0, 25)), (J.ElL, new Vector3(-100, 0, 0)), (J.Chest, new Vector3(0, 25, 0)), (J.Spine, new Vector3(0, 10, 0))),
        };

        static readonly Vector3[][] Strike =
        {
            Pose((J.ShL, new Vector3(-88, 0, 6)), (J.ElL, new Vector3(-6, 0, 0)), (J.ShR, new Vector3(-70, 0, -22)), (J.ElR, new Vector3(-115, 0, 0)), (J.Chest, new Vector3(4, 24, 0)), (J.Spine, new Vector3(0, 10, 0))),
            Pose((J.ShR, new Vector3(-88, 0, -6)), (J.ElR, new Vector3(-6, 0, 0)), (J.ShL, new Vector3(-70, 0, 22)), (J.ElL, new Vector3(-115, 0, 0)), (J.Chest, new Vector3(4, -30, 0)), (J.Spine, new Vector3(0, -12, 0))),
            Pose((J.HipR, new Vector3(-88, 0, 0)), (J.KnR, new Vector3(4, 0, 0)), (J.FtR, new Vector3(-20, 0, 0)), (J.ShL, new Vector3(-50, 0, 40)), (J.ElL, new Vector3(-100, 0, 0)), (J.ShR, new Vector3(-20, 0, -45)), (J.ElR, new Vector3(-60, 0, 0)), (J.Chest, new Vector3(-14, 0, 0)), (J.Spine, new Vector3(-6, 0, 0))),
            Pose((J.ShR, new Vector3(-85, 0, -25)), (J.ElR, new Vector3(-20, 0, 0)), (J.ShL, new Vector3(-60, 0, 25)), (J.ElL, new Vector3(-100, 0, 0)), (J.Chest, new Vector3(5, -38, 0)), (J.Spine, new Vector3(0, -14, 0))),
        };

        static readonly bool[] KickMask = Mask(true);
        static readonly bool[] PunchMask = Mask(false);

        static Vector3[] Pose(params (J j, Vector3 e)[] items)
        {
            var p = new Vector3[JC];
            foreach (var (j, e) in items) p[(int)j] = e;
            return p;
        }

        static bool[] Mask(bool includeLegs)
        {
            var m = new bool[JC];
            m[(int)J.Spine] = m[(int)J.Chest] = m[(int)J.ShL] = m[(int)J.ElL] = m[(int)J.ShR] = m[(int)J.ElR] = true;
            if (includeLegs) m[(int)J.HipR] = m[(int)J.KnR] = m[(int)J.FtR] = true;
            return m;
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

        void Set(J j, float x, float y = 0, float z = 0) => tgt[(int)j] = new Vector3(x, y, z);
        void Add(J j, float x, float y = 0, float z = 0) => tgt[(int)j] += new Vector3(x, y, z);

        float animAccum;

        void LateUpdate()
        {
            if (!built) return;
            // animation LOD: far-away people animate every 3rd / 6th frame
            animAccum += Time.deltaTime;
            if (Application.isPlaying)
            {
                var cam = Camera.main;
                if (cam)
                {
                    float d2 = (cam.transform.position - transform.position).sqrMagnitude;
                    int every = d2 > 140f * 140f ? 6 : d2 > 55f * 55f ? 3 : 1;
                    if (every > 1 && (Time.frameCount + (GetInstanceID() & 0xFF)) % every != 0)
                    {
                        if (skinBones != null && Mecanim) { ApplySkin(); if (Full) { ApplyAimBend(); SeatLock(); } }   // the Animator rewrote the bones this frame
                        return;
                    }
                }
            }
            float dt = Mathf.Min(animAccum, 0.15f);
            animAccum = 0f;
            Step(dt);
        }

        /// <summary>Advances the procedural animation by dt (LateUpdate calls it; editor captures can too).</summary>
        public void Step(float dt)
        {
            if (!built) return;
            float t = Time.time;
            for (int i = 0; i < JC; i++) tgt[i] = Vector3.zero;
            Vector3 mPos = Vector3.zero;
            Vector3 mRot = Vector3.zero;

            // ---- idle + locomotion (shaped by the body type's gait)
            var gait = GaitFor(bodyType);
            float walk01 = Mathf.Clamp01(speed / 1.6f);
            float run01 = Mathf.Clamp01((speed - 2.4f) / 3.8f);
            float stride = Mathf.Lerp(1.35f, 2.7f, run01) * gait.stride;
            phase += dt * speed / stride * Mathf.PI * 2f;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float upright = 1f - 0.7f * run01;   // running straightens the elder's stoop a little

            // standing posture
            Set(J.Chest, Mathf.Sin(t * 1.7f) * 1.3f * gait.breath + gait.stoop * 0.45f * upright);
            Set(J.Spine, gait.stoop * 0.55f * upright);
            Set(J.Neck, gait.neckUp * upright);
            Set(J.ShL, 0, 0, -gait.shoulderOut); Set(J.ShR, 0, 0, gait.shoulderOut);
            Set(J.ElL, gait.elbowRest); Set(J.ElR, gait.elbowRest);
            Set(J.HipL, gait.hipRest, 0, gait.stepIn); Set(J.HipR, gait.hipRest, 0, -gait.stepIn);
            Set(J.KnL, gait.kneeRest); Set(J.KnR, gait.kneeRest);

            float hipAmp = (walk01 * 26f + run01 * 20f) * gait.legSwing;
            Add(J.HipL, -hipAmp * s); Add(J.HipR, hipAmp * s);
            float kneeAmp = (walk01 * 32f + run01 * 58f) * gait.kneeSwing;
            Add(J.KnL, kneeAmp * Mathf.Max(0f, c)); Add(J.KnR, kneeAmp * Mathf.Max(0f, -c));
            Add(J.FtL, -8f * walk01 * Mathf.Max(0f, -s)); Add(J.FtR, -8f * walk01 * Mathf.Max(0f, s));
            float armAmp = (walk01 * 20f + run01 * 30f) * gait.armSwing;
            Add(J.ShL, armAmp * s); Add(J.ShR, -armAmp * s);
            Add(J.ElL, -(walk01 * 10f + run01 * 72f)); Add(J.ElR, -(walk01 * 10f + run01 * 72f));
            Add(J.Spine, 3f * walk01 + 9f * run01, 6f * s * walk01);
            Add(J.Chest, 0, -5f * s * walk01);
            // pelvis sway: yaw with the stride, roll toward the stance leg; the spine counters it
            float yaw = gait.hipYaw * walk01 * s, roll = gait.hipRoll * walk01 * c;
            Add(J.Hips, 0, yaw, roll);
            Add(J.Spine, 0, -yaw * 0.8f, -roll * 0.7f);
            if (!Mecanim)   // the mocap clips bob the body themselves
            {
                mPos.y -= (0.012f + 0.035f * run01) * walk01 * s * s * gait.bounce;
                mPos.y -= (1f - Mathf.Cos(gait.kneeRest * Mathf.Deg2Rad)) * 0.43f;   // bent knees lower the body
            }

            // ---- air
            airW = Mathf.MoveTowards(airW, grounded || swimming ? 0f : 1f, dt * 7f);
            if (airW > 0f)
            {
                bool falling = verticalSpeed < -4f;
                Blend(J.HipL, new Vector3(falling ? -18 : -42, 0, 0), airW);
                Blend(J.KnL, new Vector3(falling ? 25 : 75, 0, 0), airW);
                Blend(J.HipR, new Vector3(falling ? -4 : 12, 0, 0), airW);
                Blend(J.KnR, new Vector3(falling ? 15 : 35, 0, 0), airW);
                Blend(J.ShL, new Vector3(-25, 0, -38), airW);
                Blend(J.ShR, new Vector3(-25, 0, 38), airW);
                Blend(J.ElL, new Vector3(-35, 0, 0), airW);
                Blend(J.ElR, new Vector3(-35, 0, 0), airW);
            }

            // ---- block / guard
            blockW = Mathf.MoveTowards(blockW, blocking ? 1f : 0f, dt * 9f);
            if (blockW > 0f)
                for (int i = 0; i < JC; i++)
                    if (PunchMask[i]) tgt[i] = Vector3.Lerp(tgt[i], Guard[i], blockW);

            // ---- attacks
            bool attacking = attackT >= 0f;
            if (attacking)
            {
                attackT += dt;
                float u = attackT / attackDur;
                if (u >= 1f) attackT = -1f;
                else
                {
                    int k = (int)attackKind;
                    var mask = attackKind == Attack.Kick ? KickMask : PunchMask;
                    for (int i = 0; i < JC; i++)
                    {
                        if (!mask[i]) continue;
                        Vector3 g = Guard[i];
                        Vector3 e = u < 0.28f ? Vector3.Lerp(g, Windup[k][i], Smooth(u / 0.28f))
                                  : u < 0.45f ? Vector3.Lerp(Windup[k][i], Strike[k][i], Smooth((u - 0.28f) / 0.17f))
                                  : Vector3.Lerp(Strike[k][i], g, Smooth((u - 0.45f) / 0.55f));
                        tgt[i] = e;
                    }
                }
            }

            // ---- hit reaction
            if (hitT >= 0f)
            {
                hitT += dt;
                float w = 1f - Smooth(hitT / 0.4f);
                if (hitT > 0.4f) hitT = -1f;
                Add(J.Chest, -20f * w); Add(J.Spine, -8f * w); Add(J.Head, -14f * w);
                Add(J.ShL, 15f * w, 0, -20f * w); Add(J.ShR, 15f * w, 0, 20f * w);
            }

            // ---- seated / rowing
            seatW = Mathf.MoveTowards(seatW, seated || rowing || riding ? 1f : 0f, dt * 5f);
            if (seatW > 0f && riding)
            {
                // astride a bike: knees out round the tank, feet back on the pegs, hands forward on the bars
                Blend(J.HipL, new Vector3(-62, 0, -14), seatW); Blend(J.HipR, new Vector3(-62, 0, 14), seatW);
                Blend(J.KnL, new Vector3(96, 0, 0), seatW); Blend(J.KnR, new Vector3(96, 0, 0), seatW);
                Blend(J.FtL, new Vector3(-12, 0, 0), seatW); Blend(J.FtR, new Vector3(-12, 0, 0), seatW);
                if (pillion)
                {
                    // the pillion: upright, upper arms down, forearms forward so the hands rest on the thighs
                    Blend(J.ShL, new Vector3(-18, 0, 10), seatW); Blend(J.ShR, new Vector3(-18, 0, -10), seatW);
                    Blend(J.ElL, new Vector3(-62, 0, 0), seatW); Blend(J.ElR, new Vector3(-62, 0, 0), seatW);
                    Blend(J.Spine, new Vector3(3, 0, 0), seatW);
                }
                else
                {
                    Blend(J.ShL, new Vector3(-58, 0, 12), seatW); Blend(J.ShR, new Vector3(-58, 0, -12), seatW);
                    Blend(J.ElL, new Vector3(-28, 0, 0), seatW); Blend(J.ElR, new Vector3(-28, 0, 0), seatW);
                    Blend(J.Spine, new Vector3(12, 0, 0), seatW);
                }
                mPos.y = Mathf.Lerp(mPos.y, -0.44f, seatW);
            }
            else if (seatW > 0f)
            {
                Blend(J.HipL, new Vector3(-86, 0, -4), seatW); Blend(J.HipR, new Vector3(-86, 0, 4), seatW);
                Blend(J.KnL, new Vector3(86, 0, 0), seatW); Blend(J.KnR, new Vector3(86, 0, 0), seatW);
                Blend(J.FtL, Vector3.zero, seatW); Blend(J.FtR, Vector3.zero, seatW);
                if (rowing)
                {
                    rowPhase += dt * Mathf.Lerp(2f, 5f, Mathf.Clamp01(speed / 4f));
                    float r = Mathf.Sin(rowPhase);
                    Blend(J.ShL, new Vector3(-55 + 28 * r, 0, 12), seatW); Blend(J.ShR, new Vector3(-55 + 28 * r, 0, -12), seatW);
                    Blend(J.ElL, new Vector3(-55 - 30 * r, 0, 0), seatW); Blend(J.ElR, new Vector3(-55 - 30 * r, 0, 0), seatW);
                    Blend(J.Spine, new Vector3(6 + 10 * r, 0, 0), seatW);
                }
                else
                {
                    Blend(J.ShL, new Vector3(-30, 0, 4), seatW); Blend(J.ShR, new Vector3(-30, 0, -4), seatW);
                    Blend(J.ElL, new Vector3(-35, 0, 0), seatW); Blend(J.ElR, new Vector3(-35, 0, 0), seatW);
                }
                mPos.y = Mathf.Lerp(mPos.y, -0.44f, seatW);
            }

            // ---- pistol out / hands up / stretcher (upper body over the walk)
            bool posed = ((aiming && !Full) || handsUp || carrying) && !dead;   // Full: the pistol clips hold the gun
            poseW = Mathf.MoveTowards(poseW, posed ? 1f : 0f, dt * 8f);
            if (poseW > 0f)
            {
                if (handsUp)
                {
                    Blend(J.ShL, new Vector3(-162, 0, -16), poseW); Blend(J.ShR, new Vector3(-162, 0, 16), poseW);
                    Blend(J.ElL, new Vector3(-22, 0, 0), poseW); Blend(J.ElR, new Vector3(-22, 0, 0), poseW);
                }
                else if (aiming)
                {
                    float p = Mathf.Clamp(aimPitch, -40f, 40f);
                    Blend(J.ShR, new Vector3(-88 - p, 0, -4), poseW); Blend(J.ElR, new Vector3(-4, 0, 0), poseW);
                    Blend(J.ShL, new Vector3(-80 - p, 0, 32), poseW); Blend(J.ElL, new Vector3(-24, 0, 0), poseW);
                    Blend(J.Chest, new Vector3(2, 10, 0), poseW);
                }
                else
                {
                    Blend(J.ShL, new Vector3(-28, 0, 8), poseW); Blend(J.ShR, new Vector3(-28, 0, -8), poseW);
                    Blend(J.ElL, new Vector3(-24, 0, 0), poseW); Blend(J.ElR, new Vector3(-24, 0, 0), poseW);
                }
            }

            // ---- swimming
            swimW = Mathf.MoveTowards(swimW, swimming ? 1f : 0f, dt * 4f);
            if (swimW > 0f)
            {
                swimPhase += dt * (2.2f + speed * 0.8f);
                float a = swimPhase;
                Blend(J.ShL, new Vector3(-90 + 100 * Mathf.Sin(a), 0, -10), swimW);
                Blend(J.ShR, new Vector3(-90 - 100 * Mathf.Sin(a), 0, 10), swimW);
                Blend(J.ElL, new Vector3(-20, 0, 0), swimW); Blend(J.ElR, new Vector3(-20, 0, 0), swimW);
                Blend(J.HipL, new Vector3(16 * Mathf.Sin(a * 2f), 0, 0), swimW);
                Blend(J.HipR, new Vector3(-16 * Mathf.Sin(a * 2f), 0, 0), swimW);
                Blend(J.KnL, new Vector3(10, 0, 0), swimW); Blend(J.KnR, new Vector3(10, 0, 0), swimW);
                Blend(J.Head, new Vector3(-50, 0, 0), swimW);
                mRot.x = Mathf.Lerp(mRot.x, 72f, swimW);
                mPos.y += 0.45f * swimW;
                mPos.z -= 0.5f * swimW;
            }

            // ---- roll
            bool rolling = rollT >= 0f;
            Quaternion rollRot = Quaternion.identity;
            if (rolling)
            {
                rollT += dt;
                float u = Mathf.Clamp01(rollT / rollDur);
                if (rollT >= rollDur) rolling = false;
                float tuck = Mathf.Sin(u * Mathf.PI);
                Blend(J.HipL, new Vector3(-100, 0, 0), tuck); Blend(J.HipR, new Vector3(-100, 0, 0), tuck);
                Blend(J.KnL, new Vector3(125, 0, 0), tuck); Blend(J.KnR, new Vector3(125, 0, 0), tuck);
                Blend(J.ShL, new Vector3(-60, 0, 10), tuck); Blend(J.ShR, new Vector3(-60, 0, -10), tuck);
                Blend(J.ElL, new Vector3(-100, 0, 0), tuck); Blend(J.ElR, new Vector3(-100, 0, 0), tuck);
                Blend(J.Spine, new Vector3(35, 0, 0), tuck); Blend(J.Chest, new Vector3(25, 0, 0), tuck);
                Blend(J.Head, new Vector3(30, 0, 0), tuck);
                rollRot = Quaternion.Euler(360f * Smooth(u), 0, 0);
                Vector3 pivot = new Vector3(0, 0.5f, 0);
                mPos += pivot - rollRot * pivot - Vector3.up * 0.35f * tuck;
                if (!rolling) rollT = -1f;
            }

            // ---- death
            if (dead)
            {
                deadT += dt;
                float w = Smooth(deadT / 0.7f);
                mRot.x = -86f * w;
                mPos.y += 0.13f * w;
                Blend(J.ShL, new Vector3(-20, 0, -55), w); Blend(J.ShR, new Vector3(-20, 0, 55), w);
                Blend(J.ElL, new Vector3(-20, 0, 0), w); Blend(J.ElR, new Vector3(-20, 0, 0), w);
                Blend(J.HipL, new Vector3(-8, 0, -8), w); Blend(J.HipR, new Vector3(-4, 0, 10), w);
                Blend(J.KnL, new Vector3(15, 0, 0), w); Blend(J.KnR, new Vector3(5, 0, 0), w);
            }
            else deadT = 0f;

            // ---- how much of the procedural pose overrides the mocap pose (mecanim mode)
            float fullT = Mathf.Max(Mathf.Max(seatW, swimW), Mathf.Max(airW, dead ? Smooth(deadT / 0.3f) : 0f));
            if (rolling) fullT = 1f;
            float upperT = Mathf.Max(Mathf.Max(blockW, poseW), attacking ? 1f : 0f);
            if (hitT >= 0f) upperT = Mathf.Max(upperT, 1f - Smooth(hitT / 0.4f));
            bool fullClips = Full;
            if (fullClips)
            {
                // the clips do the legs, falls, swims, sitting, driving, fights, hits, rolls and deaths: the procedural pose
                // only for the arms (hands up, the stretcher, the guard) and the seated poses on a bike or in a boat
                bool procRide = (riding && !rideAnim) || rowing;   // with the motorbike pack the clips ride too
                fullT = procRide ? seatW : 0f;
                upperT = Mathf.Max(blockW, poseW);
                rolling = false;
                mRot = Vector3.zero;
                mPos = procRide ? new Vector3(0f, mPos.y, 0f) : Vector3.zero;
            }
            fullW = Mathf.MoveTowards(fullW, fullT, dt * 10f);
            upperW = Mathf.MoveTowards(upperW, upperT, dt * 9f);
            kickW = attacking && attackKind == Attack.Kick && !Full ? upperW : Mathf.MoveTowards(kickW, 0f, dt * 9f);

            // ---- apply
            mPos *= motionScale;   // a child's hips sit lower than the code-built body's: scale the drops
            float sharp = attacking ? 32f : 15f;
            float k01 = 1f - Mathf.Exp(-sharp * dt);
            for (int i = 0; i < JC; i++)
                if (joints[i]) joints[i].localRotation = Quaternion.Slerp(joints[i].localRotation, Quaternion.Euler(tgt[i]), k01);   // a bone can be gone while the editor rebuilds the world
            if (skinBones != null) { ApplySkin(); if (fullClips) ApplyAimBend(); }
            if (fullClips && (((seated || driving) && !riding) || RideNow) && !rowing) { SeatLock(); return; }
            if (rolling)
            {
                model.localRotation = rollRot;
                model.localPosition = mPos;
            }
            else
            {
                float km = 1f - Mathf.Exp(-12f * dt);
                model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(mRot), km);
                model.localPosition = Vector3.Lerp(model.localPosition, mPos, km);
            }
        }

        void Blend(J j, Vector3 e, float w) => tgt[(int)j] = Vector3.Lerp(tgt[(int)j], e, w);
    }
}

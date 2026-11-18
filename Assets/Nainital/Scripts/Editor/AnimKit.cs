using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Every character move as a real Mixamo clip (user, 2026-11-18: "legs theek nahi hain... gun shoot, run, walk,
    /// shout, punch, kick and jitne animation use ho rahe hain vo Mixamo se uthaao"). The clips (Mixamo X Bot, without
    /// skin, 30 fps; packs Locomotion, Female Locomotion, Pistol/Handgun Locomotion, Action Adventure, Gestures Basic and
    /// single motions, fetched by the google-flow-automation skill's Mixamo scripts) live in Characters/Anims and play on
    /// every rigged character through its Humanoid avatar. Builds Resources/NainitalCharacter.controller:
    ///   layer 0 Base (full body): Move (idle / walk / jog / run by Speed), MoveF (the women's set), Pistol (aimed pistol
    ///           idle / walk / run), Air, Swim, Sit, Drive, Knocked -> GetUp, Dead. Chosen by the bools.
    ///   layer 1 Action (full body, weight set by HumanoidRig while one plays): Jab, Cross, Hook, Kick, Hit, Roll, Yell,
    ///           Wave, Talk, Nod, Shake, Angry (triggers), speed by ActSpeed.
    ///   layer 2 Upper (arms and chest): Shoot.
    /// Bridge / batch: call AnimKit.Build
    /// </summary>
    public static class AnimKit
    {
        public const string Dir = "Assets/Nainital/Characters/Anims";
        public const string ControllerPath = "Assets/Resources/NainitalCharacter.controller";

        static readonly HashSet<string> Loops = new HashSet<string>
        {
            "m_idle", "m_walking", "m_running", "f_idle", "f_walking", "f_running", "jogging", "pistol_idle", "pistol_walk",
            "pistol_run", "pistol_walk_backward", "pistol_strafe", "pistol_strafe_2", "falling_idle", "treading_water",
            "swimming", "driving", "sitting_idle", "talking", "gesture_weight_shift", "block",
        };

        public static bool IsLoop(string file) => Loops.Contains(Path.GetFileNameWithoutExtension(file));

        static AnimationClip Clip(string name)
        {
            var c = AssetDatabase.LoadAllAssetsAtPath($"{Dir}/{name}.fbx").OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
            if (!c) Debug.LogWarning("[Nainital] anim kit: no clip " + name);
            return c;
        }

        /// <summary>Which leg is forward at the clip's start: + left, - right (the humanoid "Upper Leg Front-Back" muscles).
        /// Blending a walk that starts on one foot with a run that starts on the other folds both legs together.</summary>
        public static float LeadLeg(AnimationClip c)
        {
            if (!c) return 0f;
            float l = 0f, r = 0f;
            foreach (var b in AnimationUtility.GetCurveBindings(c))
            {
                if (b.propertyName == "Left Upper Leg Front-Back") l = AnimationUtility.GetEditorCurve(c, b).Evaluate(0f);
                if (b.propertyName == "Right Upper Leg Front-Back") r = AnimationUtility.GetEditorCurve(c, b).Evaluate(0f);
            }
            return l - r;
        }

        /// <summary>How far each leg swings in a clip (range of the "Upper Leg Front-Back" muscle): a run whose legs barely
        /// move shows up here.</summary>
        public static void SwingReport()
        {
            var sb = new System.Text.StringBuilder("[Nainital] leg swing (muscle range L / R), length:");
            foreach (var path in Directory.GetFiles(Dir, "*.fbx"))
            {
                var c = Clip(Path.GetFileNameWithoutExtension(path));
                if (!c) continue;
                float lo = 9, hi = -9, lo2 = 9, hi2 = -9;
                foreach (var b in AnimationUtility.GetCurveBindings(c))
                {
                    bool L = b.propertyName == "Left Upper Leg Front-Back", R = b.propertyName == "Right Upper Leg Front-Back";
                    if (!L && !R) continue;
                    var cv = AnimationUtility.GetEditorCurve(c, b);
                    for (float t = 0; t <= c.length; t += c.length / 40f)
                    {
                        float v = cv.Evaluate(t);
                        if (L) { lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); } else { lo2 = Mathf.Min(lo2, v); hi2 = Mathf.Max(hi2, v); }
                    }
                }
                sb.AppendLine().Append($"  {c.name}: {hi - lo:F2} / {hi2 - lo2:F2}, {c.length:F2} s");
            }
            Debug.Log(sb.ToString());
        }

        public static void LeadReport()
        {
            var sb = new System.Text.StringBuilder("[Nainital] lead leg at t=0 (+ left forward):");
            foreach (var n in new[] { "m_walking", "jogging", "m_running", "f_walking", "f_running", "pistol_walk", "pistol_run", "swimming" })
                sb.Append($" {n} {LeadLeg(Clip(n)):F2}");
            Debug.Log(sb.ToString());
        }

        [MenuItem("Nainital/6b. Build character animator (Mixamo clips)", priority = 6)]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory("Assets/Resources");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            void P(string n, AnimatorControllerParameterType t, float def = 0f)
            {
                ctrl.AddParameter(new AnimatorControllerParameter { name = n, type = t, defaultFloat = def, defaultBool = def > 0f });
            }
            P("Speed", AnimatorControllerParameterType.Float);
            P("ActSpeed", AnimatorControllerParameterType.Float, 1f);
            P("Grounded", AnimatorControllerParameterType.Bool, 1f);
            foreach (var b in new[] { "Dead", "Knocked", "Rising", "Seated", "Driving", "Swim", "Aim", "Female" }) P(b, AnimatorControllerParameterType.Bool);
            foreach (var t in new[] { "Jab", "Cross", "Hook", "Kick", "Hit", "Roll", "Yell", "Wave", "Talk", "Nod", "Shake", "Angry", "Shoot" })
                P(t, AnimatorControllerParameterType.Trigger);

            // ---- layer 0: where the body is and how it moves
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState Blend(string name, params (string clip, float at)[] parts)
            {
                var st = ctrl.CreateBlendTreeInController(name, out var tree, 0);
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = "Speed";
                tree.useAutomaticThresholds = false;
                foreach (var (clip, at) in parts) { var c = Clip(clip); if (c) tree.AddChild(c, at); }
                return st;
            }
            AnimatorState Single(AnimatorStateMachine m, string name, string clip, bool speedParam = false)
            {
                var st = m.AddState(name);
                st.motion = Clip(clip);
                if (speedParam) { st.speedParameterActive = true; st.speedParameter = "ActSpeed"; }
                return st;
            }
            // one-cycle loops only: the Jogging clip is 2.6 s of several strides, and blended with the 0.7 s run cycle
            // it froze the legs (user, 2026-11-18: "bahut kharab bhaag raha hai"). The run is pure at the player's run
            // speed (4.4 m/s); faster plays it faster (HumanoidRig.DriveAnimator).
            var move = Blend("Move", ("m_idle", 0f), ("m_walking", 1.5f), ("m_running", 4.4f));
            var moveF = Blend("MoveF", ("f_idle", 0f), ("f_walking", 1.4f), ("f_running", 4.4f));
            var pistol = Blend("Pistol", ("pistol_idle", 0f), ("pistol_walk", 1.4f), ("pistol_run", 4.5f));
            var swim = Blend("Swim", ("treading_water", 0f), ("swimming", 1.2f));
            var air = Single(sm, "Air", "falling_idle");
            var sit = Single(sm, "Sit", "sitting_idle");
            var drive = Single(sm, "Drive", "driving");
            var knocked = Single(sm, "Knocked", "knocked_down");
            var getUp = Single(sm, "GetUp", "getting_up");
            var dead = Single(sm, "Dead", "death_back");
            sm.defaultState = move;

            void Any(AnimatorState to, float blend, params (string p, bool v)[] when)
            {
                var t = sm.AddAnyStateTransition(to);
                t.canTransitionToSelf = false;
                t.hasExitTime = false;
                t.duration = blend;
                foreach (var (p, v) in when) t.AddCondition(v ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, p);
            }
            // most important first (Any State transitions are tried in order)
            Any(dead, 0.15f, ("Dead", true));
            Any(knocked, 0.1f, ("Knocked", true), ("Dead", false));
            Any(getUp, 0.15f, ("Rising", true), ("Knocked", false), ("Dead", false));   // HumanoidRig holds Rising while it plays
            Any(drive, 0.2f, ("Driving", true), ("Dead", false), ("Knocked", false));
            Any(sit, 0.25f, ("Seated", true), ("Driving", false), ("Dead", false), ("Knocked", false));
            Any(swim, 0.3f, ("Swim", true), ("Seated", false), ("Driving", false), ("Dead", false), ("Knocked", false));
            Any(air, 0.15f, ("Grounded", false), ("Swim", false), ("Seated", false), ("Driving", false), ("Dead", false), ("Knocked", false), ("Rising", false));
            Any(pistol, 0.2f, ("Aim", true), ("Grounded", true), ("Swim", false), ("Seated", false), ("Driving", false), ("Dead", false), ("Knocked", false), ("Rising", false));
            Any(moveF, 0.2f, ("Female", true), ("Aim", false), ("Grounded", true), ("Swim", false), ("Seated", false), ("Driving", false), ("Dead", false), ("Knocked", false), ("Rising", false));
            Any(move, 0.2f, ("Female", false), ("Aim", false), ("Grounded", true), ("Swim", false), ("Seated", false), ("Driving", false), ("Dead", false), ("Knocked", false), ("Rising", false));
            // ---- layer 1: actions over everything (HumanoidRig fades this layer in while one plays)
            var actSm = new AnimatorStateMachine { name = "Action", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(actSm, ctrl);
            ctrl.AddLayer(new AnimatorControllerLayer { name = "Action", stateMachine = actSm, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override });
            var none = actSm.AddState("None");
            actSm.defaultState = none;
            foreach (var (trig, clip) in new[] { ("Jab", "punch_jab"), ("Cross", "punch_cross"), ("Hook", "punch_hook"), ("Kick", "kick_roundhouse"),
                                                 ("Hit", "hit_reaction"), ("Roll", "roll"), ("Yell", "yelling"), ("Wave", "waving"), ("Talk", "talking"),
                                                 ("Nod", "gesture_head_nod_yes"), ("Shake", "gesture_shaking_head_no"), ("Angry", "gesture_angry_gesture") })
            {
                var st = Single(actSm, trig, clip, true);
                var t = actSm.AddAnyStateTransition(st);
                t.hasExitTime = false;
                t.duration = 0.06f;
                t.canTransitionToSelf = true;
                t.AddCondition(AnimatorConditionMode.If, 0f, trig);
                var back = st.AddTransition(none);
                back.hasExitTime = true;
                back.exitTime = 0.95f;
                back.duration = 0.1f;
            }

            // ---- layer 2: the pistol's shot over the arms and chest
            var mask = new AvatarMask { name = "UpperBody" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.LeftArm ||
                             part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            AssetDatabase.AddObjectToAsset(mask, ctrl);
            var upSm = new AnimatorStateMachine { name = "Upper", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(upSm, ctrl);
            ctrl.AddLayer(new AnimatorControllerLayer { name = "Upper", stateMachine = upSm, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override, avatarMask = mask });
            var unone = upSm.AddState("None");
            upSm.defaultState = unone;
            var shoot = Single(upSm, "Shoot", "pistol_shoot", true);
            var ts = upSm.AddAnyStateTransition(shoot);
            ts.hasExitTime = false;
            ts.duration = 0.04f;
            ts.canTransitionToSelf = true;
            ts.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
            var tb = shoot.AddTransition(unone);
            tb.hasExitTime = true;
            tb.exitTime = 0.9f;
            tb.duration = 0.1f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            int clips = ctrl.animationClips.Distinct().Count();
            Debug.Log($"[Nainital] character animator: {clips} Mixamo clips -> {ControllerPath}");
        }
    }

    /// <summary>Import settings for the Mixamo clips: Humanoid (their own X Bot avatar), loops where they loop, the root
    /// kept in place (rotation, height and ground position baked into the pose: the game moves the character).</summary>
    class AnimClipImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(AnimKit.Dir + "/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = mi.importLights = false;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimKit.Dir + "/")) return;
            var mi = (ModelImporter)assetImporter;
            bool loop = AnimKit.IsLoop(assetPath);
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = loop;
                c.loopPose = false;
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                c.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            }
            mi.clipAnimations = clips;
        }
    }
}

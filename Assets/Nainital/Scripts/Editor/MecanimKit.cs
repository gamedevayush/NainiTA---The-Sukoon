using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Builds Resources/NainitalLocomotion: one blend tree (Speed m/s) over the G.O.A.N. Mixamo clips
    /// Idle (0) - Walking (1.5) - Fast Run (4.4). HumanoidRig drives Speed and the playback rate, and
    /// overlays its procedural poses for sitting, swimming, rowing, fighting, falling and jumping.
    /// </summary>
    public static class MecanimKit
    {
        public const string ControllerPath = "Assets/Resources/NainitalLocomotion.controller";
        const string Anims = "Assets/GOAN/Animations/NewAnims/";

        [MenuItem("Nainital/6. Build locomotion animator (G.O.A.N. clips)", priority = 6)]
        public static void Build()
        {
            var idle = Clip(Anims + "Idle (3).fbx");
            var walk = Clip(Anims + "Walking.fbx");
            var run = Clip(Anims + "Fast Run.fbx");
            if (!idle || !walk || !run)
            {
                Debug.LogWarning("[Nainital] locomotion: G.O.A.N. clips missing in " + Anims + " - characters stay fully procedural");
                return;
            }
            System.IO.Directory.CreateDirectory("Assets/Resources");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var state = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1.5f);
            tree.AddChild(run, 4.4f);
            var sm = ctrl.layers[0].stateMachine;
            sm.defaultState = state;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nainital] locomotion animator: {idle.name} / {walk.name} / {run.name} -> {ControllerPath}");
        }

        static AnimationClip Clip(string fbx) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }
}

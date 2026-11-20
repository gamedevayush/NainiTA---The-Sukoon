using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// How each seated clip holds the body: the humanoid muscles of the legs and arms at the clip's middle (thigh forward,
    /// knee stretch, spine bend, arm down / forward), so a car seat pose (thighs forward, knees half open, feet ahead) can
    /// be told from a chair pose (knees at 90, shins down) without opening the clips. Bridge: call ClipPoseReport.Run
    /// (arg.txt: clip names, comma separated; empty = the sitting / driving ones).
    /// </summary>
    public static class ClipPoseReport
    {
        static readonly string[] Muscles =
        {
            "Left Upper Leg Front-Back", "Left Upper Leg In-Out", "Left Lower Leg Stretch", "Left Foot Up-Down",
            "Spine Front-Back", "Chest Front-Back", "Left Arm Down-Up", "Left Arm Front-Back", "Left Forearm Stretch",
        };

        /// <summary>Every character in the library: its height, arm (shoulder joint to wrist) and leg lengths from its
        /// Humanoid bones - riders with short arms hunch to reach the grips. Bridge: call ClipPoseReport.Arms</summary>
        public static void Arms()
        {
            var lib = CharacterLibrary.Instance;
            if (!lib) throw new System.Exception("no CharacterLibrary");
            var sb = new System.Text.StringBuilder("[Nainital] character limbs (height / arm / leg, m):");
            foreach (var e in lib.entries)
            {
                if (e == null || !e.prefab) continue;
                var g = (GameObject)Object.Instantiate(e.prefab);
                var a = g.GetComponentInChildren<Animator>(true);
                if (a && a.isHuman)
                {
                    float D(HumanBodyBones x, HumanBodyBones y) { var p = a.GetBoneTransform(x); var q = a.GetBoneTransform(y); return p && q ? Vector3.Distance(p.position, q.position) : -1f; }
                    float arm = D(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm) + D(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
                    float leg = D(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg) + D(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
                    var head = a.GetBoneTransform(HumanBodyBones.Head);
                    var foot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
                    float h = head && foot ? head.position.y - foot.position.y : -1f;
                    var ua = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    sb.AppendLine().Append($"  {e.prefab.name} ({e.bodyType}): head-foot {h:F2}, arm {arm:F2}, leg {leg:F2}, upperArm bone '{(ua ? ua.name : "-")}'");
                    // the skin's reach along the arm in the bind pose: the vertex farthest out past the hand bone along the
                    // forearm's line (the fingertips) - a hand bone sitting mid-forearm shows as a long "past hand"
                    var lo = a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    var hd = a.GetBoneTransform(HumanBodyBones.LeftHand);
                    if (ua && lo && hd)
                    {
                        Vector3 dir = (hd.position - lo.position).normalized;
                        float past = 0f;
                        foreach (var sk in g.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            var m = new Mesh();
                            sk.BakeMesh(m, true);
                            foreach (var vtx in m.vertices)
                            {
                                Vector3 w = sk.transform.TransformPoint(vtx);
                                Vector3 r = w - hd.position;
                                float along = Vector3.Dot(r, dir);
                                if (along > past && (r - dir * along).magnitude < 0.12f) past = along;
                            }
                            Object.DestroyImmediate(m);
                        }
                        sb.Append($", skin past the hand bone {past:F2} (a real hand: ~0.18), shoulder-elbow {Vector3.Distance(ua.position, lo.position):F2}, elbow-hand {Vector3.Distance(lo.position, hd.position):F2}");
                    }
                }
                Object.DestroyImmediate(g);
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>What the character animator asset holds (parameters, layers, states, clips), as the editor and as
        /// Resources.Load see it. Bridge: call ClipPoseReport.Controller</summary>
        public static void Controller()
        {
            var c = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(AnimKit.ControllerPath);
            var sb = new System.Text.StringBuilder("[Nainital] controller: ");
            if (!c) sb.Append("NOT LOADED as AnimatorController");
            else
            {
                sb.Append($"params {c.parameters.Length}, layers {c.layers.Length} [{string.Join(", ", c.layers.Select(l => l.name + ":" + (l.stateMachine ? l.stateMachine.states.Length : -1)))}], clips {c.animationClips.Length} (null {c.animationClips.Count(x => !x)})");
                var miss = c.layers.Where(l => l.stateMachine).SelectMany(l => l.stateMachine.states).Where(s => s.state && !s.state.motion).Select(s => s.state.name).ToArray();
                if (miss.Length > 0) sb.Append(" | states with no motion: " + string.Join(",", miss));
            }
            var r = Resources.Load<RuntimeAnimatorController>("NainitalCharacter");
            sb.Append($" | Resources.Load: {(r ? r.GetType().Name + " clips " + r.animationClips.Length : "NULL")}");
            Debug.Log(sb.ToString());
        }

        public static void Run()
        {
            string a = "";
            try { a = File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { }
            var names = a.Length > 0 && a.ToLowerInvariant() != "all" ? a.Split(',').Select(s => s.Trim()).ToArray()
                : Directory.GetFiles(AnimKit.Dir, "*.fbx").Select(Path.GetFileNameWithoutExtension)
                    .Where(n => n.Contains("sit") || n.Contains("driv") || n.Contains("car_") || n.Contains("ride") || n.Contains("moto") || n.Contains("bike")).ToArray();
            var sb = new System.Text.StringBuilder("[Nainital] clip poses (muscle values at mid clip; thigh F-B +1 = thigh up/forward, knee stretch -1 = bent):");
            sb.AppendLine().Append("  clip".PadRight(30) + string.Join(" ", Muscles.Select(m => m.Replace("Left ", "").Replace(" ", "").Substring(0, 10).PadLeft(10))));
            foreach (var n in names)
            {
                foreach (var path in Directory.GetFiles(Path.GetDirectoryName(Application.dataPath) + "/Assets", n + ".fbx", SearchOption.AllDirectories))
                {
                    string ap = "Assets" + path.Substring(Application.dataPath.Length).Replace('\\', '/');
                    foreach (var c in AssetDatabase.LoadAllAssetsAtPath(ap).OfType<AnimationClip>().Where(x => !x.name.StartsWith("__preview__")))
                    {
                        var binds = AnimationUtility.GetCurveBindings(c);
                        sb.AppendLine().Append(("  " + c.name + $" ({c.length:F1}s)").PadRight(30));
                        foreach (var m in Muscles)
                        {
                            var b = binds.FirstOrDefault(x => x.propertyName == m);
                            var cv = b.propertyName == m ? AnimationUtility.GetEditorCurve(c, b) : null;
                            sb.Append((cv != null ? cv.Evaluate(c.length * 0.5f).ToString("F2") : "-").PadLeft(11));
                        }
                    }
                }
            }
            Debug.Log(sb.ToString());
        }
    }
}

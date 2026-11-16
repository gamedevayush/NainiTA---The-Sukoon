using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Characters for the Unreal port: one skinned .glb per CharacterLibrary person with the NainitalLocomotion mocap
    /// (idle / walk / run) baked on it. The clips are sampled from Unity's own Humanoid retargeting, so Unreal gets
    /// exactly the motion Unity shows on that person, in one unit system (the source FBX files disagree: Blender rigs
    /// with a 100x root, Mixamo meshes and clips in other units; Unreal's IK retarget shrank the people to 1.7 cm).
    /// Same axis rule as the static meshes: position (x, y, -z), rotation (-x, -y, z, w), reversed winding.
    /// </summary>
    public static partial class UnrealExport
    {
        const float CharFps = 30f;

        static void ExportCharacters(J man)
        {
            var lib = Resources.Load<CharacterLibrary>("CharacterLibrary");

            // the blend tree's clips: (role, clip, threshold in m/s)
            var clips = new List<(string role, AnimationClip clip, float threshold)>();
            var ctrl = Resources.Load<RuntimeAnimatorController>("NainitalLocomotion") as AnimatorController;
            if (ctrl)
                foreach (var layer in ctrl.layers)
                    foreach (var st in layer.stateMachine.states)
                    {
                        void AddClip(Motion mo, float th)
                        {
                            if (mo is AnimationClip c) clips.Add((th < 0.5f ? "Idle" : th < 3f ? "Walk" : "Run", c, th));
                        }
                        if (st.state.motion is BlendTree bt) foreach (var ch in bt.children) AddClip(ch.motion, ch.threshold);
                        else AddClip(st.state.motion, 0f);
                    }

            var people = J.Arr();
            int files = 0;
            if (lib && lib.entries != null)
                foreach (var e in lib.entries)
                {
                    if (e == null || !e.prefab) continue;
                    string file = "Characters/" + Safe(e.id) + ".glb";
                    try
                    {
                        var info = WriteCharacterGlb(e.prefab, Safe(e.id), Path.Combine(outDir, file), clips);
                        people.Add(J.Obj().Set("id", e.id).Set("name", e.displayName).Set("bodyType", e.bodyType.ToString())
                            .Set("height", e.height).Set("weight", e.weight).Set("female", e.Female).Set("child", e.Child).Set("elder", e.Elder)
                            .Set("glb", file).Set("materials", info.materials).Set("clips", info.clips)
                            .Set("bones", info.bones).Set("verts", info.verts).Set("humanoid", info.humanoid));
                        files++;
                    }
                    catch (Exception ex) { Debug.LogWarning($"[UnrealExport] character {e.id}: {ex.Message}\n{ex.StackTrace}"); }
                }

            var cj = J.Arr();
            foreach (var c in clips) cj.Add(J.Obj().Set("role", c.role).Set("clip", c.clip.name).Set("threshold", c.threshold).Set("length", c.clip.length));
            man["characters"] = J.Obj().Set("people", people).Set("locomotion", cj).Set("fps", CharFps)
                .Set("walkRef", 1.5f).Set("runRef", 4.4f);   // HumanoidRig.WalkRef / RunRef: speeds of the Walking / Fast Run clips
            Debug.Log($"[UnrealExport] characters: {files} people with {clips.Count} baked clips each");
        }

        struct CharInfo { public J materials, clips; public int bones, verts; public bool humanoid; }

        static CharInfo WriteCharacterGlb(GameObject prefab, string name, string path, List<(string role, AnimationClip clip, float threshold)> clips)
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                // ---- nodes: the whole hierarchy, parents first
                var nodes = new List<Transform>();
                void Walk(Transform t) { nodes.Add(t); for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i)); }
                Walk(go.transform);
                var index = new Dictionary<Transform, int>();
                for (int i = 0; i < nodes.Count; i++) index[nodes[i]] = i;
                // the full-detail body only (Body_Low is LOD 1 of a LODGroup: with both, the person would be drawn twice)
                var lower = new HashSet<Renderer>();
                foreach (var lg in go.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = lg.GetLODs();
                    for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r) lower.Add(r);
                }
                var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(s => s.sharedMesh && s.sharedMesh.boneWeights.Length > 0 && !lower.Contains(s)).ToArray();
                if (smrs.Length == 0) throw new Exception("no skinned mesh");

                var bin = new MemoryStream();
                var bw = new BinaryWriter(bin);
                var views = J.Arr(); var accessors = J.Arr();
                int View(long start, long len, int target)
                {
                    var v = J.Obj().Set("buffer", 0).Set("byteOffset", start).Set("byteLength", len);
                    if (target != 0) v["target"] = target;
                    views.Add(v);
                    return views.Count - 1;
                }
                void Pad() { while (bin.Length % 4 != 0) bw.Write((byte)0); }
                int Acc(long start, int target, int componentType, int count, string type, J min = null, J max = null)
                {
                    var a = J.Obj().Set("bufferView", View(start, bin.Length - start, target)).Set("componentType", componentType).Set("count", count).Set("type", type);
                    if (min != null) { a["min"] = min; a["max"] = max; }
                    accessors.Add(a);
                    return accessors.Count - 1;
                }

                // ---- node TRS in the rest pose (the prefab as it is)
                J NodeJson(Transform t)
                {
                    var p = t.localPosition; var r = t.localRotation; var s = t.localScale;
                    var j = J.Obj().Set("name", t == go.transform ? name : t.name)
                        .Set("translation", J.Arr().Add(p.x).Add(p.y).Add(-p.z))
                        .Set("rotation", J.Arr().Add(-r.x).Add(-r.y).Add(r.z).Add(r.w))
                        .Set("scale", J.Arr().Add(s.x).Add(s.y).Add(s.z));
                    if (t.childCount > 0)
                    {
                        var ch = J.Arr();
                        for (int i = 0; i < t.childCount; i++) ch.Add(index[t.GetChild(i)]);
                        j["children"] = ch;
                    }
                    return j;
                }
                var jnodes = J.Arr();
                foreach (var t in nodes) jnodes.Add(NodeJson(t));

                // ---- meshes + skins
                var jmeshes = J.Arr(); var jskins = J.Arr(); var jmats = J.Arr();
                var matIdsOrdered = J.Arr();
                var sceneRoots = J.Arr().Add(0);
                int totalVerts = 0;
                var jointSet = new HashSet<int>();
                foreach (var smr in smrs)
                {
                    var mesh = smr.sharedMesh;
                    var vs = mesh.vertices; var ns = mesh.normals; var uvs = mesh.uv; var bws = mesh.boneWeights;
                    int n = vs.Length;
                    totalVerts += n;
                    if (ns.Length != n) { mesh.RecalculateNormals(); ns = mesh.normals; }

                    Pad(); long s0 = bin.Length;
                    Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn;
                    for (int i = 0; i < n; i++)
                    {
                        var g = new Vector3(vs[i].x, vs[i].y, -vs[i].z);
                        mn = Vector3.Min(mn, g); mx = Vector3.Max(mx, g);
                        bw.Write(g.x); bw.Write(g.y); bw.Write(g.z);
                    }
                    int aPos = Acc(s0, 34962, 5126, n, "VEC3", J.Arr().Add(mn.x).Add(mn.y).Add(mn.z), J.Arr().Add(mx.x).Add(mx.y).Add(mx.z));
                    s0 = bin.Length;
                    for (int i = 0; i < n; i++) { bw.Write(ns[i].x); bw.Write(ns[i].y); bw.Write(-ns[i].z); }
                    int aNrm = Acc(s0, 34962, 5126, n, "VEC3");
                    s0 = bin.Length;
                    for (int i = 0; i < n; i++) { var uv = i < uvs.Length ? uvs[i] : Vector2.zero; bw.Write(uv.x); bw.Write(1f - uv.y); }
                    int aUv = Acc(s0, 34962, 5126, n, "VEC2");
                    s0 = bin.Length;
                    for (int i = 0; i < n; i++)
                    {
                        var b = bws[i];
                        bw.Write((ushort)b.boneIndex0); bw.Write((ushort)b.boneIndex1); bw.Write((ushort)b.boneIndex2); bw.Write((ushort)b.boneIndex3);
                    }
                    int aJnt = Acc(s0, 34962, 5123, n, "VEC4");
                    s0 = bin.Length;
                    for (int i = 0; i < n; i++)
                    {
                        var b = bws[i];
                        float sum = b.weight0 + b.weight1 + b.weight2 + b.weight3;
                        if (sum < 1e-6f) { bw.Write(1f); bw.Write(0f); bw.Write(0f); bw.Write(0f); continue; }
                        bw.Write(b.weight0 / sum); bw.Write(b.weight1 / sum); bw.Write(b.weight2 / sum); bw.Write(b.weight3 / sum);
                    }
                    int aWgt = Acc(s0, 34962, 5126, n, "VEC4");

                    var prims = J.Arr();
                    var mats = smr.sharedMaterials;
                    for (int sm = 0; sm < mesh.subMeshCount; sm++)
                    {
                        var tri = mesh.GetTriangles(sm);
                        Pad(); s0 = bin.Length;
                        for (int i = 0; i + 2 < tri.Length; i += 3) { bw.Write((uint)tri[i]); bw.Write((uint)tri[i + 2]); bw.Write((uint)tri[i + 1]); }
                        int aIdx = Acc(s0, 34963, 5125, tri.Length, "SCALAR");
                        var m = mats.Length > 0 ? mats[Mathf.Min(sm, mats.Length - 1)] : null;
                        string mid = Mat(m) ?? "M_None";
                        matIdsOrdered.Add(mid);
                        jmats.Add(J.Obj().Set("name", mid).Set("pbrMetallicRoughness", J.Obj().Set("metallicFactor", 0f).Set("roughnessFactor", 0.8f)));
                        prims.Add(J.Obj().Set("attributes", J.Obj().Set("POSITION", aPos).Set("NORMAL", aNrm).Set("TEXCOORD_0", aUv).Set("JOINTS_0", aJnt).Set("WEIGHTS_0", aWgt))
                            .Set("indices", aIdx).Set("material", jmats.Count - 1).Set("mode", 4));
                    }
                    jmeshes.Add(J.Obj().Set("name", name + (smrs.Length > 1 ? "_" + jmeshes.Count : "")).Set("primitives", prims));

                    // skin: joints = this renderer's bones, inverse bind = mesh.bindposes mirrored on z (S * M * S)
                    var bones = smr.bones; var binds = mesh.bindposes;
                    var joints = J.Arr();
                    Pad(); s0 = bin.Length;
                    for (int i = 0; i < bones.Length; i++)
                    {
                        int ni = bones[i] && index.TryGetValue(bones[i], out var k) ? k : 0;
                        joints.Add(ni);
                        jointSet.Add(ni);
                        var m = i < binds.Length ? binds[i] : Matrix4x4.identity;
                        for (int c = 0; c < 4; c++)
                            for (int r = 0; r < 4; r++)
                                bw.Write(m[r, c] * (r == 2 ? -1f : 1f) * (c == 2 ? -1f : 1f));
                    }
                    int aIbm = Acc(s0, 0, 5126, bones.Length, "MAT4");
                    jskins.Add(J.Obj().Set("joints", joints).Set("inverseBindMatrices", aIbm).Set("skeleton", 0));

                    // the skinned mesh hangs on its own root node (glTF ignores a skinned node's transform)
                    jnodes.Add(J.Obj().Set("name", name + "_mesh" + (jmeshes.Count - 1)).Set("mesh", jmeshes.Count - 1).Set("skin", jskins.Count - 1));
                    sceneRoots.Add(jnodes.Count - 1);
                }

                // ---- animations: Unity's Humanoid result on this very rig, in place
                var janims = J.Arr(); var clipNames = J.Obj();
                var animator = go.GetComponentInChildren<Animator>(true);
                bool humanoid = animator && animator.avatar && animator.avatar.isHuman;
                if (animator && clips.Count > 0)
                {
                    var restPos = nodes.Select(t => t.localPosition).ToArray();
                    var restRot = nodes.Select(t => t.localRotation).ToArray();
                    var rootT = animator.transform;
                    Vector3 rootPos = rootT.localPosition; Quaternion rootRot = rootT.localRotation;
                    AnimationMode.StartAnimationMode();
                    try
                    {
                        foreach (var (role, clip, threshold) in clips)
                        {
                            int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * CharFps) + 1);
                            var pos = new Vector3[nodes.Count][]; var rot = new Quaternion[nodes.Count][];
                            for (int k = 0; k < nodes.Count; k++) { pos[k] = new Vector3[frames]; rot[k] = new Quaternion[frames]; }
                            for (int f = 0; f < frames; f++)
                            {
                                float t = Mathf.Min(f / CharFps, clip.length);
                                AnimationMode.BeginSampling();
                                AnimationMode.SampleAnimationClip(animator.gameObject, clip, t);
                                AnimationMode.EndSampling();
                                rootT.localPosition = rootPos; rootT.localRotation = rootRot;   // in place: the game moves the character
                                for (int k = 0; k < nodes.Count; k++) { pos[k][f] = nodes[k].localPosition; rot[k][f] = nodes[k].localRotation; }
                            }
                            Pad(); long s0 = bin.Length;
                            for (int f = 0; f < frames; f++) bw.Write(Mathf.Min(f / CharFps, clip.length));
                            int aTime = Acc(s0, 0, 5126, frames, "SCALAR", J.Arr().Add(0f), J.Arr().Add(Mathf.Min((frames - 1) / CharFps, clip.length)));
                            var samplers = J.Arr(); var channels = J.Arr();
                            for (int k = 1; k < nodes.Count; k++)   // node 0 = the character root, never animated
                            {
                                if (!jointSet.Contains(k) && !IsAncestorOfJoint(nodes, k, jointSet, index)) continue;
                                bool rotMoves = false, posMoves = false;
                                for (int f = 0; f < frames; f++)
                                {
                                    if (Quaternion.Angle(rot[k][f], restRot[k]) > 0.02f) rotMoves = true;
                                    if ((pos[k][f] - restPos[k]).sqrMagnitude > 1e-9f) posMoves = true;
                                }
                                if (rotMoves)
                                {
                                    s0 = bin.Length;
                                    Quaternion prev = rot[k][0];
                                    for (int f = 0; f < frames; f++)
                                    {
                                        var q = rot[k][f];
                                        if (Quaternion.Dot(q, prev) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);   // shortest path for the importer
                                        prev = q;
                                        bw.Write(-q.x); bw.Write(-q.y); bw.Write(q.z); bw.Write(q.w);
                                    }
                                    samplers.Add(J.Obj().Set("input", aTime).Set("output", Acc(s0, 0, 5126, frames, "VEC4")).Set("interpolation", "LINEAR"));
                                    channels.Add(J.Obj().Set("sampler", samplers.Count - 1).Set("target", J.Obj().Set("node", k).Set("path", "rotation")));
                                }
                                if (posMoves)
                                {
                                    s0 = bin.Length;
                                    for (int f = 0; f < frames; f++) { var p = pos[k][f]; bw.Write(p.x); bw.Write(p.y); bw.Write(-p.z); }
                                    samplers.Add(J.Obj().Set("input", aTime).Set("output", Acc(s0, 0, 5126, frames, "VEC3")).Set("interpolation", "LINEAR"));
                                    channels.Add(J.Obj().Set("sampler", samplers.Count - 1).Set("target", J.Obj().Set("node", k).Set("path", "translation")));
                                }
                            }
                            if (channels.Count > 0)
                            {
                                janims.Add(J.Obj().Set("name", role).Set("samplers", samplers).Set("channels", channels));
                                clipNames[role.ToLowerInvariant()] = role;
                            }
                        }
                    }
                    finally
                    {
                        AnimationMode.StopAnimationMode();
                    }
                }
                Pad();

                var root = J.Obj();
                root["asset"] = J.Obj().Set("version", "2.0").Set("generator", "NainiTA UnrealExport (characters)");
                root["scene"] = 0;
                root["scenes"] = J.Arr().Add(J.Obj().Set("nodes", sceneRoots));
                root["nodes"] = jnodes;
                root["meshes"] = jmeshes;
                root["skins"] = jskins;
                root["materials"] = jmats;
                if (janims.Count > 0) root["animations"] = janims;
                root["buffers"] = J.Arr().Add(J.Obj().Set("byteLength", bin.Length));
                root["bufferViews"] = views;
                root["accessors"] = accessors;

                var json = Encoding.UTF8.GetBytes(root.ToString());
                int jsonPad = (4 - json.Length % 4) % 4;
                using (var f = new BinaryWriter(File.Create(path)))
                {
                    uint total = (uint)(12 + 8 + json.Length + jsonPad + 8 + bin.Length);
                    f.Write(0x46546C67u); f.Write(2u); f.Write(total);
                    f.Write((uint)(json.Length + jsonPad)); f.Write(0x4E4F534Au); f.Write(json); for (int i = 0; i < jsonPad; i++) f.Write((byte)0x20);
                    f.Write((uint)bin.Length); f.Write(0x004E4942u); bin.Position = 0; bin.CopyTo(f.BaseStream);
                }
                return new CharInfo { materials = matIdsOrdered, clips = clipNames, bones = jointSet.Count, verts = totalVerts, humanoid = humanoid };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>True when node k is above a skin joint (its motion moves the skeleton).</summary>
        static bool IsAncestorOfJoint(List<Transform> nodes, int k, HashSet<int> joints, Dictionary<Transform, int> index)
        {
            foreach (int j in joints)
            {
                for (var t = nodes[j].parent; t; t = t.parent)
                    if (index.TryGetValue(t, out var i) && i == k) return true;
            }
            return false;
        }
    }
}

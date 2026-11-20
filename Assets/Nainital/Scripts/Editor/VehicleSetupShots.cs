using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// A check sheet of the vehicle that is on show in the Vehicle Setup tool (Play mode, scene VehicleSetup): six views in
    /// one image, Shots/vsetup_&lt;model&gt;.png - the left side, cut views through the driver's / rider's seat and the
    /// passengers' seats (the body in front of the cut is clipped away, so the people inside show), the front three-quarter,
    /// and a top view with the roof cut off. To see how the people sit (hips on the seat, feet on the floor / pegs, hands
    /// on the wheel / grips) without opening the car. Bridge: call VehicleSetupShots.Run (arg.txt: a model name to show
    /// first, or empty for the one on show; "all" = every model in Vehicles/New, one sheet each).
    /// </summary>
    public static class VehicleSetupShots
    {
        const int W = 640, H = 480;

        static string Arg() { try { return File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { return ""; } }

        /// <summary>Bridge: call VehicleSetupShots.Play - Play in the VehicleSetup scene (the tool puts its vehicle on show),
        /// running on while the editor is behind other windows. call VehicleSetupShots.Stop leaves Play.</summary>
        public static void Play()
        {
            PlayerSettings.runInBackground = true;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        public static void Stop() { if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; }

        /// <summary>Close cut views of chosen seats, one sheet per vehicle: Shots/vseat_&lt;model&gt;.png, up to 6 seats, each seen
        /// square from the left with everything nearer than 0.30 m left of the seat clipped away, 1.6 m tall around the
        /// hips and the feet - the hips on the cushion, the feet on the floor / pegs. Bridge: call VehicleSetupShots.SeatCuts
        /// (arg.txt: "model:0,1,5;model2:3" - no seat list = every seat, the first 6).</summary>
        public static void SeatCuts()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("not playing (press Play in the VehicleSetup scene)");
            var jobs = new Queue<(string model, int[] seats)>();
            foreach (var part in Arg().Split(';'))
            {
                var kv = part.Split(':');
                if (kv[0].Trim().Length == 0) continue;
                int[] list = kv.Length > 1 && kv[1].Trim().Length > 0 ? kv[1].Split(',').Select(x => int.Parse(x.Trim())).ToArray() : null;
                jobs.Enqueue((kv[0].Trim(), list));
            }
            double next = 0;
            bool shown = false;
            (string model, int[] seats) job = default;
            void Tick()
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.timeSinceStartup < next) return;
                if (!shown)
                {
                    if (jobs.Count == 0) { EditorApplication.update -= Tick; Debug.Log("[Nainital] vseat cuts done"); return; }
                    job = jobs.Dequeue();
                    VehicleSetupWindow.Goto(job.model);
                    shown = true;
                    next = EditorApplication.timeSinceStartup + 1.2;
                    return;
                }
                try { SeatCutSheet(job.seats); } catch (System.Exception e) { Debug.LogWarning("[Nainital] vseat cut: " + e.Message); }
                shown = false;
                next = EditorApplication.timeSinceStartup + 0.1;
            }
            EditorApplication.update += Tick;
        }

        static void SeatCutSheet(int[] want)
        {
            var root = GameObject.Find("VEHICLE SETUP");
            if (!root || root.transform.childCount == 0) throw new System.Exception("no vehicle on show");
            var v = root.transform.GetChild(0);
            var seats = v.GetComponentsInChildren<SetupHandle>().Where(h => h.kind == SetupHandle.Kind.Seat).OrderBy(h => h.index)
                .Where(h => want == null || want.Contains(h.index)).Take(6).ToArray();
            // a Tripo shell is one-sided: from inside the cut its floor, seats and walls are culled away. Both faces, on
            // the instance's own material copies (Play mode: gone when it is shown again / Play ends)
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponentInParent<SetupHandle>() || r.gameObject.layer == 9) continue;
                foreach (var m in r.materials)
                {
                    if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                    if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
                    m.doubleSidedGI = true;
                }
            }
            var sheet = new Texture2D(W * 3, H * 2, TextureFormat.RGB24, false);
            var px = sheet.GetPixels();
            for (int k = 0; k < px.Length; k++) px[k] = Color.white;
            sheet.SetPixels(px);
            var info = new System.Text.StringBuilder($"[Nainital] vseat {v.name}:");
            for (int i = 0; i < seats.Length; i++)
            {
                var s = seats[i];
                var a = s.GetComponentInChildren<Animator>();
                var hips = a && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
                Vector3 hp = hips ? hips.position : s.transform.position + Vector3.up * 0.5f;
                // looking at the seat from the left (+x), cut 0.30 m short of it; centred between the hips and the feet
                float cutX = hp.x - 0.30f;
                Vector3 c = new Vector3(cutX - 8f, hp.y - 0.25f, hp.z + s.transform.forward.z * 0.15f);
                var tex = Ortho(c, Vector3.right, 0.8f, 8f, "seat");
                // a ruler on the left edge in the vehicle's height: a tick every 0.1 m, long at every 0.5 m; red at the hips
                float y0 = c.y - 0.8f;
                for (int t = Mathf.CeilToInt(y0 * 10f); t <= Mathf.FloorToInt((y0 + 1.6f) * 10f); t++)
                {
                    int row = Mathf.RoundToInt((t / 10f - y0) / 1.6f * H);
                    int len = t % 5 == 0 ? 40 : 14;
                    for (int x = 0; x < len; x++) for (int d = 0; d < 2; d++) if (row + d >= 0 && row + d < H) tex.SetPixel(x, row + d, Color.black);
                }
                int hrow = Mathf.RoundToInt((v.InverseTransformPoint(hp).y - y0) / 1.6f * H);
                for (int x = 0; x < 60; x++) for (int d = 0; d < 3; d++) if (hrow + d >= 0 && hrow + d < H) tex.SetPixel(x, hrow + d, Color.red);
                tex.Apply();
                int cx = (i % 3) * W, cy = (1 - i / 3) * H;
                sheet.SetPixels(cx, cy, W, H, tex.GetPixels());
                Object.DestroyImmediate(tex);
                var lf = a && a.isHuman ? a.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
                var ll = a && a.isHuman ? a.GetBoneTransform(HumanBodyBones.LeftUpperLeg) : null;
                var lk = a && a.isHuman ? a.GetBoneTransform(HumanBodyBones.LeftLowerLeg) : null;
                float leg = ll && lk && lf ? Vector3.Distance(ll.position, lk.position) + Vector3.Distance(lk.position, lf.position) : -1f;
                info.Append($" | seat {s.index} (tile {i + 1}) hips {v.InverseTransformPoint(hp):F2} leg {leg:F2}");
            }
            sheet.Apply();
            string outP = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/vseat_" + v.name + ".png"));
            File.WriteAllBytes(outP, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log(info.ToString());
        }

        /// <summary>How far the skin reaches below the bones that the setup places, for every person on show, as they sit
        /// now: the hips bone over the lowest skin under it (the seat of the trousers: the hips go this much over a
        /// cushion), the ankle over the sole, and the ankle / hips heights. Bridge: call VehicleSetupShots.RunOffsets
        /// (arg.txt: model names).</summary>
        public static void Offsets()
        {
            var root = GameObject.Find("VEHICLE SETUP");
            if (!root || root.transform.childCount == 0) throw new System.Exception("no vehicle on show");
            var v = root.transform.GetChild(0);
            var sb = new System.Text.StringBuilder($"[Nainital] offsets {v.name}:");
            foreach (var s in v.GetComponentsInChildren<SetupHandle>().Where(h => h.kind == SetupHandle.Kind.Seat).OrderBy(h => h.index))
            {
                var a = s.GetComponentInChildren<Animator>();
                if (!a || !a.isHuman) continue;
                var hips = a.GetBoneTransform(HumanBodyBones.Hips);
                var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
                var lul = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                float butt = float.MaxValue, sole = float.MaxValue, top = float.MinValue;
                foreach (var sk in s.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var m = new Mesh();
                    sk.BakeMesh(m, true);
                    foreach (var vtx in m.vertices)
                    {
                        Vector3 w = sk.transform.TransformPoint(vtx);
                        top = Mathf.Max(top, w.y);
                        // under the pelvis: within 0.12 m of the hips bone across, behind the hip joints (not the thighs)
                        Vector3 dh = v.InverseTransformVector(w - hips.position);
                        // the middle strip only: a rider's thighs hang down both sides of the seat, a kurta's hem drapes
                        if (Mathf.Abs(dh.x) < 0.05f && dh.z < 0.02f && dh.z > -0.12f && dh.y < 0f) butt = Mathf.Min(butt, w.y);
                        Vector3 df = w - lf.position;
                        if (new Vector2(df.x, df.z).magnitude < 0.14f && df.y < 0.05f) sole = Mathf.Min(sole, w.y);
                    }
                    Object.DestroyImmediate(m);
                }
                sb.AppendLine().Append($"  seat {s.index} {a.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name).FirstOrDefault()}: hips {hips.position.y:F3} hip-joint {lul.position.y:F3} butt {butt:F3} -> hips over butt {hips.position.y - butt:F3}; ankle {lf.position.y:F3} sole {sole:F3} -> ankle over sole {lf.position.y - sole:F3}; head top over hips {top - hips.position.y:F3}; skin {s.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh?.name}");
            }
            Debug.Log(sb.ToString());
        }

        public static void RunOffsets() => Each(Offsets);

        /// <summary>Bridge: call VehicleSetupShots.RunColumns (arg.txt: model names, comma separated) - Columns of each.</summary>
        public static void RunColumns() => Each(Columns);

        /// <summary>Bridge: call VehicleSetupShots.RunProfileZ (arg.txt: model names; arg2.txt: the x lines) - ProfileZ of each.</summary>
        public static void RunProfileZ() => Each(ProfileZ);

        public static void Run() => Each(() => Sheet());

        static void Each(System.Action shoot)
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("not playing (press Play in the VehicleSetup scene)");
            string a = Arg();
            var names = new Queue<string>();
            if (a.ToLowerInvariant() == "all") foreach (var n in VehicleSetupWindow.ModelNames()) names.Enqueue(n);
            else if (a.Length > 0) foreach (var n in a.Split(',')) names.Enqueue(n.Trim());
            if (names.Count == 0) { shoot(); return; }
            // one vehicle at a time: show it, let the people settle (the animator and the IK need a few frames), shoot
            double next = 0;
            bool shown = false;
            void Tick()
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
                if (EditorApplication.timeSinceStartup < next) return;
                if (!shown)
                {
                    if (names.Count == 0) { EditorApplication.update -= Tick; Debug.Log("[Nainital] vsetup shots done"); return; }
                    VehicleSetupWindow.Goto(names.Dequeue());
                    shown = true;
                    next = EditorApplication.timeSinceStartup + 1.2;
                    return;
                }
                try { shoot(); } catch (System.Exception e) { Debug.LogWarning("[Nainital] vsetup shot: " + e.Message); }
                shown = false;
                next = EditorApplication.timeSinceStartup + 0.1;
            }
            EditorApplication.update += Tick;
        }

        /// <summary>Every surface of these colliders along a ray, nearest first, back faces included (Physics.RaycastAll
        /// gives one hit per collider: a car body is ONE mesh, so it gave the roof and never the floor under it).</summary>
        public static List<RaycastHit> AllHits(Vector3 from, Vector3 dir, float len, IEnumerable<Collider> cols)
        {
            var list = new List<RaycastHit>();
            bool back = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            foreach (var c in cols)
            {
                if (!c) continue;
                Vector3 o = from;
                float left = len;
                for (int k = 0; k < 24 && left > 0f; k++)
                {
                    if (!c.Raycast(new Ray(o, dir), out var h, left)) break;
                    // a hit's distance from the true start
                    h.distance = Vector3.Distance(from, h.point);
                    list.Add(h);
                    o = h.point + dir * 0.003f;
                    left = len - h.distance - 0.003f;
                }
            }
            Physics.queriesHitBackfaces = back;
            return list.OrderBy(h => h.distance).ToList();
        }

        /// <summary>What is inside the vehicle on show: its transform, its box, and along two lines down its length (the
        /// driver's x and the left passengers' x) every surface a ray straight down meets (back faces too: a Tripo body is a
        /// shell seen from inside), in the vehicle's own space. Tells the floor and the seat cushions apart (or that there
        /// are none). Bridge: call VehicleSetupShots.Probe</summary>
        public static void Probe()
        {
            var root = GameObject.Find("VEHICLE SETUP");
            if (!root || root.transform.childCount == 0) throw new System.Exception("no vehicle on show");
            var v = root.transform.GetChild(0);
            var temp = new List<MeshCollider>();
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.GetComponentInParent<SetupHandle>() || mf.gameObject.layer == 9) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc);
            }
            foreach (var sk in v.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!sk.sharedMesh) continue;
                var baked = new Mesh(); sk.BakeMesh(baked, true);
                var mc = sk.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = baked; temp.Add(mc);
            }
            Physics.SyncTransforms();
            bool back = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            var body = v.GetComponentsInChildren<Renderer>().Where(r => !r.GetComponentInParent<SetupHandle>() && r.gameObject.layer != 9).ToArray();
            var b = body[0].bounds;
            foreach (var r in body) b.Encapsulate(r.bounds);
            var sb = new System.Text.StringBuilder($"[Nainital] probe {v.name}: transform pos {v.position:F3} rot {v.eulerAngles:F0} scale {v.lossyScale:F2}; world box min {b.min:F2} max {b.max:F2}");
            var seats = v.GetComponentsInChildren<SetupHandle>().Where(h => h.kind == SetupHandle.Kind.Seat).OrderBy(h => h.index).ToArray();
            foreach (var s in seats) sb.AppendLine().Append($"  seat {s.index} root local {s.transform.localPosition:F2}");
            float xd = seats.Length > 0 ? seats[0].transform.localPosition.x : 0.4f;
            foreach (float x in new[] { xd, -xd })
            {
                sb.AppendLine().Append($"  line x={x:F2} (local): z: surfaces y (local, top down; * = back face)");
                Vector3 bl = v.InverseTransformPoint(b.min), bh = v.InverseTransformPoint(b.max);
                for (float z = Mathf.Min(bl.z, bh.z) + 0.1f; z < Mathf.Max(bl.z, bh.z); z += 0.2f)
                {
                    Vector3 from = v.TransformPoint(new Vector3(x, Mathf.Max(bl.y, bh.y) + 0.5f, z));
                    var hits = AllHits(from, -v.up, 6f, temp.Cast<Collider>())
                        .Select(h => v.InverseTransformPoint(h.point).y.ToString("F2") + (Vector3.Dot(h.normal, v.up) < 0f ? "*" : "") + (h.collider.name.StartsWith("Body") ? "" : "(" + h.collider.name + ")")).ToArray();
                    sb.AppendLine().Append($"    z {z:F1}: {string.Join(" ", hits)}");
                }
            }
            Physics.queriesHitBackfaces = back;
            foreach (var mc in temp) Object.Destroy(mc);
            Debug.Log(sb.ToString());
        }

        /// <summary>Every surface straight down through each seat's root, each foot marker and each hand marker of the
        /// vehicle on show (vehicle space, top first, * = back face, the part's name when not the body), and on a
        /// two-wheeler the top surface along its centre line every 5 cm - to set seats, feet and grips on the real shape.
        /// Bridge: call VehicleSetupShots.Columns</summary>
        public static void Columns()
        {
            var root = GameObject.Find("VEHICLE SETUP");
            if (!root || root.transform.childCount == 0) throw new System.Exception("no vehicle on show");
            var v = root.transform.GetChild(0);
            var temp = new List<MeshCollider>();
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.GetComponentInParent<SetupHandle>() || mf.gameObject.layer == 9) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc);
            }
            foreach (var sk in v.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!sk.sharedMesh || sk.GetComponentInParent<SetupHandle>() || sk.gameObject.layer == 9) continue;
                var baked = new Mesh(); sk.BakeMesh(baked, true);
                var mc = sk.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = baked; temp.Add(mc);
            }
            Physics.SyncTransforms();
            var body = v.GetComponentsInChildren<Renderer>().Where(r => !r.GetComponentInParent<SetupHandle>() && r.gameObject.layer != 9).ToArray();
            var b = body[0].bounds;
            foreach (var r in body) b.Encapsulate(r.bounds);
            string Col(Vector3 local)
            {
                Vector3 w = v.TransformPoint(local);
                var hits = AllHits(new Vector3(w.x, b.max.y + 0.5f, w.z), Vector3.down, b.size.y + 1f, temp.Cast<Collider>());
                return string.Join(" ", hits.Select(h => v.InverseTransformPoint(h.point).y.ToString("F2") + (Vector3.Dot(h.normal, Vector3.up) < 0f ? "*" : "")
                    + (h.collider.name.StartsWith("Body") ? "" : "(" + h.collider.name + ")")));
            }
            var sb = new System.Text.StringBuilder($"[Nainital] columns {v.name}: box local min {v.InverseTransformPoint(b.min):F2} max {v.InverseTransformPoint(b.max):F2}");
            var handles = v.GetComponentsInChildren<SetupHandle>(true);
            foreach (var s in handles.Where(h => h.kind == SetupHandle.Kind.Seat).OrderBy(h => h.index))
            {
                Vector3 p = s.transform.localPosition;
                var a = s.GetComponentInChildren<Animator>();
                var hips = a && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
                sb.AppendLine().Append($"  seat {s.index} root {p:F2} rotY {s.transform.localEulerAngles.y:F0} hips {(hips ? v.InverseTransformPoint(hips.position).ToString("F2") : "-")} | under root: {Col(p)}");
                foreach (var f in handles.Where(h => h.index == s.index && h.kind != SetupHandle.Kind.Seat && h.kind != SetupHandle.Kind.Entry))
                {
                    Vector3 fp = v.InverseTransformPoint(f.transform.position);
                    sb.AppendLine().Append($"    {f.kind} at {fp:F2} | under: {Col(fp)}");
                }
            }
            if (b.size.x < 1.1f)
            {
                sb.AppendLine().Append("  centre line (z: top surface y):");
                Vector3 lo = v.InverseTransformPoint(b.min), hi = v.InverseTransformPoint(b.max);
                for (float z = Mathf.Min(lo.z, hi.z); z <= Mathf.Max(lo.z, hi.z); z += 0.05f)
                {
                    string c = Col(new Vector3(0f, 0f, z));
                    sb.Append($" {z:F2}:{(c.Length > 0 ? c.Split(' ')[0] : "-")}");
                }
            }
            foreach (var mc in temp) Object.Destroy(mc);
            Debug.Log(sb.ToString());
        }

        /// <summary>Lines along the vehicle on the vehicle on show: for each x in Bridge/arg2.txt (comma separated, vehicle
        /// space), every 5 cm along z, every surface straight down (vehicle space y, * = back face, the part's name when
        /// not the body). The seat cushions show as steps over the floor. Bridge: call VehicleSetupShots.ProfileZ</summary>
        public static void ProfileZ()
        {
            var root = GameObject.Find("VEHICLE SETUP");
            if (!root || root.transform.childCount == 0) throw new System.Exception("no vehicle on show");
            var v = root.transform.GetChild(0);
            string a2 = "";
            try { a2 = File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg2.txt")).Trim(); } catch { }
            // "seats": a line through every seat (and the centre)
            var xs = a2.ToLowerInvariant() == "seats"
                ? v.GetComponentsInChildren<SetupHandle>().Where(h => h.kind == SetupHandle.Kind.Seat).Select(h => Mathf.Round(v.InverseTransformPoint(h.transform.position).x * 100f) / 100f)
                    .Concat(new[] { 0f }).Distinct().OrderBy(x => x).ToArray()
                : a2.Split(',').Where(s => s.Trim().Length > 0).Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var temp = new List<MeshCollider>();
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.GetComponentInParent<SetupHandle>() || mf.gameObject.layer == 9) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc);
            }
            Physics.SyncTransforms();
            var body = v.GetComponentsInChildren<Renderer>().Where(r => !r.GetComponentInParent<SetupHandle>() && r.gameObject.layer != 9).ToArray();
            var b = body[0].bounds;
            foreach (var r in body) b.Encapsulate(r.bounds);
            var sb = new System.Text.StringBuilder($"[Nainital] profileZ {v.name}:");
            foreach (float x in xs)
            {
                sb.AppendLine().Append($"  x {x:F2}:");
                for (float z = b.min.z + 0.025f; z < b.max.z; z += 0.05f)
                {
                    Vector3 w = v.TransformPoint(new Vector3(x, 0f, z));
                    var hits = AllHits(new Vector3(w.x, b.max.y + 0.5f, w.z), Vector3.down, b.size.y + 1f, temp.Cast<Collider>());
                    sb.AppendLine().Append($"    z {z:F3}: " + string.Join(" ", hits.Select(h => v.InverseTransformPoint(h.point).y.ToString("F2") + (Vector3.Dot(h.normal, Vector3.up) < 0f ? "*" : "")
                        + (h.collider.name.StartsWith("Body") ? "" : "(" + h.collider.name + ")"))));
                }
            }
            foreach (var mc in temp) Object.Destroy(mc);
            Debug.Log(sb.ToString());
        }

        /// <summary>The vehicle's width at chosen heights along its length: for each y in Bridge/arg2.txt ("y1,y2;zmin,zmax",
        /// vehicle space) every 2.5 cm along z, the outermost surface met by a ray from each side toward the middle (x of the
        /// left and the right hit, the part's name when not the body). Footpegs and grip ends show as bumps. Bridge: call
        /// VehicleSetupShots.RunSideScan (arg.txt: model names).</summary>
        public static void SideScan()
        {
            var root = GameObject.Find("VEHICLE SETUP");
            if (!root || root.transform.childCount == 0) throw new System.Exception("no vehicle on show");
            var v = root.transform.GetChild(0);
            string a2 = "";
            try { a2 = File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg2.txt")).Trim(); } catch { }
            var parts = a2.Split(';');
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var ys = parts[0].Split(',').Select(s => float.Parse(s.Trim(), ci)).ToArray();
            float z0 = float.Parse(parts[1].Split(',')[0], ci), z1 = float.Parse(parts[1].Split(',')[1], ci);
            var temp = new List<MeshCollider>();
            foreach (var mf in v.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.GetComponentInParent<SetupHandle>() || mf.gameObject.layer == 9) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc);
            }
            Physics.SyncTransforms();
            var sb = new System.Text.StringBuilder($"[Nainital] sidescan {v.name}:");
            foreach (float y in ys)
            {
                sb.AppendLine().Append($"  y {y:F2}:");
                for (float z = z0; z <= z1 + 1e-4f; z += 0.025f)
                {
                    string Side(float sx)
                    {
                        Vector3 from = v.TransformPoint(new Vector3(sx * 1.5f, y, z));
                        var hs = AllHits(from, v.TransformDirection(new Vector3(-sx, 0f, 0f)), 1.5f, temp.Cast<Collider>());
                        if (hs.Count == 0) return "-";
                        var h = hs[0];
                        return v.InverseTransformPoint(h.point).x.ToString("F3") + (h.collider.name.StartsWith("Body") ? "" : "(" + h.collider.name + ")");
                    }
                    sb.AppendLine().Append($"    z {z:F3}: L {Side(-1f)} R {Side(1f)}");
                }
            }
            foreach (var mc in temp) Object.Destroy(mc);
            Debug.Log(sb.ToString());
        }

        public static void RunSideScan() => Each(SideScan);

        /// <summary>The handlebar of the vehicle on show from its own mesh (the parts under it too, the wheel left out), in
        /// vehicle space with the steering straight: its box, and on each side the 3 cm slice at the widest point (the bar
        /// end: x, the y and z range of the slice), plus the hand markers. Bridge: call VehicleSetupShots.RunBar (arg.txt:
        /// model names).</summary>
        public static void Bar()
        {
            var root = GameObject.Find("VEHICLE SETUP");
            if (!root || root.transform.childCount == 0) throw new System.Exception("no vehicle on show");
            var v = root.transform.GetChild(0);
            var sb = new System.Text.StringBuilder($"[Nainital] bar {v.name}:");
            foreach (var bar in v.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Handle", System.StringComparison.OrdinalIgnoreCase)))
            {
                var pts = new List<Vector3>();
                foreach (var mf in bar.GetComponentsInChildren<MeshFilter>())
                {
                    if (!mf.sharedMesh || mf.name.StartsWith("Wheel", System.StringComparison.OrdinalIgnoreCase)) continue;
                    using (var md = Mesh.AcquireReadOnlyMeshData(mf.sharedMesh))
                    {
                        var vs = new Unity.Collections.NativeArray<Vector3>(md[0].vertexCount, Unity.Collections.Allocator.Temp);
                        md[0].GetVertices(vs);
                        foreach (var p in vs) pts.Add(v.InverseTransformPoint(mf.transform.TransformPoint(p)));
                        vs.Dispose();
                    }
                }
                if (pts.Count == 0) continue;
                var mn = new Vector3(pts.Min(p => p.x), pts.Min(p => p.y), pts.Min(p => p.z));
                var mx = new Vector3(pts.Max(p => p.x), pts.Max(p => p.y), pts.Max(p => p.z));
                sb.AppendLine().Append($"  {bar.name}: {pts.Count} verts, box {mn:F2} .. {mx:F2}");
                // the widest points, band by band of height (a scooty's bar carries its apron and cowl)
                for (float y0 = Mathf.Floor(mn.y * 20f) / 20f; y0 < mx.y; y0 += 0.05f)
                {
                    var band = pts.Where(p => p.y >= y0 && p.y < y0 + 0.05f).ToList();
                    if (band.Count == 0) continue;
                    var l = band.OrderBy(p => p.x).First(); var r = band.OrderByDescending(p => p.x).First();
                    sb.AppendLine().Append($"    y {y0:F2}-{y0 + 0.05f:F2}: left end x {l.x:F2} (z {l.z:F2}), right end x {r.x:F2} (z {r.z:F2}), {band.Count} verts");
                }
            }
            foreach (var h in v.GetComponentsInChildren<SetupHandle>().Where(h => h.kind == SetupHandle.Kind.HandL || h.kind == SetupHandle.Kind.HandR))
                sb.AppendLine().Append($"  {h.kind} {h.index} at {v.InverseTransformPoint(h.transform.position):F2}");
            Debug.Log(sb.ToString());
        }

        public static void RunBar() => Each(Bar);

        /// <summary>The animator of every person on show: enabled, controller, state, the bools, layer weights, the rig's
        /// flags. Bridge: call VehicleSetupShots.Diag</summary>
        public static void Diag()
        {
            var root = GameObject.Find("VEHICLE SETUP");
            if (!root) throw new System.Exception("no vehicle on show");
            var sb = new System.Text.StringBuilder("[Nainital] vsetup diag:");
            foreach (var h in root.GetComponentsInChildren<SetupHandle>().Where(x => x.kind == SetupHandle.Kind.Seat).OrderBy(x => x.index))
            {
                var rig = h.GetComponentInChildren<HumanoidRig>(true);
                var a = h.GetComponentInChildren<Animator>(true);
                sb.AppendLine().Append($"  seat {h.index}: rig {(rig ? (rig.enabled ? "on" : "OFF") : "none")} riding {rig?.riding} pillion {rig?.pillion} seated {rig?.seated} full {rig?.Full} rideClips {rig?.HasRideClips}");
                if (!a) { sb.Append(" NO ANIMATOR"); continue; }
                var st = a.GetCurrentAnimatorStateInfo(0);
                string sname = new[] { "Move", "MoveF", "Ride", "PillionRide", "Sit", "Drive", "Air", "Swim", "Pistol", "Dead", "Knocked", "GetUp" }.FirstOrDefault(n => st.IsName(n)) ?? ("hash " + st.shortNameHash);
                sb.Append($" | animator {(a.enabled ? "on" : "OFF")} active {a.isActiveAndEnabled} ctrl {(a.runtimeAnimatorController ? a.runtimeAnimatorController.name : "NONE")} clips {(a.runtimeAnimatorController ? a.runtimeAnimatorController.animationClips.Count(c => c) : 0)} human {a.isHuman} cull {a.cullingMode} state {sname} t {st.normalizedTime:F2}");
                foreach (var p in a.parameters)
                    if (p.type == AnimatorControllerParameterType.Bool && a.GetBool(p.nameHash)) sb.Append(" " + p.name);
                sb.Append($" lean {(a.parameters.Any(p => p.name == "Lean") ? a.GetFloat("Lean") : 0f):F2}");
                for (int l = 1; l < a.layerCount; l++) sb.Append($" L{l}={a.GetLayerWeight(l):F2}");
                var hips = a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
                if (hips) sb.Append($" hipsY {hips.position.y:F2} rootY {h.transform.position.y:F2}");
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>The sheet of the vehicle on show now.</summary>
        public static string Sheet()
        {
            var root = GameObject.Find("VEHICLE SETUP");
            if (!root || root.transform.childCount == 0) throw new System.Exception("no vehicle on show (open Nainital > Vehicle Setup)");
            var vehicle = root.transform.GetChild(0);
            var body = vehicle.GetComponentsInChildren<Renderer>().Where(r => !r.GetComponentInParent<SetupHandle>() && r.gameObject.layer != 9).ToArray();
            var b = body[0].bounds;
            foreach (var r in body) b.Encapsulate(r.bounds);
            var seats = vehicle.GetComponentsInChildren<SetupHandle>().Where(h => h.kind == SetupHandle.Kind.Seat).OrderBy(h => h.index).ToArray();
            float xDrv = seats.Length > 0 ? seats[0].transform.position.x : b.center.x;
            var left = seats.Skip(1).Where(s => s.transform.position.x < -0.1f).ToArray();
            float xPas = left.Length > 0 ? left[0].transform.position.x : -xDrv;
            bool two = b.size.x < 1.1f;   // bikes and scooties: thin
            float span = Mathf.Max(b.size.z, b.size.y) * 0.62f + 0.3f;

            var shots = new List<Texture2D>
            {
                // 1. the left side, whole
                Ortho(new Vector3(b.min.x - 8f, b.center.y, b.center.z), Vector3.right, span, 0.1f, "side L"),
                // 2. cut through the driver / rider from his side (right-hand drive: the right): everything nearer than
                //    his outer shoulder is clipped away
                two ? Ortho(new Vector3(b.max.x + 8f, b.center.y, b.center.z), Vector3.left, span, 0.1f, "side R")
                    : Ortho(new Vector3(b.max.x + 8f, b.center.y, b.center.z), Vector3.left, span, (b.max.x + 8f) - (xDrv + 0.32f), "cut driver"),
                // 3. cut through the passengers on the left
                two ? Persp(b, new Vector3(-1.2f, 0.55f, 1.4f), 0f, "front L high")
                    : Ortho(new Vector3(b.min.x - 8f, b.center.y, b.center.z), Vector3.right, span, (xPas - 0.32f) - (b.min.x - 8f), "cut passengers"),
                // 4. front three-quarter from the right, a little high
                Persp(b, new Vector3(1.25f, 0.45f, 1.3f), 0f, "front R"),
                // 5. top, the roof cut off (a car) / the whole thing (a bike)
                TopCut(b, two, span),
                // 6. close on the driver's / rider's hands and the wheel / the bar, from behind and above his right shoulder
                Close(seats.Length > 0 ? seats[0].transform : vehicle, b, two),
            };

            var sheet = new Texture2D(W * 3, H * 2, TextureFormat.RGB24, false);
            for (int i = 0; i < shots.Count; i++)
            {
                int cx = (i % 3) * W, cy = (1 - i / 3) * H;
                sheet.SetPixels(cx, cy, W, H, shots[i].GetPixels());
                Object.DestroyImmediate(shots[i]);
            }
            sheet.Apply();
            string outP = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/vsetup_" + vehicle.name + ".png"));
            File.WriteAllBytes(outP, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            var info = string.Join("; ", seats.Select(s =>
            {
                var rig = s.GetComponentInChildren<Animator>();
                var hips = rig && rig.isHuman ? rig.GetBoneTransform(HumanBodyBones.Hips) : null;
                var fl = rig && rig.isHuman ? rig.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
                string st = rig ? (rig.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name).FirstOrDefault() ?? "-") : "-";
                var ik = s.GetComponent<HandIK>();
                var hr = s.GetComponentInChildren<HumanoidRig>();
                float lift = hr ? hr.SoleLift : 0f;
                string miss = ik && fl && ik.footLeft ? $" footL-target {(fl.position - (ik.footLeft.position + Vector3.up * lift)).magnitude * 100f:F0}cm" : "";
                // the seat of the trousers on the surface, the head under the roof (the setup's own measures)
                var sd = VehicleSetupWindow.SeatData(s.index);
                if (hr && hips && sd != null && !float.IsNaN(hr.Fit.x))
                {
                    if (sd.surface >= 0f) miss += $" butt-gap {((hips.position.y - hr.Fit.x) - sd.surface) * 100f:F0}cm";
                    if (sd.roof >= 0f) miss += $" head-room {(sd.roof - (hips.position.y + hr.Fit.y)) * 100f:F0}cm";
                    miss += $" fit {hr.Fit.x:F2}/{hr.Fit.y:F2}/{hr.Fit.z:F2}";
                }
                if (ik) miss += $" bend {ik.BendNow():F0}deg (max {ik.maxBend:F0})";
                var hl = rig && rig.isHuman ? rig.GetBoneTransform(HumanBodyBones.LeftHand) : null;
                if (ik && hl && ik.left) miss += $" handL-target {(hl.position - ik.left.position).magnitude * 100f:F0}cm";
                // where the hands' targets sit from the hips: ahead (along the vehicle) and up
                if (ik && hips && ik.left && ik.right)
                {
                    var v = ik.vehicle ? ik.vehicle : s.transform;
                    Vector3 d = v.InverseTransformVector((ik.left.position + ik.right.position) * 0.5f - hips.position);
                    miss += $" grips {d.z:F2}m ahead {d.y:F2}m up";
                    var ua = rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    var la = rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    if (ua && la && hl)
                    {
                        Vector3 sh = v.InverseTransformVector(ua.position - hips.position);
                        miss += $" armL {(Vector3.Distance(ua.position, la.position) + Vector3.Distance(la.position, hl.position)):F2}m shoulderL {sh.z:F2}ahead {sh.y:F2}up";
                    }
                }
                int nAnim = s.GetComponentsInChildren<Animator>().Length;
                return $"seat {s.index}: clip {st} anims {nAnim}" + (hips ? $" hipsY {hips.position.y:F2}" : "") + (fl ? $" footL {fl.position.x:F2},{fl.position.y:F2},{fl.position.z:F2}" : "") + miss;
            }));
            Debug.Log($"[Nainital] vsetup sheet {outP} ({vehicle.name}, box {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2}) {info}");
            return outP;
        }

        static Texture2D Ortho(Vector3 pos, Vector3 look, float half, float near, string label)
        {
            var cam = NewCam();
            cam.orthographic = true;
            cam.orthographicSize = half;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look, Vector3.up));
            cam.nearClipPlane = Mathf.Max(0.01f, near);
            cam.farClipPlane = 30f;
            return Grab(cam);
        }

        static Texture2D Persp(Bounds b, Vector3 dir, float near, string label)
        {
            var cam = NewCam();
            cam.fieldOfView = 30f;
            float d = b.extents.magnitude / Mathf.Sin(15f * Mathf.Deg2Rad) * 1.05f;
            cam.transform.position = b.center + dir.normalized * d;
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = d * 3f;
            return Grab(cam);
        }

        static Texture2D TopCut(Bounds b, bool two, float span)
        {
            var cam = NewCam();
            cam.orthographic = true;
            cam.orthographicSize = span;
            cam.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y + 8f, b.center.z), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            // a car: cut 0.3 m under the roof so the seats and the people show
            cam.nearClipPlane = two ? 0.1f : 8f + 0.3f;
            cam.farClipPlane = 30f;
            return Grab(cam);
        }

        static Texture2D Close(Transform seat, Bounds b, bool two)
        {
            var cam = NewCam();
            cam.fieldOfView = 40f;
            Vector3 head = seat.position + Vector3.up * (two ? 1.2f : 1.05f);
            Vector3 at = seat.position + Vector3.up * (two ? 0.85f : 0.8f) + Vector3.forward * (two ? 0.45f : 0.45f);
            // behind and above the right shoulder, inside the car (the roof clipped by the near plane)
            cam.transform.position = head + new Vector3(two ? 0.55f : 0.25f, 0.45f, -0.75f);
            cam.transform.LookAt(at);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            return Grab(cam);
        }

        static Camera NewCam()
        {
            var go = new GameObject("VSetupShotCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.75f, 0.8f);
            cam.enabled = false;
            return cam;
        }

        static Texture2D Grab(Camera cam)
        {
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(cam.gameObject);
            return tex;
        }
    }
}

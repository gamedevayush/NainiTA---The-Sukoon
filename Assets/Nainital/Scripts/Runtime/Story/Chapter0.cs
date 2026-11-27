using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Chapter 0 - Prologue "Akela Safar" (docs/Nainital_Story_Mission_Bible.docx, B. Chapter 0; docs/STORY.md):
    ///   0.0  The Last Trip  - the family's car on the Haldwani - Nainital highway (owner, 26 Nov 2026: "crash haldwani
    ///        nainital road pe krna"), the talk, the car from the wrong side, the crash at the valley's edge, the fire, the
    ///        QTE out through the broken glass, the ambulance and the police.
    ///   0.0b Sirens         - his eyes in the ambulance: blur, the lights, the site falling behind through the back window.
    ///   0.0c Waking Up      - the hospital room, the nurse, the doctor.
    ///   0.1  A New Start    - a few days later on the Mall Road: the basics (walk, run, camera, map, phone), to the lake.
    ///   0.2  A Roof Over My Head - the Uncle at the lake, follow him towards the Zoo Road house, the landlord, Uncle gone,
    ///        sleep = save.
    /// The scenes are Unity Timelines in Assets/Nainital/Scenes/Story_C0.unity (owner, 26 Nov 2026: "timeline pe shift
    /// krdo"), first made by Editor/StoryTimelineBaker from the places and the motion below, then edited by hand. This file
    /// holds the missions, the tutorial checks, Uncle's walk, and the highway / ground helpers the baker uses.
    /// </summary>
    public static class Chapter0
    {
        public const string Scene = "Story_C0";

        // ------------------------------------------------------------------ places (world x, z), measured 26 Nov 2026
        public static readonly Vector3 HighwayStart = new Vector3(150f, 0f, -3240f);   // the map's edge on the Haldwani highway
        public static readonly Vector3 Bend = new Vector3(267.1f, 0f, -3034.3f);       // the bend: a ~46 m drop on the left going up
        public static readonly Vector3 HighwayFar = new Vector3(16f, 0f, -2354f);      // further up towards Nainital
        public static readonly Vector3 MallStart = new Vector3(833.0f, 0f, -895.8f);   // Lower Mall, ~160 m from Tallital
        public static readonly Vector3 UncleSpot = new Vector3(804.1f, 0f, -742.9f);   // Lower Mall by the lake, ~315 m from Tallital
        public static readonly Vector3 HouseFront = new Vector3(1233.0f, 0f, -1255.3f);   // the Zoo Road house's front at the road (build log)
        public const float Lane = 2.0f;   // a lane's middle from the highway's (8 m wide)

        // Uncle's walk (26 Nov 2026): the house's only way in is the Zoo Road from the Snow View Road, ~800 m from any point
        // by the lake - too long to follow. He walks the first ~290 m (up the Upper Mall, the hairpin onto the Snow View
        // Road towards the Zoo Road), then the scene cuts to the house. Shortest walk on world.json's roads and paths
        // (centre lines, x / z pairs); StartWalk keeps him to the side of the road.
        static readonly float[] WalkXZ = {
            804.1f, -742.9f, 811.4f, -737.9f, 809.3f, -728.3f, 807.7f, -720.5f, 805.1f, -708.9f, 803.3f, -701.1f, 801.5f, -693.4f,
            799.5f, -685.7f, 797.4f, -678.0f, 794.4f, -668.4f, 792.2f, -662.7f, 789.2f, -655.4f, 786.2f, -648.0f, 783.3f, -640.6f,
            785.0f, -635.1f, 801.2f, -665.4f, 817.4f, -695.8f, 827.6f, -718.0f, 832.7f, -729.1f, 837.4f, -736.8f, 860.2f, -763.0f,
            880.2f, -788.9f };

        // ------------------------------------------------------------------ the chain
        public static void Build(List<Mission> list)
        {
            var m0 = new Mission { id = "s0_0", title = "The Last Trip", intro = "", story = true, quiet = true, giver = StoryWalker.Ground(MallStart + new Vector3(2f, 0f, 3f)) };
            m0.steps.Add(new TimelineStep(Scene, "c0_0_trip") { checkpoint = true });
            list.Add(m0);

            var m1 = new Mission { id = "s0_0b", title = "Sirens", story = true, quiet = true, autoStart = true };
            m1.steps.Add(new TimelineStep(Scene, "c0_0b_sirens") { checkpoint = true });
            list.Add(m1);

            var m2 = new Mission { id = "s0_0c", title = "Waking Up", story = true, quiet = true, autoStart = true };
            m2.steps.Add(new TimelineStep(Scene, "c0_0c_hospital") { checkpoint = true });
            list.Add(m2);

            var m3 = new Mission { id = "s0_1", title = "A New Start", story = true, autoStart = true,
                intro = "Naya shehar, nayi shuruaat. Chalna-phirna seekho aur jheel ke kinare jaao." };
            m3.steps.Add(new TimelineStep(Scene, "c0_1_newstart") { checkpoint = true });
            Vector3 from = Vector3.zero;
            float ran = 0f, looked = 0f;
            bool mapSeen = false, phoneSeen = false;
            m3.steps.Add(new WhenStep("Chalo: W A S D (ya joystick)", () => P && Flat(P.transform.position - from) > 8f, begin: () => from = P ? P.transform.position : Vector3.zero));
            m3.steps.Add(new WhenStep("Daudo: Shift dabaake rakho", () => (ran += Speed() > 4.6f ? Time.deltaTime : 0f) > 1.2f, begin: () => { ran = 0f; lastPos = P ? P.transform.position : Vector3.zero; }));
            m3.steps.Add(new WhenStep("Camera ghumao: mouse (ya screen pe ungli)", () => (looked += Mathf.Abs(GameInput.Look.x) + Mathf.Abs(GameInput.Look.y)) > 140f, begin: () => looked = 0f));
            m3.steps.Add(new WhenStep("Naksha kholo aur band karo: M", () => { if (Minimap.BigMapOpen) mapSeen = true; return mapSeen && !Minimap.BigMapOpen; }, begin: () => mapSeen = false));
            m3.steps.Add(new WhenStep("Phone kholo aur band karo: T", () => { if (Phone.IsOpen) phoneSeen = true; return phoneSeen && !Phone.IsOpen; }, begin: () => phoneSeen = false));
            m3.steps.Add(new GoToStep("Jheel ke kinare jaao", StoryWalker.Ground(UncleSpot), 5f));
            list.Add(m3);

            var m4 = new Mission { id = "s0_2", title = "A Roof Over My Head", story = true, autoStart = true, giver = StoryWalker.Ground(UncleSpot),
                intro = "", passNote = "Ghar mil gaya - bistar pe sone se game save hota hai" };
            m4.steps.Add(new TimelineStep(Scene, "c0_2_uncle") { checkpoint = true });
            m4.steps.Add(new HookStep("", StartWalk));
            m4.steps.Add(new FollowStep("Uncle ke peeche-peeche chalo", () => walker ? walker.transform : null,
                () => walker && walker.Arrived && P && Flat(P.transform.position - walker.transform.position) < 7f));
            m4.steps.Add(new HookStep("", () => { StopWalk(); return null; }));
            m4.steps.Add(new TimelineStep(Scene, "c0_2_house"));
            m4.steps.Add(new WhenStep("Ghar ke andar bistar pe so jao (save)", () => HouseBed.SleepCount > sleeps, () => Bed(),
                begin: () => sleeps = HouseBed.SleepCount));
            m4.onEnd = ok => StopWalk();
            list.Add(m4);
        }

        static int sleeps;
        static Vector3 lastPos;
        static PlayerController P => PlayerController.Instance;
        public static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }
        static float Speed()
        {
            if (!P || Time.deltaTime <= 0f) return 0f;
            Vector3 p = P.transform.position;
            float v = Flat(p - lastPos) / Time.deltaTime;
            lastPos = p;
            return v;
        }
        static Vector3? Bed() { var b = Object.FindAnyObjectByType<HouseBed>(); return b ? b.transform.position : (Vector3?)null; }

        // ------------------------------------------------------------------ the highway (the baker draws the drive with these)
        public static readonly List<Vector3> Route = new List<Vector3>();
        public static readonly List<float> RouteS = new List<float>();
        public static float BendS { get; private set; }
        public static float ImpactS => BendS - 12f;
        public static float RouteLength => RouteS.Count > 0 ? RouteS[RouteS.Count - 1] : 0f;

        /// <summary>The highway's centre line from the map's edge up past the bend (RoadRoute; its Nearest counts height
        /// too, so the ends are put on the ground first).</summary>
        public static bool BuildRoute()
        {
            Route.Clear(); RouteS.Clear();
            bool ok = RoadRoute.ReadyAny() && RoadRoute.Find(StoryWalker.Ground(HighwayStart), StoryWalker.Ground(HighwayFar), Route) && Route.Count > 4;
            if (!ok)
            {
                Debug.LogWarning("[Nainital] chapter 0: no road route on the highway, a straight line instead");
                Route.Clear();
                for (int i = 0; i <= 40; i++) Route.Add(StoryWalker.Ground(Vector3.Lerp(HighwayStart, HighwayFar, i / 40f)));
            }
            float acc = 0f;
            for (int i = 0; i < Route.Count; i++) { if (i > 0) acc += Flat(Route[i] - Route[i - 1]); RouteS.Add(acc); }
            float bd = float.MaxValue;
            BendS = 0f;
            for (int i = 0; i < Route.Count; i++) { float d = Flat(Route[i] - Bend); if (d < bd) { bd = d; BendS = RouteS[i]; } }
            Debug.Log($"[Nainital] chapter 0: highway route {Route.Count} points, {acc:F0} m, the bend at {BendS:F0} m");
            return ok;
        }

        public static void Sample(float s, out Vector3 p, out Vector3 tan)
        {
            s = Mathf.Clamp(s, 0f, RouteLength);
            int i = 1;
            while (i < RouteS.Count - 1 && RouteS[i] < s) i++;
            float a = RouteS[i - 1], b = RouteS[i];
            float u = b > a ? (s - a) / (b - a) : 0f;
            p = Vector3.Lerp(Route[i - 1], Route[i], u);
            tan = Route[i] - Route[i - 1]; tan.y = 0f;
            if (i + 1 < Route.Count) { Vector3 t2 = Route[i + 1] - Route[i]; t2.y = 0f; tan = Vector3.Lerp(tan.normalized, t2.normalized, u); }
            tan = tan.sqrMagnitude > 1e-6f ? tan.normalized : Vector3.forward;
        }

        public static Vector3 LeftOf(Vector3 tan) => new Vector3(-tan.z, 0f, tan.x);

        /// <summary>A vehicle on the route at s, `lateral` metres to the left of the way up (negative: right), turned
        /// `yaw` degrees from the way up (180: coming down), sat on the ground under its four tyres.</summary>
        public static void PlaceOnRoute(GameObject car, float s, float lateral, float yaw)
        {
            if (!car) return;
            Sample(s, out var p, out var tan);
            Vector3 c = p + LeftOf(tan) * lateral;
            Vector3 fwd = Quaternion.AngleAxis(yaw, Vector3.up) * tan;
            Vector3 f = StoryWalker.Ground(c + fwd * 1.3f), r = StoryWalker.Ground(c - fwd * 1.3f);
            Vector3 dir = f - r;
            car.transform.SetPositionAndRotation((f + r) * 0.5f, Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir.normalized : fwd, Vector3.up));
            SitOnGround(car);
        }

        /// <summary>The car on its four tyres: turned to the plane under its wheels and lowered / raised till the tyres touch
        /// (a slope, the road's camber; the model's pivot is not always at the tyres' bottom).</summary>
        public static void SitOnGround(GameObject car, float extraPitch = 0f, float extraRoll = 0f)
        {
            var vp = car ? car.GetComponentInChildren<VehicleParts>() : null;
            if (!vp) return;
            var hub = vp.SuspensionPoints();
            float wr = Mathf.Max(0.2f, vp.wheelRadius);
            var tr = vp.transform;
            var g = new Vector3[4];
            for (int i = 0; i < 4; i++) g[i] = StoryWalker.Ground(tr.TransformPoint(hub[i] - Vector3.up * wr) + Vector3.up * 1.5f);
            Vector3 fwd = (g[0] + g[1]) * 0.5f - (g[2] + g[3]) * 0.5f, right = (g[1] + g[3]) * 0.5f - (g[0] + g[2]) * 0.5f;
            if (fwd.sqrMagnitude < 1e-4f || right.sqrMagnitude < 1e-4f) return;
            Vector3 up = Vector3.Cross(fwd, right).normalized;
            car.transform.rotation = Quaternion.LookRotation(fwd.normalized, up) * Quaternion.Euler(extraPitch, 0f, extraRoll);
            float dy = 0f;
            for (int i = 0; i < 4; i++) dy += g[i].y - tr.TransformPoint(hub[i] - Vector3.up * wr).y;
            car.transform.position += Vector3.up * (dy / 4f);
        }

        /// <summary>Which way the lake is from a point (its nearest edge).</summary>
        public static Vector3 TowardLake(Vector3 p)
        {
            var la = LakeArea.Instance ? LakeArea.Instance : Object.FindAnyObjectByType<LakeArea>();
            if (!la || la.innerOutline == null || la.innerOutline.Length < 3) return Vector3.right;
            Vector2 q = new Vector2(p.x, p.z), best = q; float bd = float.MaxValue;
            var o = la.innerOutline;
            for (int i = 0; i < o.Length; i++)
            {
                Vector2 a = o[i], b = o[(i + 1) % o.Length], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                Vector2 c = a + ab * t;
                float d = (c - q).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            Vector3 dir = new Vector3(best.x - p.x, 0f, best.y - p.z);
            return dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.right;
        }

        // ------------------------------------------------------------------ 0.2: Uncle's walk
        static StoryWalker walker;

        static IEnumerator StartWalk()
        {
            StopWalk();
            var go = Cutscene.MakePerson("Story: Uncle", "m_old_sweater", false, true, false, out var rig);
            walker = go.AddComponent<StoryWalker>();
            walker.rig = rig;
            walker.follower = P ? P.transform : null;
            walker.speed = 1.6f;
            // from where the lake scene left him (its StoryActor "uncle"), else the spot by the railing
            Vector3 start = StoryDirector.LastPoses.TryGetValue("uncle", out var up) ? up.position : StoryWalker.Ground(UncleSpot);
            var pts = new List<Vector3>();
            for (int i = 0; i + 1 < WalkXZ.Length; i += 2) pts.Add(new Vector3(WalkXZ[i], 0f, WalkXZ[i + 1]));
            var path = new List<Vector3> { start };
            for (int i = 0; i < pts.Count; i++)
            {
                // 1.6 m left of the centre line (the side of the road; a hairpin's corner keeps its own point)
                Vector3 a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(pts.Count - 1, i + 1)];
                Vector3 t = b - a; t.y = 0f;
                Vector3 off = t.sqrMagnitude > 1e-4f && i > 0 && i < pts.Count - 1 ? LeftOf(t.normalized) * 1.6f : Vector3.zero;
                path.Add(StoryWalker.Ground(pts[i] + off));
            }
            walker.path = path;
            Vector3 dir = path[1] - start; dir.y = 0f;
            go.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir : Vector3.forward));
            float len = 0f; for (int i = 1; i < path.Count; i++) len += Flat(path[i] - path[i - 1]);
            Debug.Log($"[Nainital] chapter 0: Uncle walks {path.Count} points, {len:F0} m towards the Zoo Road");
            yield break;
        }

        static void StopWalk()
        {
            if (walker) Object.Destroy(walker.gameObject);
            walker = null;
        }
    }
}

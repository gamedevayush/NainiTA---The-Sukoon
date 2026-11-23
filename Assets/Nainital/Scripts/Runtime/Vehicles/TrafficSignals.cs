using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Traffic lights where three or more main roads meet (owner, 23 Nov 2026: "traffic light lgaani pdengi jahan atleast 3
    /// bhi major roads hain...vrna jam bahut tgda lgra"; "naina devi ka jo mall road gate hai..., bus stand wala chouraha,
    /// or bhi agar kuch major ho to...ek perfect se lights lgaana"). Found from the road network when the game starts (no
    /// Build World): the map's junctions (RoadNetwork.meetings grouped within 20 m), their arms (a road leaving the meeting
    /// point), kept when 3+ arms are main roads (MajorWidth). Each arm traffic comes in on gets a stop line and a signal on
    /// the driver's left. One arm at a time has green - vehicle-actuated: an arm with nobody waiting is skipped, a green with
    /// nobody left on it ends early - then amber, then all red while the junction clears. TrafficCar and TrafficBike ask
    /// HoldAt. Lights hold the town's traffic only (the player is not fined yet).
    /// </summary>
    public class TrafficSignals : MonoBehaviour
    {
        public const float MajorWidth = 6f, MaxGreen = 16f, MinGreen = 6f, Amber = 3f, AllRed = 2.5f;
        public static TrafficSignals Instance { get; private set; }

        public sealed class Arm
        {
            public int road;
            public string name;
            public float width;
            public Vector3 stop;     // the middle of the road at the stop line
            public Vector3 inDir;    // the way traffic comes in (towards the junction), flat
            public Renderer red, amber, green;
            public int state = 0;    // 0 red, 1 amber, 2 green
            public bool waiting;
        }

        public sealed class Junction
        {
            public Vector3 centre;
            public float radius;
            public readonly List<Arm> arms = new List<Arm>();
            public int current;
            public int phase;        // 0 green, 1 amber, 2 all red
            public float t;
        }

        public readonly List<Junction> junctions = new List<Junction>();
        RoadNetwork net;
        float scanT;
        static Material matPole, matBand, matHead, matPlate, matLine;
        static readonly Material[] lampOff = new Material[3], lampOn = new Material[3];

        // ------------------------------------------------------------------ finding the junctions
        struct Cand { public Vector3 at; public int road; public Vector3 outDir; public float width; public bool incoming; public string name; }

        public void Build(RoadNetwork network)
        {
            Instance = this;
            net = network;
            junctions.Clear();
            if (!net || net.meetings == null) return;
            var cands = new List<Cand>();
            foreach (var m in net.meetings)
            {
                if (m.road < 0 || m.road >= net.roads.Length || m.other < 0 || m.other >= net.roads.Length) continue;
                var mr = net.roads[m.road];
                var o = net.roads[m.other];
                if (m.idx < 0 || m.idx >= o.pts.Length || mr.pts.Length < 2) continue;
                Vector3 J = o.pts[m.idx];
                // the meeting road leaves the junction from its end; the other road passes through it (or ends there)
                AddArm(cands, J, m.road, m.end == 0 ? 0 : mr.pts.Length - 1, m.end == 0 ? 1 : -1);
                if (m.idx > 0) AddArm(cands, J, m.other, m.idx, -1);
                if (m.idx < o.pts.Length - 1) AddArm(cands, J, m.other, m.idx, 1);
            }
            // grouped: meetings within 32 m are one junction (the Tallital chauraha is three meetings 23-31 m apart round its
            // island: three sets of lights there were one too many); arms the same way (within 30 deg) are one arm
            var groups = new List<(List<Vector3> at, List<Cand> arms)>();
            foreach (var c in cands)
            {
                int gi = -1;
                for (int g = 0; g < groups.Count && gi < 0; g++)
                    foreach (var p in groups[g].at) if (Flat(p - c.at) < 32f) { gi = g; break; }
                if (gi < 0) { groups.Add((new List<Vector3>(), new List<Cand>())); gi = groups.Count - 1; }
                groups[gi].at.Add(c.at);
                bool dup = false;
                foreach (var a in groups[gi].arms) if (Vector3.Angle(a.outDir, c.outDir) < 30f) { dup = true; break; }
                if (!dup) groups[gi].arms.Add(c);
            }
            foreach (var (at, arms) in groups)
            {
                // main-road arms, a road counted twice at most (through the junction): a hairpin of one road near a lane
                // read as four arms (421,-1564)
                int major = 0;
                var perRoad = new Dictionary<int, int>();
                foreach (var a in arms)
                {
                    if (a.width < MajorWidth) continue;
                    perRoad.TryGetValue(a.road, out int n0);
                    if (n0 >= 2) continue;
                    perRoad[a.road] = n0 + 1;
                    major++;
                }
                if (major < 3) continue;
                Vector3 centre = Vector3.zero;
                foreach (var p in at) centre += p;
                centre /= at.Count;
                float spread = 0f;
                foreach (var p in at) spread = Mathf.Max(spread, Flat(p - centre));
                float radius = 9f;
                foreach (var a in arms) radius = Mathf.Max(radius, a.width * 0.5f + 5f);
                radius += spread;   // the stop lines outside every meeting point of a big chauraha
                if (net.IslandNear(centre, 15f, out var isl)) radius = Mathf.Max(radius, Flat(isl.centre - centre) + isl.radius + 6f);
                var j = new Junction { centre = centre, radius = radius };
                foreach (var a in arms)
                {
                    if (!a.incoming) continue;   // a one-way road leaving it: nobody comes in there
                    var arm = new Arm { road = a.road, name = a.name, width = a.width, inDir = -a.outDir };
                    arm.stop = PointOut(a.road, centre, a.outDir, radius);
                    j.arms.Add(arm);
                }
                if (j.arms.Count < 2) continue;
                j.current = Random.Range(0, j.arms.Count);
                j.t = Random.Range(0f, MaxGreen);
                junctions.Add(j);
            }
            foreach (var j in junctions) Dress(j);
            foreach (var j in junctions) Show(j);
            var sb = new System.Text.StringBuilder($"[Nainital] traffic signals: {junctions.Count} junctions");
            foreach (var j in junctions)
            {
                sb.Append($"\n  ({j.centre.x:F0},{j.centre.z:F0}) r {j.radius:F0} m:");
                foreach (var a in j.arms) sb.Append($" [{(string.IsNullOrEmpty(a.name) ? "#" + a.road : a.name)} w{a.width:F1}]");
            }
            Debug.Log(sb.ToString());
        }

        static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        /// <summary>The arm of `road` leaving J from point i0 the way `step` goes: its direction (taken ~12 m out).</summary>
        void AddArm(List<Cand> into, Vector3 J, int road, int i0, int step)
        {
            var r = net.roads[road];
            var pts = r.pts;
            Vector3 d = Vector3.zero;
            for (int i = i0 + step; i >= 0 && i < pts.Length; i += step)
            {
                d = pts[i] - J;
                d.y = 0f;
                if (d.magnitude >= 12f) break;
            }
            if (d.sqrMagnitude < 4f) return;   // a stub
            // traffic comes in along -step: both ways, or one-way that way (oneWay +1 = along the points)
            bool incoming = r.oneWay == 0 || r.oneWay == -step;
            into.Add(new Cand { at = J, road = road, outDir = d.normalized, width = r.width, incoming = incoming, name = r.name });
        }

        /// <summary>Where `road` is `dist` out from the junction's centre, near the arm's way out: its nearest point there.</summary>
        Vector3 PointOut(int road, Vector3 centre, Vector3 outDir, float dist)
        {
            var pts = net.roads[road].pts;
            Vector3 want = centre + outDir * dist, best = want;
            float bd = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 rel = pts[i] - centre;
                rel.y = 0f;
                if (Vector3.Dot(rel, outDir) < 0f) continue;
                float e = Mathf.Abs(rel.magnitude - dist) + Flat(pts[i] - want) * 0.3f;
                if (e < bd) { bd = e; best = pts[i]; }
            }
            return best;
        }

        // ------------------------------------------------------------------ the lights
        void Update()
        {
            float dt = Time.deltaTime;
            scanT -= dt;
            bool scan = scanT <= 0f;
            if (scan) scanT = 0.5f;
            foreach (var j in junctions)
            {
                if (scan) foreach (var a in j.arms) a.waiting = Waiting(j, a);
                j.t += dt;
                var cur = j.arms[j.current];
                bool othersWait = false;
                for (int k = 0; k < j.arms.Count; k++) if (k != j.current && j.arms[k].waiting) othersWait = true;
                switch (j.phase)
                {
                    case 0:   // green: its full time, or less once its arm is empty and somebody waits elsewhere
                        if ((j.t > MaxGreen && othersWait) || (j.t > MinGreen && !cur.waiting && othersWait)) { j.phase = 1; j.t = 0f; Show(j); }
                        break;
                    case 1:
                        if (j.t > Amber) { j.phase = 2; j.t = 0f; Show(j); }
                        break;
                    default:   // all red while the junction clears, then the next arm somebody waits on
                        if (j.t > AllRed)
                        {
                            int next = j.current;
                            for (int k = 1; k <= j.arms.Count; k++)
                            {
                                int n = (j.current + k) % j.arms.Count;
                                if (j.arms[n].waiting) { next = n; break; }
                            }
                            j.current = next;
                            j.phase = 0;
                            j.t = 0f;
                            Show(j);
                        }
                        break;
                }
            }
        }

        static readonly Collider[] seen = new Collider[16];

        /// <summary>Somebody coming in on this arm within 45 m of its stop line (a vehicle on its lane, facing in).</summary>
        bool Waiting(Junction j, Arm a)
        {
            Vector3 c = a.stop - a.inDir * 22f + Vector3.up;
            int n = Physics.OverlapBoxNonAlloc(c, new Vector3(a.width * 0.5f + 1.5f, 2f, 23f), seen, Quaternion.LookRotation(a.inDir),
                                               1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var rb = seen[i].attachedRigidbody;
                if (!rb) continue;
                Vector3 f = rb.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.01f && Vector3.Dot(f.normalized, a.inDir) > 0.4f) return true;
            }
            return false;
        }

        void Show(Junction j)
        {
            for (int k = 0; k < j.arms.Count; k++)
            {
                var a = j.arms[k];
                a.state = k != j.current ? 0 : j.phase == 0 ? 2 : j.phase == 1 ? 1 : 0;
                if (a.red) a.red.sharedMaterial = a.state == 0 ? lampOn[0] : lampOff[0];
                if (a.amber) a.amber.sharedMaterial = a.state == 1 ? lampOn[1] : lampOff[1];
                if (a.green) a.green.sharedMaterial = a.state == 2 ? lampOn[2] : lampOff[2];
            }
        }

        /// <summary>Must a vehicle at `pos` facing `fwd` at `speed` stop for a light? dist: how far its stop line is.
        /// Red: stop. Amber: stop if it still can at a comfortable 3.5 m/s2, else go on. Already past the line: go.</summary>
        public bool HoldAt(Vector3 pos, Vector3 fwd, float speed, out float dist)
        {
            dist = 0f;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) return false;
            fwd.Normalize();
            foreach (var j in junctions)
            {
                float dc = Flat(j.centre - pos);
                if (dc > j.radius + 75f || dc < j.radius - 2f) continue;   // far, or in the junction already
                foreach (var a in j.arms)
                {
                    if (a.state == 2 || Vector3.Dot(fwd, a.inDir) < 0.45f) continue;
                    Vector3 rel = pos - a.stop;
                    rel.y = 0f;
                    float along = -Vector3.Dot(rel, a.inDir);   // + = the line still ahead
                    float lat = Vector3.Dot(rel, Vector3.Cross(Vector3.up, a.inDir));
                    if (along < -0.5f || along > 75f || Mathf.Abs(lat) > a.width * 0.5f + 3f) continue;
                    if (a.state == 1 && speed * speed / (2f * 3.5f) > along - 0.5f) return false;
                    dist = along;
                    return true;
                }
            }
            return false;
        }

        /// <summary>A signalled junction within `range` m (no overtaking towards it).</summary>
        public bool Near(Vector3 pos, float range)
        {
            foreach (var j in junctions) if (Flat(j.centre - pos) < j.radius + range) return true;
            return false;
        }

        // ------------------------------------------------------------------ the signal posts
        static void Materials()
        {
            if (matPole) return;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material M(Color c, float smooth = 0.35f, float metal = 0.3f)
            {
                var m = new Material(lit);
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                return m;
            }
            matPole = M(new Color(0.24f, 0.25f, 0.27f));
            matHead = M(new Color(0.05f, 0.05f, 0.05f), 0.45f, 0.1f);
            matPlate = M(new Color(0.95f, 0.75f, 0.08f), 0.3f, 0f);
            matLine = M(new Color(0.92f, 0.92f, 0.9f), 0.1f, 0f);
            matLine.SetFloat("_Cull", 0f);   // seen from either side whatever way the strip winds
            // the bottom band: the yellow-and-black stripes of an Indian signal post
            var tex = new Texture2D(2, 8) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int y = 0; y < 8; y++) for (int x = 0; x < 2; x++) tex.SetPixel(x, y, (y / 2) % 2 == 0 ? new Color(0.95f, 0.78f, 0.05f) : new Color(0.05f, 0.05f, 0.05f));
            tex.Apply();
            matBand = M(Color.white, 0.3f, 0f);
            matBand.SetTexture("_BaseMap", tex);
            Color[] lamp = { new Color(1f, 0.08f, 0.05f), new Color(1f, 0.6f, 0.05f), new Color(0.1f, 1f, 0.35f) };
            for (int k = 0; k < 3; k++)
            {
                lampOff[k] = M(lamp[k] * 0.18f, 0.8f, 0f);
                lampOn[k] = M(lamp[k], 0.8f, 0f);
                lampOn[k].EnableKeyword("_EMISSION");
                lampOn[k].SetColor("_EmissionColor", lamp[k] * 6f);
                lampOn[k].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
        }

        static GameObject Part(PrimitiveType t, Transform parent, Vector3 local, Vector3 scale, Material m, bool keepCollider = false)
        {
            var g = GameObject.CreatePrimitive(t);
            if (!keepCollider) Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        static readonly Collider[] block = new Collider[8];
        static int groundMask = -1;

        static bool Ground(Vector3 p, out Vector3 g)
        {
            if (groundMask < 0) groundMask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer));
            g = p;
            var t = Terrain.activeTerrain;
            float top = t ? t.SampleHeight(p) + t.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, top) + 4f, p.z), Vector3.down, out var h, 12f, groundMask, QueryTriggerInteraction.Ignore)) { g = h.point; return true; }
            if (t) { g = new Vector3(p.x, top, p.z); return true; }
            return false;
        }

        /// <summary>p lies on some road's carriageway (its half width and a little more, any road of the network).</summary>
        bool OnAnyRoad(Vector3 p)
        {
            foreach (var r in net.roads)
            {
                if (r?.pts == null) continue;
                float lim = r.width * 0.5f + 0.6f, lim2 = lim * lim;
                for (int i = 0; i + 1 < r.pts.Length; i++)
                {
                    Vector3 a = r.pts[i], b = r.pts[i + 1];
                    Vector2 ab = new Vector2(b.x - a.x, b.z - a.z), ap = new Vector2(p.x - a.x, p.z - a.z);
                    float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
                    if ((ap - ab * t).sqrMagnitude < lim2) return true;
                }
            }
            return false;
        }

        static bool Free(Vector3 g)
        {
            int n = Physics.OverlapCapsuleNonAlloc(g + Vector3.up * 0.6f, g + Vector3.up * 3.4f, 0.35f, block, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!(block[i] is TerrainCollider)) return false;
            return true;
        }

        /// <summary>A post on the drivers' left at each arm's stop line, its lamps facing them, and the stop line itself.</summary>
        void Dress(Junction j)
        {
            Materials();
            var root = new GameObject("Signal " + Mathf.RoundToInt(j.centre.x) + "," + Mathf.RoundToInt(j.centre.z)).transform;
            root.SetParent(transform, false);
            foreach (var a in j.arms)
            {
                Vector3 left = Vector3.Cross(a.inDir, Vector3.up);   // forward x up = left
                Vector3 spot = Vector3.zero;
                bool ok = false;
                // the kerb on the left, a little back if something stands there; else the right-hand kerb
                // (owner, 23 Nov 2026: "why traffic signals are on road? vehicles are hitting with it"): never on any road's
                // carriageway - the other arms meet here too - so a little further back / out until clear of all of them
                foreach (float side in new[] { 1f, -1f })
                {
                    for (int k = 0; k < 8 && !ok; k++)
                        foreach (float extra in new[] { 0.8f, 1.6f, 2.6f })
                        {
                            Vector3 p = a.stop - a.inDir * (k * 1.5f) + left * side * (a.width * 0.5f + extra);
                            if (!OnAnyRoad(p) && Ground(p, out var g) && Free(g)) { spot = g; ok = true; break; }
                        }
                    if (ok) break;
                }
                if (!ok) continue;
                var post = new GameObject("Post " + (string.IsNullOrEmpty(a.name) ? "#" + a.road : a.name)).transform;
                post.SetParent(root, false);
                post.SetPositionAndRotation(spot, Quaternion.LookRotation(-a.inDir));   // the lamps face the drivers coming in
                post.gameObject.layer = 0;
                var pole = Part(PrimitiveType.Cylinder, post, new Vector3(0f, 1.75f, 0f), new Vector3(0.14f, 1.75f, 0.14f), matPole, true);
                Part(PrimitiveType.Cylinder, post, new Vector3(0f, 0.45f, 0f), new Vector3(0.16f, 0.45f, 0.16f), matBand);
                Part(PrimitiveType.Cube, post, new Vector3(0f, 3.05f, 0.02f), new Vector3(0.48f, 1.16f, 0.03f), matPlate);   // the yellow border board
                Part(PrimitiveType.Cube, post, new Vector3(0f, 3.05f, 0.14f), new Vector3(0.34f, 1.0f, 0.2f), matHead);      // the housing
                Renderer Lamp(float y, int k)
                {
                    var l = Part(PrimitiveType.Sphere, post, new Vector3(0f, y, 0.25f), new Vector3(0.22f, 0.22f, 0.05f), lampOff[k]);
                    Part(PrimitiveType.Cube, post, new Vector3(0f, y + 0.13f, 0.31f), new Vector3(0.28f, 0.02f, 0.14f), matHead);   // its visor
                    return l.GetComponent<Renderer>();
                }
                a.red = Lamp(3.37f, 0);
                a.amber = Lamp(3.05f, 1);
                a.green = Lamp(2.73f, 2);
                pole.isStatic = false;
                StopLine(root, a);
            }
        }

        /// <summary>The white stop line across the lane coming in (the left half of a two-way road), laid on the ground.</summary>
        void StopLine(Transform root, Arm a)
        {
            Vector3 left = Vector3.Cross(a.inDir, Vector3.up);
            bool twoWay = net.roads[a.road].oneWay == 0;
            float from = twoWay ? 0f : -a.width * 0.5f, to = a.width * 0.5f - 0.3f;
            const int seg = 6;
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float s = Mathf.Lerp(from, to, i / (float)seg);
                Vector3 p = a.stop + left * s;
                if (!Ground(p, out var g)) g = p;
                v.Add(g - a.inDir * 0.2f + Vector3.up * 0.03f);
                v.Add(g + a.inDir * 0.2f + Vector3.up * 0.03f);
                if (i > 0) { int b = v.Count - 4; tri.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 }); }
            }
            var mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var g2 = new GameObject("Stop line");
            g2.transform.SetParent(root, false);
            g2.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g2.AddComponent<MeshRenderer>();
            r.sharedMaterial = matLine;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

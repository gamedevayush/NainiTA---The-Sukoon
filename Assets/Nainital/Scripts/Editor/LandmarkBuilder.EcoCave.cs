using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Eco Cave Garden (user, 2026-11-18: "nainital eco cave park bnna hai theek se ... iske andr ka map dhundho, bahut
    /// badi cave hain, 4-5 hain", with photos and the park's guide map). The guide map was fitted to the satellite
    /// (Tools: scratchpad ecocave/eco_layout.jpg): park frame u along Kaladhungi road to the north-east, v up the slope
    /// (south-east), origin at the gate. Six walk-through caves - Apes, Flying Fox, Bats, Porcupine, Tiger, Panther - are
    /// winding rock tunnels on the upper ridge, entered from the paths below and left onto the ridge path. Stone steps
    /// with green chain railings climb from the gate to a junction; flagstone paths with rusty red railings join the
    /// fountain, three summer houses, the cafeteria, photography kiosk, manager's hut and the bull ride; bamboo stands
    /// among the oaks; a Kumaoni villager statue by the junction.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        static readonly Vector2 EcO = new Vector2(-937f, 182f), EcU = new Vector2(0.817f, 0.577f), EcV = new Vector2(0.577f, -0.817f);
        static Vector2 Ec(float u, float v) => EcO + EcU * u + EcV * v;
        static float EcYaw(float du, float dv) { Vector2 d = EcU * du + EcV * dv; return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }

        // the walks, (u, v) pairs; checked against the caves so none runs through a cave's rock (check_plan.py, 19 Nov)
        static readonly float[][] EcPaths =
        {
            new[] { 0f, 1f, 0f, 10f, 2f, 18f },                                            // 0: the steps up from the gate
            new[] { 2f, 18f, 16f, 22f, 22.8f, 20.2f },                                       // 1: to the fountain
            new[] { 2f, 18f, -6f, 30f, -13f, 39f, -16f, 43.5f },                            // 2: to Tiger Cave
            new[] { 2f, 18f, 8f, 34f, 7f, 47f },                                           // 3: past the bull ride to Porcupine Cave
            new[] { 0f, 6f, 18f, 8f, 34f, 12f, 48f, 14f, 60f, 12f },                       // 4: the low walk north-east
            new[] { 60f, 12f, 66f, 30f, 65f, 46f, 60f, 61f },                              // 5: up the north-east side
            new[] { 60f, 61f, 40f, 62.5f, 22f, 62.5f, 6f, 61f, -10f, 60.5f, -22f, 60.5f, -31f, 58f, -31f, 53.5f },   // 6: the ridge
            new[] { 0f, 6f, -18f, 7.5f, -40f, 8f },                                         // 7: the low walk south-west
            new[] { 31.4f, 25.2f, 32f, 34f, 26f, 40f, 26f, 47f },                             // 8: photography, manager, Bats Cave
            new[] { 32f, 34f, 41f, 40f, 43f, 43.5f },                                      // 9: to Flying Fox Cave
            new[] { -38f, 37f, -43f, 24f, -40f, 8f },                                      // 10: down from Panther Cave
        };

        static readonly (string name, float au, float av, float bu, float bv, float bend)[] EcCaves =
        {
            ("APES CAVE", 62f, 40f, 54f, 56f, -2.2f),
            ("FLYING FOX CAVE", 44f, 46f, 36f, 58f, 2.0f),
            ("BATS CAVE", 26f, 50f, 14f, 58f, -2.0f),
            ("PORCUPINE CAVE", 8f, 52f, -4f, 57f, 1.8f),
            ("TIGER CAVE", -18f, 46f, -26f, 56f, -2.0f),
            ("PANTHER CAVE", -30f, 50f, -38f, 40f, 2.2f),
        };

        // where the park's buildings stand (u, v, radius kept clear)
        static readonly (string what, float u, float v, float r)[] EcSpots =
        {
            ("gate", 0f, -1f, 4f), ("ticket", 5.6f, 0.9f, 2f), ("toilets", -7.5f, 10.5f, 2.6f), ("generator", -17f, 3.5f, 2f),
            ("summer", -40f, 12.5f, 3f), ("summer", 18f, 3.5f, 3f), ("summer", 47f, 18.5f, 3f), ("cafeteria", 62.5f, 5f, 5.5f),
            ("fountain", 29f, 19f, 6.2f), ("photo", 37f, 26f, 2.2f), ("manager", 22f, 33f, 2.2f), ("bull", 13.5f, 31.5f, 3f),
            ("statue", -3.6f, 14.5f, 1.4f), ("bamboohut", 43f, 31f, 2.4f), ("junction", 2f, 18f, 2.5f),
            ("bamboo", -26f, 22f, 3f), ("bamboo", 14f, 44f, 3f), ("bamboo", 51f, 29f, 3f), ("bamboo", -4f, 44f, 3f), ("bamboo", 33f, 46f, 2.5f),
        };

        /// <summary>The park's walks, caves and buildings as circles, for the planners to keep houses and trees off them.</summary>
        internal static IEnumerable<(Vector2 c, float r)> EcoCaveSpots()
        {
            foreach (var path in EcPaths)
                for (int i = 0; i + 3 < path.Length; i += 2)
                {
                    Vector2 a = Ec(path[i], path[i + 1]), b = Ec(path[i + 2], path[i + 3]);
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 2.5f));
                    for (int k = 0; k <= n; k++) yield return (Vector2.Lerp(a, b, k / (float)n), 2.0f);
                }
            foreach (var c in EcCaves)
            {
                Vector2 a = Ec(c.au, c.av), b = Ec(c.bu, c.bv);
                for (int k = 0; k <= 8; k++) yield return (Vector2.Lerp(a, b, k / 8f), 4.2f);
            }
            foreach (var s in EcSpots) if (s.what != "bamboo") yield return (Ec(s.u, s.v), s.r + 1f);
            // the houses the building data put inside the park (its own huts stand in their places)
            foreach (var (u, v) in new[] { (22f, 17f), (22f, 0f), (62f, 4f), (67f, 13f) }) yield return (Ec(u, v), 3f);
        }

        // ------------------------------------------------------------------ caves
        sealed class EcCaveRun
        {
            public string name; public int seed;
            public readonly List<Vector2> p = new List<Vector2>(), t = new List<Vector2>();
            public readonly List<float> s = new List<float>(), floor = new List<float>(), w = new List<float>(), h = new List<float>(),
                                        W = new List<float>(), H = new List<float>();
            public float Len => s[s.Count - 1];
            public Vector2 Out(bool end) => end ? t[t.Count - 1] : -t[0];
        }

        static Vector2 CatRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) =>
            0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);

        static EcCaveRun EcCaveLayout(WorldCtx ctx, string name, Vector2 a, Vector2 b, float bend, int seed)
        {
            var run = new EcCaveRun { name = name, seed = seed };
            Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized;
            Vector2 m1 = a + d * 0.35f + n * bend, m2 = a + d * 0.7f - n * (bend * 0.7f);
            var ctrl = new[] { a * 2f - m1, a, m1, m2, b, b * 2f - m2 };
            var dense = new List<Vector2>();
            for (int k = 0; k < 3; k++)
                for (int j = 0; j < 40; j++) dense.Add(CatRom(ctrl[k], ctrl[k + 1], ctrl[k + 2], ctrl[k + 3], j / 40f));
            dense.Add(b);
            var acc = new List<float> { 0f };
            for (int i = 1; i < dense.Count; i++) acc.Add(acc[i - 1] + Vector2.Distance(dense[i - 1], dense[i]));
            float total = acc[acc.Count - 1];
            int count = Mathf.Max(8, Mathf.CeilToInt(total / 0.35f)), seg = 0;
            for (int i = 0; i <= count; i++)
            {
                float target = total * i / count;
                while (seg < acc.Count - 2 && acc[seg + 1] < target) seg++;
                run.p.Add(Vector2.Lerp(dense[seg], dense[seg + 1], Mathf.InverseLerp(acc[seg], acc[seg + 1], target)));
                run.s.Add(target);
            }
            for (int i = 0; i < run.p.Count; i++)
                run.t.Add((run.p[Mathf.Min(i + 1, run.p.Count - 1)] - run.p[Mathf.Max(i - 1, 0)]).normalized);
            // the floor follows the ground along the way, smoothed over ~5 m, pinned to the ground at both mouths
            var g = new List<float>();
            foreach (var q in run.p) g.Add(ctx.H(q));
            for (int i = 0; i < run.p.Count; i++)
            {
                float sum = 0f;
                for (int k = -7; k <= 7; k++) sum += g[Mathf.Clamp(i + k, 0, g.Count - 1)];
                run.floor.Add(sum / 15f + 0.04f);
            }
            float e0 = g[0] + 0.04f - run.floor[0], e1 = g[g.Count - 1] + 0.04f - run.floor[run.floor.Count - 1];
            for (int i = 0; i < run.floor.Count; i++)
            {
                float t = run.s[i] / total;
                run.floor[i] += e0 * (1f - t) + e1 * t;
                // the passage is ~1.7 m wide and ~2.3 m high, wider and higher in places; the rock round it 1.2-2.4 m thick
                float s = run.s[i];
                run.w.Add(0.84f + 0.32f * (Mathf.PerlinNoise(s * 0.3f + seed * 3.1f, 0.5f) - 0.5f));
                run.h.Add(2.3f + 0.5f * (Mathf.PerlinNoise(s * 0.25f + seed * 1.7f, 3.1f) - 0.5f));
                run.W.Add(run.w[i] + 1.6f + 1.2f * (Mathf.PerlinNoise(s * 0.2f + seed, 7.3f) - 0.5f));
                run.H.Add(run.h[i] + 1.2f + 1.4f * (Mathf.PerlinNoise(s * 0.17f + seed * 0.6f, 11.9f) - 0.5f));
            }
            return run;
        }

        /// <summary>Adds a grid of shared vertices (rows i, columns j) with smooth normals turned towards `want`, uvs from
        /// the row distance (sAlong) and the distance round each row.</summary>
        static void Grid(MeshBuilder mb, Vector3[,] P, Vector3[,] want, float[] sAlong, float tile)
        {
            int N = P.GetLength(0), K = P.GetLength(1), b = mb.V.Count;
            for (int i = 0; i < N; i++)
            {
                float arc = 0f;
                for (int j = 0; j < K; j++)
                {
                    if (j > 0) arc += Vector3.Distance(P[i, j - 1], P[i, j]);
                    Vector3 du = P[Mathf.Min(i + 1, N - 1), j] - P[Mathf.Max(i - 1, 0), j];
                    Vector3 dv = P[i, Mathf.Min(j + 1, K - 1)] - P[i, Mathf.Max(j - 1, 0)];
                    Vector3 nrm = Vector3.Cross(du, dv);
                    if (nrm.sqrMagnitude < 1e-10f) nrm = want[i, j];
                    nrm.Normalize();
                    if (Vector3.Dot(nrm, want[i, j]) < 0f) nrm = -nrm;
                    mb.V.Add(P[i, j]); mb.N.Add(nrm); mb.UV.Add(new Vector2(sAlong[i] / tile, arc / tile));
                }
            }
            for (int i = 0; i + 1 < N; i++)
                for (int j = 0; j + 1 < K; j++)
                {
                    int a = b + i * K + j, c = b + (i + 1) * K + j, d = b + (i + 1) * K + j + 1, e = b + i * K + j + 1;
                    Vector3 w = want[i, j] + want[i + 1, j + 1];
                    GridTri(mb, a, c, d, w);
                    GridTri(mb, a, d, e, w);
                }
        }

        static void GridTri(MeshBuilder mb, int a, int b, int c, Vector3 want)
        {
            Vector3 n = Vector3.Cross(mb.V[b] - mb.V[a], mb.V[c] - mb.V[a]);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, want) < 0f) { int t = b; b = c; c = t; }
            mb.T.Add(a); mb.T.Add(b); mb.T.Add(c);
        }

        /// <summary>A weathered rock lump (smooth, noisy), its open bottom sunk below `c`.</summary>
        static void RockLump(MeshBuilder mb, Vector3 c, Vector3 radii, int seed, float tile = 2.2f)
        {
            const int seg = 12, rings = 7;
            var P = new Vector3[rings + 1, seg + 1];
            var want = new Vector3[rings + 1, seg + 1];
            var sAlong = new float[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float lat = Mathf.Lerp(-0.4f, 1f, i / (float)rings) * Mathf.PI * 0.5f;
                sAlong[i] = lat * radii.y;
                for (int j = 0; j <= seg; j++)
                {
                    float lon = j / (float)seg * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    float n = 1f + 0.34f * (Mathf.PerlinNoise(seed * 0.37f + d.x * 1.6f + 3f, d.z * 1.6f + d.y * 1.2f + 5f) - 0.5f)
                                 + 0.14f * (Mathf.PerlinNoise(d.x * 4f + seed, d.y * 4f + d.z * 2.5f) - 0.5f);
                    P[i, j] = c + Vector3.Scale(d * n, radii);
                    want[i, j] = d;
                }
            }
            Grid(mb, P, want, sAlong, tile);
        }

        /// <summary>
        /// One cave: an inner passage (rock, darker: little light gets in), a flat floor, and an outer rock mass whose
        /// skirt runs down into the ground; at each mouth the rock thins to a lip round the opening, with boulders either
        /// side. Two bulbs hang from the roof, as in the real caves.
        /// </summary>
        static void EcCaveMesh(WorldCtx ctx, SiteMeshes sm, EcCaveRun run, System.Random rng, Transform parent)
        {
            var inner = sm.Photo(GreyRock(), new Color(0.72f, 0.71f, 0.69f), 0.12f);
            var outer = sm.Photo(GreyRock(), new Color(0.8f, 0.8f, 0.78f), 0.1f);
            var floorMb = sm.Photo("rock_ground_02", new Color(0.50f, 0.47f, 0.43f), 0.1f);
            int N = run.p.Count, K = 13;
            float len = run.Len;
            var Pin = new Vector3[N, K]; var Win = new Vector3[N, K];
            var Pout = new Vector3[N, K + 2]; var Wout = new Vector3[N, K + 2];
            var Pfl = new Vector3[N, 3]; var Wfl = new Vector3[N, 3];
            var sArr = new float[N];
            var wUsed = new float[N];
            for (int i = 0; i < N; i++)
            {
                Vector2 c = run.p[i], r2 = new Vector2(run.t[i].y, -run.t[i].x);
                Vector3 R = new Vector3(r2.x, 0f, r2.y), up = Vector3.up;
                float s = run.s[i], fl = run.floor[i], mouth = Mathf.Min(s, len - s);
                float flare = 1f - Mathf.Clamp01(mouth / 1.2f);
                float w = run.w[i] + 0.22f * flare, h = run.h[i] + 0.25f * flare;
                wUsed[i] = w; sArr[i] = s;
                Vector3 axis = V3(c, fl);
                for (int j = 0; j < K; j++)
                {
                    float th = Mathf.PI * j / (K - 1), cs = Mathf.Cos(th), sn = Mathf.Max(0f, Mathf.Sin(th));
                    float x = w * Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), 0.7f), y = h * Mathf.Pow(sn, 0.8f);
                    float disp = j == 0 || j == K - 1 ? 0f
                        : 0.24f * (Mathf.PerlinNoise(s * 0.9f + run.seed, j * 0.55f) - 0.4f) + 0.09f * (Mathf.PerlinNoise(s * 2.4f, j * 1.3f + run.seed) - 0.5f);
                    Vector3 dir = R * cs + up * sn;
                    Pin[i, j] = axis + R * x + up * y + dir * disp;
                    Win[i, j] = -dir;
                }
                for (int j = 0; j < 3; j++) { Pfl[i, j] = axis + R * (w * (1 - j)); Wfl[i, j] = up; }
                float k = Mathf.SmoothStep(0f, 1f, mouth / 1.8f);
                float Wd = Mathf.Lerp(w + 0.35f, run.W[i], k), Hd = Mathf.Lerp(h + 0.3f, run.H[i], k);
                for (int j = 0; j < K + 2; j++)
                {
                    if (j == 0 || j == K + 1)
                    {
                        float sx = j == 0 ? 1f : -1f;
                        Vector3 foot = axis + R * (sx * Wd * 1.04f);
                        float y = Mathf.Min(-1.9f, ctx.H(XZ(foot)) - fl - 0.5f);
                        Pout[i, j] = foot + up * y;
                        Wout[i, j] = R * sx;
                        continue;
                    }
                    float th = Mathf.PI * (j - 1) / (K - 1), cs = Mathf.Cos(th), sn = Mathf.Max(0f, Mathf.Sin(th));
                    float x = Wd * Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), 0.85f), yy = Hd * Mathf.Pow(sn, 0.9f);
                    Vector3 dir = R * cs + up * sn;
                    // big knobbly lumps: a pile of boulders, not a smooth dome
                    float disp = (0.3f + 0.7f * k) * (1.1f * (Mathf.PerlinNoise(s * 0.45f + run.seed * 2f, j * 0.4f) - 0.5f)
                                                      + 0.45f * (Mathf.PerlinNoise(s * 1.5f, j * 1.1f + run.seed) - 0.5f)
                                                      + 0.15f * (Mathf.PerlinNoise(s * 3.7f + 9f, j * 2.3f + run.seed) - 0.5f));
                    Pout[i, j] = axis + R * x + up * yy + dir * disp;
                    Wout[i, j] = dir;
                }
            }
            Grid(inner, Pin, Win, sArr, 1.8f);
            Grid(outer, Pout, Wout, sArr, 2.6f);
            Grid(floorMb, Pfl, Wfl, sArr, 1.6f);
            // the lips round both openings
            foreach (bool end in new[] { false, true })
            {
                int i = end ? N - 1 : 0;
                Vector3 o = V3(run.Out(end), 0f);
                Vector2 uvT(Vector3 p) => new Vector2((p.x + p.z) / 2.6f, p.y / 2.6f);
                void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
                {
                    outer.Tri(a, b, c, uvT(a), uvT(b), uvT(c), o);
                    outer.Tri(a, c, d, uvT(a), uvT(c), uvT(d), o);
                }
                for (int j = 0; j + 1 < K; j++) Q(Pin[i, j], Pin[i, j + 1], Pout[i, j + 2], Pout[i, j + 1]);
                Vector2 r2 = new Vector2(run.t[i].y, -run.t[i].x);
                Vector3 R = new Vector3(r2.x, 0f, r2.y);
                Vector3 bR = V3(run.p[i], Pout[i, 0].y) + R * wUsed[i], bL = V3(run.p[i], Pout[i, K + 1].y) - R * wUsed[i];
                Q(Pin[i, 0], Pout[i, 1], Pout[i, 0], bR);
                Q(Pin[i, K - 1], Pout[i, K], Pout[i, K + 1], bL);
                // boulders either side of the mouth, half in the rock
                Vector3 m = V3(run.p[i], run.floor[i]);
                foreach (float sx in new[] { 1f, -1f })
                {
                    float rr = 0.7f + (float)rng.NextDouble() * 0.6f;
                    Vector3 at = m + R * sx * (wUsed[i] + 0.5f + rr * 1.3f) + o * (0.2f + (float)rng.NextDouble() * 0.6f) + Vector3.down * 0.3f;   // clear of the opening
                    RockLump(outer, at, new Vector3(rr * 1.1f, rr * 0.9f, rr), rng.Next(1000));
                }
            }
            // boulders on the back of the rock and round its foot, so it reads as a pile of boulders, not a tube
            for (int k = 0; k < 8; k++)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(N * (0.12f + 0.105f * k)), 0, N - 1);
                Vector2 r2 = new Vector2(run.t[i].y, -run.t[i].x);
                float side = (k % 2 == 0 ? 1f : -1f) * (0.3f + (float)rng.NextDouble() * 1.2f);
                float rr = 1.1f + (float)rng.NextDouble() * 0.9f;
                // high enough that the boulder's underside stays above the passage's roof (they hung into it, 19 Nov)
                Vector3 at = V3(run.p[i] + r2 * side, run.floor[i] + run.h[i] + 0.3f + rr * 0.62f + (float)rng.NextDouble() * 0.4f);
                RockLump(outer, at, new Vector3(rr * 1.3f, rr * 0.8f, rr * 1.1f), rng.Next(1000));
            }
            for (int k = 0; k < 4; k++)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(N * (0.25f + 0.17f * k)), 0, N - 1);
                Vector2 r2 = new Vector2(run.t[i].y, -run.t[i].x);
                float sx = k % 2 == 0 ? 1f : -1f, rr = 0.9f + (float)rng.NextDouble() * 0.7f;
                Vector2 foot = run.p[i] + r2 * sx * (run.W[i] + 0.3f);
                RockLump(outer, V3(foot, Mathf.Min(ctx.H(foot), run.floor[i]) + 0.1f), new Vector3(rr * 1.2f, rr * 0.85f, rr), rng.Next(1000));
            }
            // two bulbs on wires from the roof, and the light they give (the sun does not get in)
            foreach (float f in new[] { 0.33f, 0.7f })
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((N - 1) * f), 0, N - 1);
                Vector3 top = V3(run.p[i], run.floor[i] + run.h[i] - 0.05f);
                sm.loose.Limb(top + Vector3.up * 0.25f, top + Vector3.down * 0.2f, 0.008f, 0.008f, 3, S(Pal.IronBlack), false);
                sm.loose.Sphere(top + Vector3.down * 0.28f, Vector3.one * 0.07f, 6, 4, S(Pal.LampBulb));
                var lg = new GameObject("Cave bulb " + run.name);
                lg.transform.SetParent(parent, false);
                lg.transform.position = top + Vector3.down * 0.4f;
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 5.5f;
                l.intensity = 1.6f;
                l.color = new Color(1f, 0.84f, 0.6f);
                l.shadows = LightShadows.None;
            }
        }

        // ------------------------------------------------------------------ the park
        static void BuildEcoCaveGarden(WorldCtx ctx, Transform parent)
        {
            var rng = new System.Random(1911);
            var sm = new SiteMeshes();
            var pave = sm.Photo("cobblestone_floor_08", new Color(0.86f, 0.84f, 0.8f), 0.3f);
            var stone = sm.Photo("old_stone_wall", new Color(0.8f, 0.78f, 0.74f));
            var concrete = sm.Photo("concrete_floor_worn_001", new Color(0.82f, 0.82f, 0.8f));

            // 1. the caves, from the ground as it is
            var caves = new List<EcCaveRun>();
            for (int k = 0; k < EcCaves.Length; k++)
            {
                var c = EcCaves[k];
                caves.Add(EcCaveLayout(ctx, c.name, Ec(c.au, c.av), Ec(c.bu, c.bv), c.bend, 17 + k * 29));
            }

            // 2. the walks: nodes every ~2.5 m at the smoothed ground; shared ends get one level; spurs to the cave mouths
            var walks = new List<List<WalkNode>>();
            var widths = new List<float>();
            var ends = new Dictionary<Vector2Int, float>();
            float gateY = ctx.H(Ec(0f, 1f)) + 0.12f;
            ends[Vector2Int.RoundToInt(Ec(0f, 1f) * 2f)] = gateY;
            float Level(Vector2 p, Vector2 dir)
            {
                Vector2 side = new Vector2(dir.y, -dir.x) * 0.7f, along = dir * 1.5f;
                return (ctx.H(p) * 2f + ctx.H(p + side) + ctx.H(p - side) + ctx.H(p + along) + ctx.H(p - along)) / 6f + 0.12f;
            }
            List<WalkNode> MakeWalk(List<Vector2> line, float? startY, float? endY)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(line[i], line[i + 1]) / 2.5f));
                    for (int k = 0; k < n; k++) pts.Add(Vector2.Lerp(line[i], line[i + 1], k / (float)n));
                }
                pts.Add(line[line.Count - 1]);
                var ys = new float[pts.Count];
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 dir = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                    ys[i] = Level(pts[i], dir);
                }
                var sm2 = (float[])ys.Clone();
                for (int i = 1; i + 1 < ys.Length; i++) sm2[i] = (ys[i - 1] + ys[i] * 2f + ys[i + 1]) * 0.25f;
                var k0 = Vector2Int.RoundToInt(pts[0] * 2f);
                var k1 = Vector2Int.RoundToInt(pts[pts.Count - 1] * 2f);
                if (startY.HasValue) sm2[0] = startY.Value; else if (ends.TryGetValue(k0, out float y0)) sm2[0] = y0; else ends[k0] = sm2[0];
                if (endY.HasValue) sm2[sm2.Length - 1] = endY.Value; else if (ends.TryGetValue(k1, out float y1)) sm2[sm2.Length - 1] = y1; else ends[k1] = sm2[sm2.Length - 1];
                var nodes = new List<WalkNode>();
                for (int i = 0; i < pts.Count; i++)
                {
                    bool steps = i + 1 < pts.Count && Mathf.Abs(sm2[i + 1] - sm2[i]) / Mathf.Max(0.1f, Vector2.Distance(pts[i], pts[i + 1])) > 0.12f;
                    nodes.Add(new WalkNode(pts[i], sm2[i], steps));
                }
                return nodes;
            }
            foreach (var path in EcPaths)
            {
                var line = new List<Vector2>();
                for (int i = 0; i + 1 < path.Length; i += 2) line.Add(Ec(path[i], path[i + 1]));
                walks.Add(MakeWalk(line, null, null));
                widths.Add(walks.Count == 1 ? 2.6f : 1.8f);
            }
            // spurs: from the nearest walk to each cave mouth, ending level with the cave floor
            foreach (var run in caves)
                foreach (bool end in new[] { false, true })
                {
                    int i = end ? run.p.Count - 1 : 0;
                    Vector2 mouth = run.p[i], front = mouth + run.Out(end) * 1.2f;
                    float best = float.MaxValue; Vector2 bp = front; float by = 0f;
                    foreach (var wk in walks)
                        for (int j = 0; j + 1 < wk.Count; j++)
                        {
                            Vector2 a = wk[j].p, ab = wk[j + 1].p - a;
                            float t = Mathf.Clamp01(Vector2.Dot(front - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                            float d = Vector2.Distance(front, a + ab * t);
                            if (d < best) { best = d; bp = a + ab * t; by = Mathf.Lerp(wk[j].y, wk[j + 1].y, t); }
                        }
                    var line = new List<Vector2>();
                    if (best > 0.3f) line.Add(bp);
                    line.Add(front);
                    line.Add(mouth);
                    walks.Add(MakeWalk(line, best > 0.3f ? by : (float?)null, run.floor[i]));
                    widths.Add(1.5f);
                }

            // 3. the ground: no bump of earth through a walk or into a cave
            Vector2 bmin = new Vector2(float.MaxValue, float.MaxValue), bmax = new Vector2(float.MinValue, float.MinValue);
            foreach (var (u, v) in new[] { (-52f, -4f), (76f, -4f), (76f, 70f), (-52f, 70f) })
            {
                bmin = Vector2.Min(bmin, Ec(u, v)); bmax = Vector2.Max(bmax, Ec(u, v));
            }
            var lines = new List<Vector2[]>();
            foreach (var wk in walks) { var l = new Vector2[wk.Count]; for (int i = 0; i < wk.Count; i++) l[i] = wk[i].p; lines.Add(l); }
            ShapeTerrain(ctx, bmin, bmax, (p, y) =>
            {
                float ny = y;
                for (int k = 0; k < walks.Count; k++)
                {
                    var l = lines[k];
                    Project(l, p, out _, out _, out int seg, out float d);
                    if (d > widths[k] * 0.5f + 0.9f) continue;
                    Vector2 a = l[seg], ab = l[seg + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    float top = Mathf.Lerp(walks[k][seg].y, walks[k][seg + 1].y, t);
                    if (walks[k][seg].steps) top = Mathf.Min(walks[k][seg].y, walks[k][seg + 1].y);
                    ny = Mathf.Min(ny, top - 0.1f);
                }
                foreach (var run in caves)
                {
                    Project(run.p, p, out float s, out float side, out int seg, out float d);
                    if (d > 2.2f || s <= 0.01f && Vector2.Dot(p - run.p[0], run.t[0]) < -0.8f || s >= run.Len - 0.01f && Vector2.Dot(p - run.p[run.p.Count - 1], run.t[run.t.Count - 1]) > 0.8f) continue;
                    int i = Mathf.Clamp(seg, 0, run.floor.Count - 1);
                    ny = Mathf.Min(ny, run.floor[i] - 0.35f);
                }
                return ny;
            });
            PaintTerrain(ctx, bmin, bmax, (p, slope) =>
            {
                foreach (var l in lines) { Project(l, p, out _, out _, out _, out float d); if (d < 1.8f) return new Vector4(0.1f, 0.2f, 0.1f, 0.6f); }
                return null;
            });

            // 4. the walks themselves, their railings
            for (int k = 0; k < walks.Count; k++)
            {
                Walkway(ctx, sm, walks[k], widths[k], pave, stone);
                if (k == 0)
                {
                    foreach (float off in new[] { 1.45f, -1.45f }) ChainRail(sm, WalkSide(walks[k], off, 2f, 1), Pal.IronGreen, Pal.IronBlack);
                    continue;
                }
                if (k >= EcPaths.Length) continue;   // spurs: open
                // a rusty red pipe railing on the downhill side, broken where another walk joins
                var wk = walks[k];
                Vector2 mid = wk[wk.Count / 2].p, dir = (wk[Mathf.Min(wk.Count / 2 + 1, wk.Count - 1)].p - wk[Mathf.Max(wk.Count / 2 - 1, 0)].p).normalized;
                Vector2 rgt = new Vector2(dir.y, -dir.x);
                float sgn = ctx.H(mid + rgt * 2f) < ctx.H(mid - rgt * 2f) ? 1f : -1f;
                var pts = WalkSide(wk, sgn * (widths[k] * 0.5f + 0.1f), 2f);
                var runPts = new List<Vector3>();
                foreach (var q in pts)
                {
                    bool near = Vector2.Distance(XZ(q), wk[0].p) < 2.2f || Vector2.Distance(XZ(q), wk[wk.Count - 1].p) < 2.2f;
                    for (int o = 0; o < walks.Count && !near; o++)
                    {
                        if (o == k) continue;
                        Project(lines[o], XZ(q), out _, out _, out _, out float d);
                        if (d < widths[o] * 0.5f + 1.3f) near = true;
                    }
                    if (near) { if (runPts.Count > 1) PipeRail(sm, runPts, Pal.TempleRed, 0.95f); runPts.Clear(); continue; }
                    runPts.Add(q);
                }
                if (runPts.Count > 1) PipeRail(sm, runPts, Pal.TempleRed, 0.95f);
            }

            // 5. the caves
            foreach (var run in caves) EcCaveMesh(ctx, sm, run, rng, parent);
            foreach (var run in caves)
                foreach (bool end in new[] { false, true })
                {
                    int i = end ? run.p.Count - 1 : 0;
                    Vector2 o = run.Out(end), r2 = new Vector2(o.y, -o.x);
                    Vector2 at = run.p[i] + o * 1.3f + r2 * (run.w[i] + 2.7f);
                    float gy = ctx.H(at);
                    Board(sm, new Vector3(at.x, gy + 1.55f, at.y), Mathf.Atan2(o.x, o.y) * Mathf.Rad2Deg, 1.7f, 0.75f, Pal.BoatGreen,
                          new[] { run.name, end ? "EXIT" : "ENTRY" }, new[] { 0.2f, 0.13f }, Pal.ShirtWhite, Pal.IronGreen, gy);
                }

            // 6. the gate on Kaladhungi road, the wall along it, the ticket window
            var road = RoadLine(ctx, "Kaldhungi-Nainital Road", Ec(0f, -6f), out float hw);
            float gateYaw = EcYaw(0f, -1f);
            var gq = Quaternion.Euler(0f, gateYaw, 0f);
            Vector2 gate = Ec(0f, -1f);
            float gy0 = ctx.H(gate);
            Slab(pave, stone, Ec(0f, -3.0f), Ec(0f, 1.0f), 6.2f, gateY, gateY, Mathf.Min(GroundMin(ctx, Ec(0f, -3f), Ec(0f, 1f), 6.2f), gateY) - 0.4f);
            foreach (float u in new[] { -2.7f, 2.7f })
                TBox(stone, V3(Ec(u, -1f), gy0 + 1.4f), new Vector3(0.85f, 3.6f, 0.85f), gq, 1.2f);
            // the green iron arch, the name board on it
            Vector3 arcPrev = default;
            for (int k = 0; k <= 12; k++)
            {
                float a = k / 12f * Mathf.PI;
                Vector3 p = V3(gate, gy0) + gq * new Vector3(Mathf.Cos(a) * 2.7f, 3.2f + Mathf.Sin(a) * 1.65f, 0f);
                if (k > 0) sm.atlas.Limb(arcPrev, p, 0.07f, 0.07f, 6, S(Pal.IronGreen), false);
                arcPrev = p;
            }
            Board(sm, V3(gate, gy0 + 3.75f) + gq * new Vector3(0f, 0f, 0.12f), gateYaw, 4.4f, 0.95f, Pal.BoatGreen,
                  new[] { "ECO CAVE PARK", "K.M.V.N. NAINITAL" }, new[] { 0.36f, 0.14f }, Pal.ShirtWhite, Pal.IronGreen, gy0 + 3.2f);
            if (road != null)
            {
                Project(road, Ec(0f, 10f), out _, out float parkSide, out _, out _);
                var wall = new List<Vector2>();
                for (float u = -46f; u <= 70.01f; u += 2f)
                {
                    for (float v = -14f; v < 12f; v += 0.2f)
                    {
                        Vector2 p = Ec(u, v);
                        Project(road, p, out _, out float side, out _, out float d);
                        if (Mathf.Sign(side) == Mathf.Sign(parkSide) && d >= hw + 1.3f) { wall.Add(p); break; }
                    }
                }
                for (int i = 0; i + 1 < wall.Count; i++)
                {
                    Vector2 a = wall[i], b = wall[i + 1], mid = (a + b) * 0.5f;
                    if (Vector2.Distance(mid, gate) < 3.4f) continue;
                    float ya = ctx.H(a) + 0.95f, yb = ctx.H(b) + 0.95f;
                    Slab(concrete, stone, a, b, 0.45f, ya, yb, GroundMin(ctx, a, b, 0.45f) - 0.4f);
                }
            }
            var tq = Ec(5.6f, 0.9f);
            float ty = Hut(ctx, sm, tq, gateYaw, 2.6f, 2.2f, 2.6f, "plastered_wall_04", new Color(0.93f, 0.9f, 0.78f), new Color(0.45f, 0.62f, 0.45f), Pal.IronGreen);
            Board(sm, V3(tq, ty + 2.35f) + gq * new Vector3(0f, 0f, 1.16f), gateYaw, 2.2f, 0.4f, Pal.BoatGreen, new[] { "TICKET COUNTER" }, new[] { 0.17f }, Pal.ShirtWhite, Pal.IronGreen, ty + 2.2f);
            Vector2 price = Ec(8.4f, -0.2f);
            Board(sm, V3(price, ctx.H(price) + 1.5f), gateYaw, 1.6f, 1.0f, Pal.ShirtWhite, new[] { "ENTRY TICKET", "ADULT  RS 100", "CHILD  RS 60" },
                  new[] { 0.15f, 0.15f, 0.15f }, Pal.JacketBlue, Pal.IronGreen, ctx.H(price), Glyph3D.Face.Block, new[] { Pal.JacketRed, Pal.JacketBlue, Pal.JacketBlue });

            // 7. the buildings and the rest, each pushed clear of the walks
            Vector2 Clear(float u, float v, float r)
            {
                Vector2 p = Ec(u, v);
                for (int it = 0; it < 6; it++)
                {
                    float worst = 0f; Vector2 away = Vector2.zero;
                    for (int k = 0; k < walks.Count; k++)
                    {
                        Project(lines[k], p, out _, out float side, out int seg, out float d);
                        float need = r + widths[k] * 0.5f + 0.5f;
                        if (need - d > worst)
                        {
                            worst = need - d;
                            Vector2 ab = lines[k][seg + 1] - lines[k][seg];
                            Vector2 n = new Vector2(ab.y, -ab.x).normalized;
                            away = n * (side >= 0f ? 1f : -1f);
                        }
                    }
                    if (worst <= 0.02f) break;
                    p += away * (worst + 0.05f);
                }
                return p;
            }
            float faceDown = EcYaw(0f, -1f);
            float FaceTo(Vector2 from, Vector2 to) { Vector2 d = to - from; return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }
            Vector2 NearestWalk(Vector2 p)
            {
                float best = float.MaxValue; Vector2 bp = p;
                foreach (var l in lines)
                {
                    Project(l, p, out _, out _, out int seg, out float d);
                    if (d < best)
                    {
                        best = d;
                        Vector2 a = l[seg], ab = l[seg + 1] - a;
                        bp = a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    }
                }
                return bp;
            }
            var plaster = new Color(0.93f, 0.9f, 0.8f);
            var greenRoof = new Color(0.42f, 0.6f, 0.42f);
            foreach (var spot in EcSpots)
            {
                if (spot.what == "gate" || spot.what == "ticket" || spot.what == "junction") continue;
                Vector2 c = spot.what == "fountain" ? Ec(spot.u, spot.v) : Clear(spot.u, spot.v, spot.r);
                float yaw = FaceTo(c, NearestWalk(c));
                var q = Quaternion.Euler(0f, yaw, 0f);
                switch (spot.what)
                {
                    case "toilets":
                    {
                        float y = Hut(ctx, sm, c, yaw, 4f, 3f, 2.7f, "plastered_wall_04", plaster, greenRoof, Pal.JacketBlue);
                        Board(sm, V3(c, y + 2.45f) + q * new Vector3(0f, 0f, 1.56f), yaw, 1.6f, 0.38f, Pal.JacketBlue, new[] { "TOILETS" }, new[] { 0.2f }, Pal.ShirtWhite, Pal.IronBlack, y + 2.3f);
                        break;
                    }
                    case "generator":
                    {
                        float y = Hut(ctx, sm, c, yaw, 3f, 2.4f, 2.4f, "concrete_wall_008", new Color(0.8f, 0.8f, 0.78f), new Color(0.55f, 0.55f, 0.55f), Pal.IronBlack, false);
                        Board(sm, V3(c, y + 2.15f) + q * new Vector3(0f, 0f, 1.26f), yaw, 1.8f, 0.34f, Pal.BoatYellow, new[] { "GENERATOR ROOM" }, new[] { 0.15f }, Pal.IronBlack, Pal.IronBlack, y + 2f);
                        sm.atlas.Cylinder(V3(c, y) + q * new Vector3(1.2f, 0f, 1.6f), 0.3f, 0.3f, 0.9f, 10, S(Pal.JacketRed), true);
                        break;
                    }
                    case "summer": SummerHouse(ctx, sm, c, yaw, pave, stone); break;
                    case "cafeteria":
                    {
                        float y = Hut(ctx, sm, c, yaw, 8f, 5.2f, 3.0f, "plastered_wall_04", new Color(0.96f, 0.9f, 0.76f), greenRoof, Pal.WoodDark, true, 1.4f);
                        Board(sm, V3(c, y + 2.6f) + q * new Vector3(0f, 0f, 2.66f), yaw, 3.2f, 0.55f, Pal.JacketRed, new[] { "CAFETERIA" }, new[] { 0.3f }, Pal.ShirtWhite, Pal.IronBlack, y + 2.4f);
                        // a paved apron with tables under umbrellas
                        Vector2 ap0 = c + XZ(q * Vector3.forward) * 2.7f, ap1 = c + XZ(q * Vector3.forward) * 6.2f;
                        Slab(concrete, stone, ap0, ap1, 8f, y, y, Mathf.Min(GroundMin(ctx, ap0, ap1, 8f), y) - 0.4f);
                        foreach (float x in new[] { -2.6f, 0f, 2.6f })
                        {
                            Vector3 tp = V3(c, y) + q * new Vector3(x, 0f, 4.5f);
                            sm.atlas.Cylinder(tp, 0.06f, 0.06f, 0.72f, 6, S(Pal.IronBlack), true);
                            sm.atlas.Cylinder(tp + Vector3.up * 0.72f, 0.45f, 0.45f, 0.04f, 10, S(Pal.ShirtWhite), true);
                            sm.loose.Cylinder(tp + Vector3.up * 0.76f, 0.025f, 0.025f, 1.6f, 4, S(Pal.IronBlack), true);
                            sm.loose.Cylinder(tp + Vector3.up * 2.1f, 1.2f, 0.05f, 0.4f, 8, S(x == 0f ? Pal.JacketRed : Pal.BoatYellow), true);
                            foreach (float cz in new[] { -0.75f, 0.75f })
                                sm.atlas.Box(tp + q * new Vector3(0f, 0.22f, cz), new Vector3(0.42f, 0.44f, 0.42f), q, S(Pal.JacketRed));
                        }
                        break;
                    }
                    case "fountain": Fountain(ctx, sm, c, pave, stone, concrete, parent); break;
                    case "photo":
                    {
                        float y = Hut(ctx, sm, c, yaw, 3f, 2.4f, 2.5f, "plastered_wall_04", new Color(0.82f, 0.9f, 0.95f), new Color(0.5f, 0.5f, 0.62f), Pal.JacketBlue);
                        Board(sm, V3(c, y + 2.3f) + q * new Vector3(0f, 0f, 1.26f), yaw, 2.6f, 0.5f, Pal.JacketBlue, new[] { "PHOTOGRAPHY", "KUMAONI DRESS" }, new[] { 0.17f, 0.11f }, Pal.ShirtWhite, Pal.IronBlack, y + 2.1f);
                        // the dress-up rack: pichhauda and ghagra in yellow, red and green
                        Vector3 r0 = V3(c, y) + q * new Vector3(-1.4f, 0f, 2.1f), r1 = V3(c, y) + q * new Vector3(1.4f, 0f, 2.1f);
                        sm.atlas.Limb(r0, r0 + Vector3.up * 1.7f, 0.03f, 0.03f, 4, S(Pal.IronBlack));
                        sm.atlas.Limb(r1, r1 + Vector3.up * 1.7f, 0.03f, 0.03f, 4, S(Pal.IronBlack));
                        sm.atlas.Limb(r0 + Vector3.up * 1.7f, r1 + Vector3.up * 1.7f, 0.025f, 0.025f, 4, S(Pal.IronBlack), false);
                        int[] cloth = { Pal.BoatYellow, Pal.JacketRed, Pal.SareeOrange, Pal.BoatGreen, Pal.SareePink, Pal.BoatYellow };
                        for (int k = 0; k < cloth.Length; k++)
                        {
                            Vector3 hang = Vector3.Lerp(r0, r1, (k + 0.5f) / cloth.Length) + Vector3.up * 1.65f;
                            Vector3 rr = q * Vector3.right * 0.2f;
                            sm.loose.QuadDouble(hang - rr + Vector3.down * 1.1f, hang + rr + Vector3.down * 1.1f, hang + rr * 0.7f, hang - rr * 0.7f, new Rect(S(cloth[k]), Vector2.zero), q * Vector3.forward);
                        }
                        break;
                    }
                    case "manager":
                    {
                        float y = Hut(ctx, sm, c, yaw, 3.2f, 3f, 2.6f, "plastered_wall_04", plaster, greenRoof, Pal.WoodDark);
                        Board(sm, V3(c, y + 2.35f) + q * new Vector3(0f, 0f, 1.56f), yaw, 1.8f, 0.4f, Pal.BoatGreen, new[] { "MANAGER" }, new[] { 0.2f }, Pal.ShirtWhite, Pal.IronGreen, y + 2.2f);
                        break;
                    }
                    case "bull": BullRide(ctx, sm, c, yaw, rng); break;
                    case "statue": VillagerStatue(ctx, sm, c, yaw, stone); break;
                    case "bamboohut": BambooHut(ctx, sm, c, yaw); break;
                    case "bamboo": Bamboo(sm.loose, V3(c, ctx.H(c)), spot.r, 16, rng); break;
                }
            }
            // the junction's direction board
            Vector2 jb = Ec(4.6f, 16.2f);
            Board(sm, V3(jb, ctx.H(jb) + 1.7f), faceDown, 1.9f, 1.15f, Pal.BoatGreen, new[] { "WAY TO CAVES", "FOUNTAIN  CAFETERIA", "TIGER  PANTHER CAVE" },
                  new[] { 0.2f, 0.12f, 0.12f }, Pal.ShirtWhite, Pal.IronGreen, ctx.H(jb));

            // the oaks, deodars and pines the park stands among (the hillside fill leaves slopes this gentle bare)
            var extra = new List<float>();
            for (float u = -46f; u <= 70f; u += 6f)
                for (float v = 2f; v <= 68f; v += 6f)
                {
                    if (rng.NextDouble() < 0.2) continue;
                    Vector2 p = Ec(u + (float)(rng.NextDouble() - 0.5) * 4.5f, v + (float)(rng.NextDouble() - 0.5) * 4.5f);
                    bool bad = false;
                    for (int k = 0; k < lines.Count && !bad; k++) { Project(lines[k], p, out _, out _, out _, out float d); bad = d < widths[k] * 0.5f + 3.6f; }
                    foreach (var run in caves) { if (bad) break; Project(run.p, p, out _, out _, out _, out float d); bad = d < 6f; }
                    foreach (var spot in EcSpots) { if (bad) break; bad = Vector2.Distance(p, Ec(spot.u, spot.v)) < spot.r + 3f; }
                    if (bad) continue;
                    double r = rng.NextDouble();
                    extra.Add(p.x); extra.Add(p.y); extra.Add(r < 0.45 ? 2f : r < 0.75 ? 0f : r < 0.92 ? 1f : 3f); extra.Add(0.8f + (float)rng.NextDouble() * 0.45f);
                }
            var trees = new float[ctx.world.trees.Length + extra.Count];
            ctx.world.trees.CopyTo(trees, 0);
            extra.CopyTo(trees, ctx.world.trees.Length);
            ctx.world.trees = trees;

            var root = sm.Emit(ctx, "Eco Cave Garden", parent);
            // the info board, the zone, where the teleport puts you, and the preview camera
            var site = new Site { origin = V3(Ec(-4.4f, 1.6f), ctx.H(Ec(-4.4f, 1.6f))), rot = Quaternion.Euler(0f, faceDown, 0f), depth = 1f };
            Sign(ctx, "Eco Cave Garden", site, Vector3.zero, parent);
            ctx.Zone("Eco Cave Garden", V3(Ec(10f, 32f), ctx.H(Ec(10f, 32f))), 75f);
            ctx.landmarkSites["Eco Cave Garden"] = V3(Ec(0f, 0f), gateY + 0.1f);
            var cam = new GameObject("ViewShot Eco Cave Garden");
            cam.transform.SetParent(parent, false);
            Vector2 cp = Ec(0.6f, -4.8f);
            cam.transform.SetPositionAndRotation(V3(cp, ctx.H(cp) + 1.7f), Quaternion.Euler(0f, EcYaw(0.05f, 1f), 0f));
            Debug.Log($"[Nainital] Eco Cave Garden: {caves.Count} caves, {walks.Count} walks ({EcPaths.Length} paths + spurs), {extra.Count / 4} trees");
        }

        static void SummerHouse(WorldCtx ctx, SiteMeshes sm, Vector2 c, float yaw, MeshBuilder pave, MeshBuilder stone)
        {
            float gMax = float.MinValue, gMin = float.MaxValue;
            for (int k = 0; k < 6; k++)
            {
                float a = (yaw + k * 60f) * Mathf.Deg2Rad;
                float g = ctx.H(c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 2.6f);
                gMax = Mathf.Max(gMax, g); gMin = Mathf.Min(gMin, g);
            }
            float y = gMax + 0.25f;
            for (int k = 0; k < 6; k++)
            {
                float a0 = (yaw + 30f + k * 60f) * Mathf.Deg2Rad, a1 = (yaw + 30f + (k + 1) * 60f) * Mathf.Deg2Rad;
                Vector2 p0 = c + new Vector2(Mathf.Sin(a0), Mathf.Cos(a0)) * 2.7f, p1 = c + new Vector2(Mathf.Sin(a1), Mathf.Cos(a1)) * 2.7f;
                Slab(pave, stone, c, (p0 + p1) * 0.5f, Vector2.Distance(p0, p1), y, y, gMin - 0.4f);
            }
            Vector3 o = V3(c, y);
            Vector2 post = S(Pal.WoodDark), rail = S(Pal.WoodLight);
            for (int k = 0; k < 6; k++)
            {
                float a = (yaw + 30f + k * 60f) * Mathf.Deg2Rad, a1 = (yaw + 30f + (k + 1) * 60f) * Mathf.Deg2Rad;
                Vector3 p = o + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2.35f, p1 = o + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * 2.35f;
                sm.atlas.Cylinder(p, 0.1f, 0.09f, 2.5f, 6, post, true);
                if (k == 5) continue;   // the open side faces the walk
                sm.atlas.Limb(p + Vector3.up * 0.75f, p1 + Vector3.up * 0.75f, 0.05f, 0.05f, 5, rail, false);
                Vector3 bc = Vector3.Lerp(p, p1, 0.5f) - (Vector3.Lerp(p, p1, 0.5f) - o).normalized * 0.3f;
                sm.atlas.Box(bc + Vector3.up * 0.43f, new Vector3(Vector3.Distance(p, p1) - 0.4f, 0.07f, 0.4f), Quaternion.LookRotation(p1 - p) * Quaternion.Euler(0f, 90f, 0f), rail);
            }
            sm.atlas.Cylinder(o + Vector3.up * 2.5f, 3.1f, 0.15f, 1.5f, 6, S(Pal.BoatGreen), true, true, 1f, (90f - yaw - 30f) * Mathf.Deg2Rad);   // corners over the posts
            sm.atlas.Sphere(o + Vector3.up * 4.05f, Vector3.one * 0.14f, 6, 4, S(Pal.TempleRed));
        }

        static Material poolWater;
        static Material PoolWater()
        {
            if (poolWater) return poolWater;
            string path = WorldBuilder.Gen + "/M_SitePoolWater.mat";
            poolWater = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!poolWater) { poolWater = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(poolWater, path); }
            poolWater.SetColor("_BaseColor", new Color(0.16f, 0.42f, 0.40f));
            poolWater.SetFloat("_Smoothness", 0.92f);
            poolWater.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(poolWater);
            return poolWater;
        }

        /// <summary>The fountain: a round flagstone plaza, a stone-rimmed pool with teal water and a tiered spout.</summary>
        static void Fountain(WorldCtx ctx, SiteMeshes sm, Vector2 c, MeshBuilder pave, MeshBuilder stone, MeshBuilder concrete, Transform parent)
        {
            float gMax = float.MinValue, gMin = float.MaxValue;
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f * Mathf.Deg2Rad;
                float g = ctx.H(c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 6f);
                gMax = Mathf.Max(gMax, g); gMin = Mathf.Min(gMin, g);
            }
            float y = (gMax + gMin) * 0.5f + 0.12f;
            const int seg = 16;
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                Vector2 p0 = c + new Vector2(Mathf.Sin(a0), Mathf.Cos(a0)) * 6f, p1 = c + new Vector2(Mathf.Sin(a1), Mathf.Cos(a1)) * 6f;
                Slab(pave, stone, c, (p0 + p1) * 0.5f, Vector2.Distance(p0, p1) * 1.02f, y, y, gMin - 0.5f);
                Vector2 q0 = c + new Vector2(Mathf.Sin(a0), Mathf.Cos(a0)) * 2.8f, q1 = c + new Vector2(Mathf.Sin(a1), Mathf.Cos(a1)) * 2.8f;
                Slab(concrete, stone, q0, q1, 0.4f, y + 0.5f, y + 0.5f, y - 0.1f, 1.2f);   // the rim
            }
            var water = new MeshBuilder();
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                water.Tri(V3(c, y + 0.36f), V3(c + new Vector2(Mathf.Sin(a0), Mathf.Cos(a0)) * 2.7f, y + 0.36f), V3(c + new Vector2(Mathf.Sin(a1), Mathf.Cos(a1)) * 2.7f, y + 0.36f), Vector2.zero, Vector3.up);
            }
            ctx.MeshObject("Eco Cave fountain water", parent, water, PoolWater(), null, false);
            Vector3 o = V3(c, y);
            sm.atlas.Cylinder(o + Vector3.up * 0.3f, 0.5f, 0.4f, 0.7f, 10, S(Pal.Concrete), true);
            sm.atlas.Cylinder(o + Vector3.up * 1.0f, 1.0f, 0.9f, 0.18f, 12, S(Pal.Concrete), true);
            sm.atlas.Cylinder(o + Vector3.up * 1.18f, 0.18f, 0.14f, 0.6f, 8, S(Pal.Concrete), true);
            sm.atlas.Cylinder(o + Vector3.up * 1.78f, 0.55f, 0.5f, 0.12f, 10, S(Pal.Concrete), true);
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                Vector3 lip = o + Vector3.up * 1.15f + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.95f;
                Vector3 land = o + Vector3.up * 0.38f + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 1.6f;
                sm.loose.Limb(lip, Vector3.Lerp(lip, land, 0.5f) + Vector3.up * 0.15f, 0.03f, 0.02f, 4, S(Pal.ShirtWhite), false);
                sm.loose.Limb(Vector3.Lerp(lip, land, 0.5f) + Vector3.up * 0.15f, land, 0.02f, 0.035f, 4, S(Pal.ShirtWhite), false);
            }
            sm.loose.Limb(o + Vector3.up * 1.9f, o + Vector3.up * 2.9f, 0.04f, 0.015f, 5, S(Pal.ShirtWhite), false);
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                Bench(sm.atlas, o + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 4.6f, k * 90f + 45f + 180f, Pal.BoatGreen, Pal.Concrete);
            }
        }

        /// <summary>The bull ride: a padded ring, the mechanical bull on its post, the board.</summary>
        static void BullRide(WorldCtx ctx, SiteMeshes sm, Vector2 c, float yaw, System.Random rng)
        {
            float y = ctx.H(c) + 0.05f;
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 o = V3(c, y);
            sm.atlas.Cylinder(o + Vector3.down * 0.3f, 2.6f, 2.6f, 0.62f, 16, S(Pal.JacketRed), true);
            sm.atlas.Cylinder(o + Vector3.up * 0.32f, 2.3f, 2.3f, 0.12f, 16, S(Pal.BoatBlue), true);
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f * Mathf.Deg2Rad;
                if (k == 0) continue;
                sm.atlas.Cylinder(o + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2.55f + Vector3.up * 0.3f, 0.05f, 0.05f, 0.9f, 5, S(Pal.BoatYellow), true);
            }
            sm.atlas.Cylinder(o + Vector3.up * 0.3f, 2.55f, 2.55f, 0.06f, 16, S(Pal.BoatYellow), false);
            Vector3 b = o + Vector3.up * 0.44f;
            sm.atlas.Cylinder(b, 0.22f, 0.18f, 0.55f, 8, S(Pal.IronBlack), true);
            var saved = sm.atlas.M;
            sm.atlas.M = Matrix4x4.TRS(b + Vector3.up * 0.55f, q * Quaternion.Euler(-6f, 25f, 0f), Vector3.one);
            Vector2 hide = S(Pal.HairBlack), horn = S(Pal.ShirtWhite);
            sm.atlas.Sphere(new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.42f, 0.85f), 10, 6, hide);
            sm.atlas.Sphere(new Vector3(0f, 0.55f, -0.25f), new Vector3(0.46f, 0.48f, 0.5f), 8, 5, hide);
            sm.atlas.Limb(new Vector3(0f, 0.6f, 0.7f), new Vector3(0f, 0.55f, 1.05f), 0.24f, 0.2f, 8, hide);
            sm.atlas.Sphere(new Vector3(0f, 0.5f, 1.15f), new Vector3(0.2f, 0.2f, 0.28f), 8, 4, hide);
            foreach (float x in new[] { -1f, 1f })
            {
                sm.atlas.Limb(new Vector3(x * 0.12f, 0.68f, 1.1f), new Vector3(x * 0.42f, 0.85f, 1.12f), 0.05f, 0.02f, 5, horn);
                foreach (float z in new[] { -0.55f, 0.5f })
                    sm.atlas.Limb(new Vector3(x * 0.22f, 0.25f, z), new Vector3(x * 0.25f, -0.25f, z + 0.1f), 0.1f, 0.07f, 6, hide);
            }
            sm.atlas.Limb(new Vector3(0f, 0.6f, -0.82f), new Vector3(0f, 0.1f, -1.0f), 0.03f, 0.02f, 4, hide);
            sm.atlas.Box(new Vector3(0f, 0.88f, 0.05f), new Vector3(0.5f, 0.08f, 0.6f), S(Pal.JacketRed));   // the saddle
            sm.atlas.M = saved;
            Vector2 bp = c + XZ(q * Vector3.forward) * 3.2f + XZ(q * Vector3.right) * 1.6f;
            Board(sm, V3(bp, ctx.H(bp) + 1.6f), yaw, 1.6f, 0.55f, Pal.JacketRed, new[] { "BULL RIDE" }, new[] { 0.26f }, Pal.BoatYellow, Pal.IronBlack, ctx.H(bp));
        }

        /// <summary>The painted statue of a Kumaoni villager by the junction: yellow turban, brown jacket, yellow dhoti,
        /// a green staff; a painted blue drum beside him.</summary>
        static void VillagerStatue(WorldCtx ctx, SiteMeshes sm, Vector2 c, float yaw, MeshBuilder stone)
        {
            float g = ctx.H(c);
            var q = Quaternion.Euler(0f, yaw, 0f);
            TBox(stone, V3(c, g + 0.2f), new Vector3(1.0f, 1.0f, 1.0f), q, 1.2f);
            Vector3 o = V3(c, g + 0.7f);
            var saved = sm.atlas.M;
            sm.atlas.M = Matrix4x4.TRS(o, q, Vector3.one * 1.05f);
            Vector2 skin = S(Pal.SkinMid), dhoti = S(Pal.JacketYellow), jacket = S(Pal.ShoeBrown), turban = S(Pal.BoatYellow);
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                sm.atlas.Limb(new Vector3(x, 0.95f, 0f), new Vector3(x * 1.3f, 0.5f, 0.02f), 0.13f, 0.11f, 8, dhoti);
                sm.atlas.Limb(new Vector3(x * 1.3f, 0.5f, 0.02f), new Vector3(x * 1.3f, 0.07f, 0f), 0.06f, 0.05f, 6, skin);
                sm.atlas.Box(new Vector3(x * 1.3f, 0.04f, 0.06f), new Vector3(0.1f, 0.08f, 0.24f), S(Pal.ShoeBrown));
            }
            sm.atlas.Limb(new Vector3(0f, 0.92f, 0f), new Vector3(0f, 1.42f, 0f), 0.2f, 0.22f, 10, jacket);
            sm.atlas.Sphere(new Vector3(0f, 1.42f, 0f), new Vector3(0.24f, 0.09f, 0.15f), 8, 4, jacket);
            sm.atlas.Limb(new Vector3(0f, 1.48f, 0f), new Vector3(0f, 1.56f, 0f), 0.05f, 0.05f, 6, skin);
            sm.atlas.Sphere(new Vector3(0f, 1.66f, 0.01f), new Vector3(0.1f, 0.12f, 0.11f), 10, 6, skin);
            sm.atlas.Sphere(new Vector3(0f, 1.6f, 0.07f), new Vector3(0.07f, 0.05f, 0.05f), 6, 3, S(Pal.HairBlack));   // the moustache
            sm.atlas.Sphere(new Vector3(0f, 1.77f, -0.01f), new Vector3(0.14f, 0.09f, 0.14f), 10, 5, turban);
            sm.atlas.Limb(new Vector3(0.24f, 1.38f, 0f), new Vector3(0.3f, 1.08f, 0.12f), 0.06f, 0.05f, 6, jacket);
            sm.atlas.Limb(new Vector3(0.3f, 1.08f, 0.12f), new Vector3(0.32f, 1.05f, 0.3f), 0.05f, 0.045f, 6, jacket);
            sm.atlas.Sphere(new Vector3(0.32f, 1.05f, 0.33f), Vector3.one * 0.045f, 6, 3, skin);
            sm.atlas.Limb(new Vector3(-0.24f, 1.38f, 0f), new Vector3(-0.27f, 0.98f, 0f), 0.06f, 0.05f, 6, jacket);
            sm.atlas.Sphere(new Vector3(-0.27f, 0.94f, 0f), Vector3.one * 0.045f, 6, 3, skin);
            sm.atlas.Limb(new Vector3(0.33f, 0.0f, 0.33f), new Vector3(0.31f, 2.1f, 0.33f), 0.025f, 0.022f, 6, S(Pal.BoatGreen));
            sm.atlas.M = saved;
            // the drum
            Vector3 d = V3(c, g) + q * new Vector3(-1.3f, 0f, 0.2f);
            d.y = ctx.H(XZ(d));
            sm.atlas.Cylinder(d, 0.36f, 0.36f, 0.95f, 14, S(Pal.JacketBlue), true);
            foreach (float hh in new[] { 0.12f, 0.48f, 0.83f }) sm.atlas.Cylinder(d + Vector3.up * hh, 0.37f, 0.37f, 0.05f, 14, S(Pal.ShirtWhite), false);
        }

        /// <summary>The bamboo hut: four bamboo posts, a thatch roof, a bench, a lantern hanging in the middle.</summary>
        static void BambooHut(WorldCtx ctx, SiteMeshes sm, Vector2 c, float yaw)
        {
            float g = ctx.H(c);
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 o = V3(c, g);
            Vector2 cane = S(Pal.WoodLight);
            foreach (float x in new[] { -1.3f, 1.3f })
                foreach (float z in new[] { -1.3f, 1.3f })
                {
                    Vector3 p = o + q * new Vector3(x, 0f, z);
                    p.y = ctx.H(XZ(p)) - 0.2f;
                    sm.atlas.Limb(p, o + q * new Vector3(x, 2.4f, z), 0.07f, 0.06f, 6, cane);
                }
            foreach (float z in new[] { -1.3f, 1.3f }) sm.atlas.Limb(o + q * new Vector3(-1.4f, 2.35f, z), o + q * new Vector3(1.4f, 2.35f, z), 0.05f, 0.05f, 5, cane);
            sm.atlas.Pyramid(o + Vector3.up * 2.35f, 3.6f, 3.6f, 1.5f, S(Pal.PantsKhaki));
            sm.atlas.Box(o + q * new Vector3(0f, 0.42f, -1.0f), new Vector3(2.2f, 0.07f, 0.4f), q, cane);
            foreach (float x in new[] { -0.9f, 0.9f }) sm.atlas.Box(o + q * new Vector3(x, 0.2f, -1.0f), new Vector3(0.08f, 0.4f, 0.36f), q, cane);
            Vector3 lamp = o + Vector3.up * 1.8f;
            sm.loose.Limb(lamp + Vector3.up * 0.55f, lamp + Vector3.up * 0.18f, 0.01f, 0.01f, 3, S(Pal.IronBlack), false);
            sm.loose.Box(lamp, new Vector3(0.22f, 0.3f, 0.22f), S(Pal.IronBlack));
            sm.loose.Sphere(lamp, new Vector3(0.1f, 0.13f, 0.1f), 6, 4, S(Pal.LampBulb));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Things on the lake side of Lower Mall in the user's Street View route (19 Nov 2026, Aug 2022 imagery), placed from
    /// the panoramas' positions: the City Joggers Park open gym (yellow and red frames on the bank, panoramas 50-56), the
    /// pedal swan boats moored below the walk (74-76), and the Rotary Selfie Point - a long timber pavilion of cabins with a
    /// red tin roof, little gables and a scalloped white fascia, a blue arch at its south end (80-83).
    /// </summary>
    public static partial class LandmarkBuilder
    {
        // the car's track on Lower Mall (local metres), heading north
        static readonly Vector2 GymA = new Vector2(784.6f, -646.8f), GymB = new Vector2(748.3f, -591.3f);
        static readonly Vector2 BoatsA = new Vector2(672.4f, -416.0f), BoatsB = new Vector2(665.0f, -397.0f);
        static readonly Vector2 SelfieA = new Vector2(646.0f, -363.0f), SelfieB = new Vector2(611.1f, -329.1f);

        /// <summary>The unit vector from the road towards the lake at p (the side where the water is nearer).</summary>
        static Vector2 LakeSide(WorldCtx ctx, Vector2 p, Vector2 along)
        {
            Vector2 n = new Vector2(-along.y, along.x);
            for (float d = 2f; d < 40f; d += 2f)
            {
                if (ctx.InLake(p + n * d)) return n;
                if (ctx.InLake(p - n * d)) return -n;
            }
            return n.x < 0f ? n : -n;   // the lake lies west of this stretch
        }

        /// <summary>A point beside the walk, `off` metres towards the lake from the car's track, kept out of the water.</summary>
        static Vector2 Bank(WorldCtx ctx, Vector2 track, Vector2 lake, float off)
        {
            Vector2 p = track + lake * off;
            for (int k = 0; k < 12 && ctx.InLake(p); k++) p -= lake * 0.5f;
            return p;
        }

        internal static void BuildLowerMallSights(WorldCtx ctx, Transform parent)
        {
            var sm = new SiteMeshes();
            var rng = new System.Random(2208);

            // ---- the open gym: frames of yellow posts and red bars, seats and pedals, a board
            {
                Vector2 along = (GymB - GymA).normalized, lake = LakeSide(ctx, GymA, along);
                float len = Vector2.Distance(GymA, GymB);
                int pieces = 0;
                for (float s = 6f; s < len - 4f; s += 5.5f + (float)rng.NextDouble() * 3f)
                {
                    Vector2 p = Bank(ctx, GymA + along * s, lake, 5.2f + (float)rng.NextDouble() * 1.5f);
                    float y = ctx.H(p);
                    Vector3 c = V3(p, y), a3 = new Vector3(along.x, 0f, along.y);
                    Vector3 p0 = c - a3 * 0.7f, p1 = c + a3 * 0.7f;
                    sm.atlas.Cylinder(p0, 0.06f, 0.06f, 2.1f, 6, S(Pal.BoatYellow), true);
                    sm.atlas.Cylinder(p1, 0.06f, 0.06f, 2.1f, 6, S(Pal.BoatYellow), true);
                    sm.atlas.Limb(p0 + Vector3.up * 2.05f, p1 + Vector3.up * 2.05f, 0.05f, 0.05f, 6, S(Pal.BoatRed), false);
                    int kind = pieces % 3;
                    if (kind == 0)   // a swing seat on red arms
                        foreach (float t in new[] { 0.3f, 0.7f })
                        {
                            Vector3 top = Vector3.Lerp(p0, p1, t) + Vector3.up * 2.0f;
                            sm.atlas.Limb(top, top + Vector3.down * 1.4f, 0.025f, 0.025f, 5, S(Pal.BoatRed), false);
                            sm.atlas.Box(top + Vector3.down * 1.45f, new Vector3(0.35f, 0.06f, 0.4f), S(Pal.BoatRed));
                        }
                    else if (kind == 1)   // pedals on a red frame
                    {
                        sm.atlas.Box(c + Vector3.up * 0.55f, new Vector3(0.5f, 0.08f, 0.35f), Quaternion.LookRotation(a3), S(Pal.BoatRed));
                        sm.atlas.Limb(c + Vector3.up * 0.55f, c + Vector3.up * 1.2f, 0.04f, 0.04f, 5, S(Pal.BoatYellow), false);
                        sm.atlas.Box(c + Vector3.up * 0.35f + a3 * 0.4f, new Vector3(0.25f, 0.05f, 0.12f), S(Pal.JacketBlack));
                    }
                    else   // a twister disc
                        sm.atlas.Cylinder(c + Vector3.up * 0.15f, 0.35f, 0.35f, 0.08f, 10, S(Pal.BoatRed), true);
                    // a workout spot the player can use (Runtime/Interaction/GymStation.cs; stats only, 22 Nov 2026)
                    var gs = new GameObject("Gym spot " + pieces).AddComponent<GymStation>();
                    gs.transform.SetParent(parent, false);
                    gs.transform.position = c + Vector3.up * 0.9f;
                    gs.kind = kind == 0 ? "pull" : kind == 1 ? "pedal" : "twist";
                    gs.along = a3;   // the top bar's direction (the pull-ups' hands go on it)
                    gs.radius = 1.8f;
                    pieces++;
                }
                Vector2 bp = Bank(ctx, GymA + along * 3f, lake, 3.6f);
                float yaw = Mathf.Atan2(-lake.x, -lake.y) * Mathf.Rad2Deg;   // facing the road
                Board(sm, V3(bp, ctx.H(bp) + 1.7f), yaw, 1.8f, 0.9f, Pal.ShirtWhite, new[] { "CITY JOGGERS PARK", "OPEN GYM" }, new[] { 0.14f, 0.18f },
                      Pal.BoatBlue, Pal.StoneGrey, ctx.H(bp));
                Debug.Log($"[Nainital] Lower Mall: open gym, {pieces} pieces");
            }

            // ---- the swan boats moored along the walk
            {
                Vector2 along = (BoatsB - BoatsA).normalized, lake = LakeSide(ctx, BoatsA, along);
                float len = Vector2.Distance(BoatsA, BoatsB) + 10f;
                int[] pals = { Pal.SareePink, Pal.ShirtWhite, Pal.BoatBlue, Pal.BoatYellow, Pal.SareePink, Pal.ShirtWhite };
                int boats = 0;
                for (float s = 0f; s < len; s += 2.2f)
                {
                    Vector2 p = BoatsA + along * s + lake * 4f;
                    for (int k = 0; k < 20 && !ctx.InLake(p); k++) p += lake * 0.5f;   // out on the water
                    p += lake * 1.2f;
                    if (!ctx.InLake(p)) continue;
                    Vector3 c = V3(p, ctx.lakeY + 0.05f);
                    var q = Quaternion.LookRotation(new Vector3(-lake.x, 0f, -lake.y));   // nose to the bank
                    int pal = pals[boats % pals.Length];
                    sm.atlas.Box(c + Vector3.up * 0.25f, new Vector3(1.5f, 0.5f, 2.1f), q, S(pal));
                    sm.atlas.Box(c + Vector3.up * 0.55f + q * new Vector3(0f, 0f, -0.3f), new Vector3(1.2f, 0.1f, 0.7f), q, S(Pal.ShirtWhite));   // the seat
                    Vector3 neck0 = c + q * new Vector3(0f, 0.45f, 0.9f), neck1 = c + q * new Vector3(0f, 1.5f, 1.05f);
                    sm.atlas.Limb(neck0, neck1, 0.16f, 0.1f, 6, S(pal), false);
                    sm.atlas.Sphere(neck1 + q * new Vector3(0f, 0.05f, 0.12f), new Vector3(0.28f, 0.26f, 0.42f), 8, 4, S(pal));
                    sm.atlas.Box(neck1 + q * new Vector3(0f, 0f, 0.38f), new Vector3(0.08f, 0.07f, 0.18f), q, S(Pal.Saffron));   // the beak
                    boats++;
                }
                Debug.Log($"[Nainital] Lower Mall: {boats} swan boats");
            }

            // ---- the Rotary Selfie Point: a row of timber cabins under one red tin roof, little gables, a blue arch
            {
                Vector2 along = (SelfieB - SelfieA).normalized, lake = LakeSide(ctx, SelfieA, along);
                var wood = sm.Photo("brown_planks_04", new Color(1.25f, 1.08f, 0.82f));
                float len = Mathf.Min(Vector2.Distance(SelfieA, SelfieB), 30f);
                Vector2 c0 = Bank(ctx, (SelfieA + SelfieB) * 0.5f, lake, 6.0f);
                float y = ctx.H(c0) + 0.1f;
                var q = Quaternion.LookRotation(new Vector3(along.x, 0f, along.y));
                Vector3 C(float x, float yy, float z) => V3(c0, y) + q * new Vector3(x, yy, z);   // x across (lake +), z along
                float sx = Vector2.Dot(lake, new Vector2((q * Vector3.right).x, (q * Vector3.right).z)) > 0f ? 1f : -1f;
                const float depth = 4.2f, wallH = 2.8f;
                TBox(wood, C(0f, wallH * 0.5f, 0f), new Vector3(depth, wallH, len), q, 1.2f);
                // the red tin roof, two slopes along the length, with a scalloped white fascia
                for (int side = -1; side <= 1; side += 2)
                {
                    var rq = q * Quaternion.Euler(0f, 0f, side * 24f);
                    sm.atlas.Box(C(side * depth * 0.28f, wallH + 0.55f, 0f), new Vector3(depth * 0.62f, 0.07f, len + 0.8f), rq, S(Pal.TempleRed));
                    for (float z = -len * 0.5f; z < len * 0.5f; z += 0.45f)
                        sm.loose.Box(C(side * (depth * 0.5f + 0.32f), wallH + 0.12f, z + 0.22f), new Vector3(0.04f, 0.22f, 0.36f), q, S(Pal.ShirtWhite));
                }
                // little gables over the doors on the road side, every 7 m
                for (float z = -len * 0.5f + 3.5f; z < len * 0.5f - 2f; z += 7f)
                {
                    Vector3 g = C(-sx * (depth * 0.5f + 0.2f), wallH + 0.7f, z);
                    sm.atlas.Box(g, new Vector3(0.5f, 1.1f, 1.8f), q, S(Pal.TempleRed));
                    sm.loose.Box(C(-sx * (depth * 0.5f + 0.03f), 1.15f, z), new Vector3(0.05f, 2.0f, 1.0f), q, S(Pal.WoodDark));   // the door
                    sm.loose.Box(C(-sx * (depth * 0.5f + 0.03f), 1.5f, z + 2.0f), new Vector3(0.05f, 0.9f, 1.1f), q, S(Pal.JacketBlack));   // a window
                }
                // the blue arch at the south end, with the board
                Vector3 archC = C(-sx * 0.4f, 0f, -len * 0.5f - 1.6f);
                foreach (float x in new[] { -1.1f, 1.1f })
                    sm.atlas.Box(archC + q * new Vector3(x, 1.3f, 0f), new Vector3(0.18f, 2.6f, 0.18f), q, S(Pal.BoatBlue));
                for (int k = 0; k <= 8; k++)
                {
                    float a0 = Mathf.PI * k / 8f, a1 = Mathf.PI * (k + 1) / 8f;
                    if (k == 8) break;
                    Vector3 p0 = archC + q * new Vector3(Mathf.Cos(a0) * 1.1f, 2.6f + Mathf.Sin(a0) * 0.7f, 0f);
                    Vector3 p1 = archC + q * new Vector3(Mathf.Cos(a1) * 1.1f, 2.6f + Mathf.Sin(a1) * 0.7f, 0f);
                    sm.atlas.Limb(p0, p1, 0.09f, 0.09f, 5, S(Pal.BoatBlue), false);
                }
                float yawA = q.eulerAngles.y + 180f;
                Board(sm, archC + q * new Vector3(0f, 2.75f, -0.05f), yawA, 1.9f, 0.42f, Pal.BoatBlue, new[] { "ROTARY SELFIE POINT" }, new[] { 0.14f },
                      Pal.ShirtWhite, Pal.BoatBlue, y + 2.5f);
                // a little roof over the arch
                sm.atlas.Box(archC + q * new Vector3(0f, 3.55f, 0f), new Vector3(2.8f, 0.08f, 1.0f), q, S(Pal.TempleRed));
                Debug.Log($"[Nainital] Lower Mall: Rotary Selfie Point at ({c0.x:F1}, {c0.y:F1}), {len:F0} m");
            }
            sm.Emit(ctx, "Lower Mall lake side (Street View)", parent);
        }
    }
}

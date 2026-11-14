using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The ground round the Capitol and the Mallital lake bank (user, 2026-11-14 screenshots):
    ///  - "stairs red line direction nahi, balki green direction hongi; terrain smoothed nahi hai; upar ki upar aur neeche ki
    ///    neeche, beech mein stairs". The tiled footway climbed straight up a grass slope from the Capitol's front to the walk.
    ///    Now the walk keeps its level, the Capitol's front lies flat at its own level, a stone retaining wall stands between
    ///    them along the walk, and a flight of stone stairs runs down along that wall. The footway's ramp leg is gone
    ///    (WorldBuilder.DataFixes.cs starts it at the stairs' foot).
    ///  - "ye kya banaya hai": between the lakeside path and the water the DEM fell away as a rock slope, so the railing and
    ///    the embankment sat down at the water. The bank is now level with the path right to the shore; the embankment wall
    ///    drops straight into the lake with the railing on top.
    /// Runs before the roads are levelled and built, so the paths follow the new ground.
    /// </summary>
    public static partial class WorldBuilder
    {
        // the walk to Naina Devi at the Capitol's front corner, its direction (north-east), and the side toward the lake
        static readonly Vector2 CapWalkAt = new Vector2(-5f, -15f), CapWalkDir = new Vector2(0.55f, 0.835f).normalized;
        static Vector2 CapLakeSide => new Vector2(CapWalkDir.y, -CapWalkDir.x);   // south-east
        const float CapWallOff = 5.3f;          // the retaining wall's line, from the walk's centre
        const float CapWallFrom = -3.5f, CapWallTo = 7f;   // along the walk from CapWalkAt
        internal static Vector2 CapStairsFoot => CapWalkAt + CapWalkDir * (CapWallTo - 4.4f) + CapLakeSide * (CapWallOff + 1.3f);

        // the lakeside path in front of the Capitol, from the Capitol corner to the boat stand
        static readonly Vector2[] MallitalBankPath =
        {
            new Vector2(9f, -73f), new Vector2(23f, -56f), new Vector2(28f, -45f), new Vector2(28f, -37f), new Vector2(28f, -27f),
            new Vector2(28f, -17f), new Vector2(35f, 0f), new Vector2(40f, 19f), new Vector2(50f, 36f),
        };

        static float capLowY = float.NaN, capHighY = float.NaN;

        static void ShapeCapitolGround(WorldCtx ctx)
        {
            var td = ctx.terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 tp = ctx.terrain.transform.position, sz = td.size;
            float cx = (res - 1) / sz.x, cz = (res - 1) / sz.z;

            // the two levels: the walk where it passes the Capitol, and the ground at the Capitol's front
            capHighY = (ctx.H(CapWalkAt) + ctx.H(CapWalkAt + CapWalkDir * 6f)) * 0.5f;
            capLowY = Mathf.Min(ctx.H(new Vector2(2f, -26f)), ctx.H(new Vector2(10f, -20f)), capHighY - 1.2f);

            // the path along the lake: its level from the ground under it, smoothed
            var path = Resample(MallitalBankPath, 2f, false);
            var pathY = new float[path.Count];
            for (int i = 0; i < path.Count; i++) pathY[i] = ctx.H(path[i]);
            var sm = new float[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                float s = 0f; int c = 0;
                for (int j = Mathf.Max(0, i - 4); j <= Mathf.Min(path.Count - 1, i + 4); j++) { s += pathY[j]; c++; }
                sm[i] = Mathf.Max(s / c, ctx.lakeY + 1.4f);
            }

            int x0 = Mathf.Clamp(Mathf.FloorToInt((-30f - tp.x) * cx), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((65f - tp.x) * cx), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((-85f - tp.z) * cz), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((45f - tp.z) * cz), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var hs = td.GetHeights(x0, z0, w, h);
            int low = 0, bank = 0;
            for (int zi = 0; zi < h; zi++)
                for (int xi = 0; xi < w; xi++)
                {
                    var q = new Vector2(tp.x + (x0 + xi) / cx, tp.z + (z0 + zi) / cz);
                    float cur = tp.y + hs[zi, xi] * sz.y;
                    float want = cur;

                    // 1. the Capitol's front: flat at the low level, from the retaining wall to the lakeside path
                    Vector2 rel = q - CapWalkAt;
                    float along = Vector2.Dot(rel, CapWalkDir), side = Vector2.Dot(rel, CapLakeSide);
                    if (side > CapWallOff && along > -22f && along < CapWallTo + 2f)
                    {
                        float dPath = Geo.DistanceToPolyline(MallitalBankPath, q, false);
                        if (side < CapWallOff + 40f && dPath > 1.5f && !ctx.InLake(q) && ctx.LakeDistance(q) > 2.5f)
                        {
                            // a 3 m ease at the far (north-east) end into the park's own ground
                            float ease = Mathf.Clamp01((along - CapWallTo) / 2f);
                            want = Mathf.Lerp(capLowY, cur, ease);
                            low++;
                        }
                    }
                    // the strip between the walk and the wall: at the walk's level
                    else if (side > 2f && side <= CapWallOff && along > CapWallFrom && along < CapWallTo)
                        want = capHighY;

                    // 2. the bank between the lakeside path and the water: level with the path to the shore
                    if (!ctx.InLake(q))
                    {
                        float dl = ctx.LakeDistance(q);
                        if (dl < 9f)
                        {
                            int best = -1; float bd = float.MaxValue;
                            for (int i = 0; i < path.Count; i++) { float d = (path[i] - q).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                            if (best >= 0 && bd < 14f * 14f && dl <= ctx.LakeDistance(path[best]) + 0.5f)   // between the path and the shore
                            {
                                float y = sm[best] - 0.05f;
                                if (want < y) { want = y; bank++; }
                            }
                        }
                    }
                    if (Mathf.Abs(want - cur) > 0.01f) hs[zi, xi] = Mathf.Clamp01((want - tp.y) / sz.y);
                }
            td.SetHeights(x0, z0, hs);
            Debug.Log($"[Nainital] Capitol ground: walk {capHighY:F1}, front {capLowY:F1} ({low} samples); lake bank raised at {bank} samples");
        }

        /// <summary>The stone retaining wall between the walk and the Capitol's front, and the stairs down along it.</summary>
        static void CapitolWallAndStairs(WorldCtx ctx, MeshBuilder mb, MeshBuilder col)
        {
            if (float.IsNaN(capLowY)) return;
            float rise = capHighY - capLowY;
            if (rise < 0.4f) return;
            Vector2 stone = Atlas.MaterialUV(Atlas.MatStone), cap = Atlas.MaterialUV(Atlas.MatConcrete);
            Vector2 a = CapWalkAt + CapWalkDir * CapWallFrom + CapLakeSide * CapWallOff, b = CapWalkAt + CapWalkDir * CapWallTo + CapLakeSide * CapWallOff;
            var wrot = Quaternion.LookRotation(new Vector3(CapWalkDir.x, 0f, CapWalkDir.y));
            float len = Vector2.Distance(a, b);
            Vector2 mid = (a + b) * 0.5f;
            mb.Box(V(mid, capLowY + rise * 0.5f - 0.3f), new Vector3(0.6f, rise + 0.6f, len), wrot, stone);
            col.Box(V(mid, capLowY + rise * 0.5f), new Vector3(0.6f, rise, len), wrot, Vector2.zero);
            mb.Box(V(mid, capHighY + 0.2f), new Vector3(0.75f, 0.4f, len + 0.2f), wrot, cap);   // the coping people sit on
            // iron railing on the coping, except over the stairs' top
            for (float t = 0f; t < len - 4.6f; t += 2f)
            {
                Vector2 p = a + CapWalkDir * t;
                mb.Limb(V(p, capHighY + 0.4f), V(p, capHighY + 1.3f), 0.03f, 0.03f, 4, Atlas.SolidUV(Pal.IronGreen), false);
                mb.Limb(V(p, capHighY + 1.3f), V(p + CapWalkDir * 2f, capHighY + 1.3f), 0.025f, 0.025f, 4, Atlas.SolidUV(Pal.IronGreen), false);
            }
            // the flight: 2.4 m wide, beside the wall on the low side, going down toward the Capitol (south-west)
            int steps = Mathf.Max(3, Mathf.CeilToInt(rise / 0.17f));
            float tread = 0.3f, sr = rise / steps;
            var srot = Quaternion.LookRotation(new Vector3(-CapWalkDir.x, 0f, -CapWalkDir.y));
            Vector2 top = b + CapLakeSide * 1.3f;   // the stairs' top, at the wall's north-east end
            // a landing at the top, joined to the walk through a gap in the wall
            mb.Box(V(top + CapWalkDir * 0.6f, capHighY - 0.25f), new Vector3(2.6f, 0.5f, 1.2f), srot, stone);
            col.Box(V(top + CapWalkDir * 0.6f, capHighY - 0.25f), new Vector3(2.6f, 0.5f, 1.2f), srot, Vector2.zero);
            for (int k = 0; k < steps; k++)
            {
                float sy = capHighY - (k + 1) * sr;
                Vector2 sp = top - CapWalkDir * (tread * 0.5f + k * tread);
                float sh = sy - capLowY + 0.3f;
                mb.Box(V(sp, capLowY - 0.3f + sh * 0.5f), new Vector3(2.4f, sh, tread + 0.02f), srot, stone);
                col.Box(V(sp, capLowY - 0.3f + sh * 0.5f), new Vector3(2.4f, sh, tread + 0.02f), srot, Vector2.zero);
            }
            // the outer side of the flight: a low stone wall with a rail
            Vector2 o0 = top + CapLakeSide * 1.3f, o1 = top - CapWalkDir * (steps * tread) + CapLakeSide * 1.3f;
            for (int k = 0; k <= steps; k += 2)
            {
                float y = capHighY - k * sr;
                Vector2 p = Vector2.Lerp(o0, o1, k / (float)steps);
                mb.Limb(V(p, y), V(p, y + 0.95f), 0.03f, 0.03f, 4, Atlas.SolidUV(Pal.IronGreen), false);
            }
            mb.Limb(V(o0, capHighY + 0.95f), V(o1, capLowY + 0.95f), 0.03f, 0.03f, 4, Atlas.SolidUV(Pal.IronGreen), false);
            Debug.Log($"[Nainital] Capitol wall and stairs: rise {rise:F2} m, {steps} steps");
        }
    }
}

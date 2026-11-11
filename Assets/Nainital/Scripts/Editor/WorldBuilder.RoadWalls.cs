using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Hill roads as they are in Nainital: where a road is cut into the slope a mossy rubble retaining wall holds the
    /// bank, and where it stands on fill a low parapet - whitewashed, with black bands - runs along the drop. Without them the
    /// cuts showed as grassy walls of earth (user, 2026-11-11: "kuch jagah terrain bahut gadbad hai").</summary>
    public static partial class WorldBuilder
    {
        static void BuildRoadWalls(WorldCtx ctx)
        {
            var parent = new GameObject("Road walls and parapets").transform;
            parent.SetParent(ctx.worldRoot, false);
            var mossMat = MossyStoneMaterial();
            var walls = new MallChunks(ctx, parent, "Retaining wall", mossMat ? mossMat : ctx.mats.atlas, true, 0.1f);
            var parapets = new MallChunks(ctx, parent, "Parapet", ctx.mats.atlas, true, 0.1f);
            var col = new MeshBuilder();

            // houses near a road: a wall must not run into them
            var houseCells = new Dictionary<Vector2Int, List<Vector3>>();
            var hd = ctx.world.houses;
            for (int i = 0; i + 8 < hd.Length; i += 9)
            {
                var k = new Vector2Int(Mathf.FloorToInt(hd[i] / 20f), Mathf.FloorToInt(hd[i + 1] / 20f));
                if (!houseCells.TryGetValue(k, out var list)) houseCells[k] = list = new List<Vector3>();
                list.Add(new Vector3(hd[i], hd[i + 1], Mathf.Max(hd[i + 2], hd[i + 3]) * 0.5f));
            }
            bool NearHouse(Vector2 q)
            {
                var k = new Vector2Int(Mathf.FloorToInt(q.x / 20f), Mathf.FloorToInt(q.y / 20f));
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (houseCells.TryGetValue(new Vector2Int(k.x + dx, k.y + dz), out var list))
                            foreach (var h in list) if ((new Vector2(h.x, h.y) - q).magnitude < h.z + 2.5f) return true;
                return false;
            }
            // other roads' decks: no wall across a side road or a lane (points every 2 m in a 20 m grid)
            var deckCells = new Dictionary<Vector2Int, List<(Vector2 p, float half, int road)>>();
            for (int ri = 0; ri < ctx.world.roads.Length; ri++)
            {
                var r = ctx.world.roads[ri];
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                foreach (var q in Resample(line, 2f, false))
                {
                    var k = new Vector2Int(Mathf.FloorToInt(q.x / 20f), Mathf.FloorToInt(q.y / 20f));
                    if (!deckCells.TryGetValue(k, out var list)) deckCells[k] = list = new List<(Vector2, float, int)>();
                    list.Add((q, r.width * 0.5f + 1.2f, ri));
                }
            }
            bool OnOtherDeck(Vector2 q, int own)
            {
                var k = new Vector2Int(Mathf.FloorToInt(q.x / 20f), Mathf.FloorToInt(q.y / 20f));
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (deckCells.TryGetValue(new Vector2Int(k.x + dx, k.y + dz), out var list))
                            foreach (var (pp, half, road) in list) if (road != own && (pp - q).sqrMagnitude < half * half) return true;
                return false;
            }

            Vector2 white = Atlas.WallSolid(0), black = Atlas.SolidUV(Pal.IronBlack), cap = Atlas.MaterialUV(Atlas.MatConcrete);
            int nWall = 0, nPara = 0;
            for (int ri = 0; ri < ctx.world.roads.Length; ri++)
            {
                var r = ctx.world.roads[ri];
                if (!VehicleKinds.Contains(r.kind) || IsMallDeck(r.name)) continue;
                var own = Geo.ToPoints(r.pts);
                var path = Resample(own, 2f, false);
                if (path.Count < 3) continue;
                float half = r.width * 0.5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    // per sample: 0 none, 1 retaining wall (with its height), 2 parapet
                    var kind = new int[path.Count];
                    var top = new float[path.Count];
                    var edge = new Vector2[path.Count];
                    var baseY = new float[path.Count];
                    var face = new Vector3[path.Count];
                    for (int i = 0; i < path.Count; i++)
                    {
                        Vector2 p = path[i];
                        Vector2 tan = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                        Vector2 n = new Vector2(-tan.y, tan.x) * side;
                        float hc = ctx.H(p);
                        edge[i] = p + n * (half + 0.35f);
                        baseY[i] = hc;
                        face[i] = Flat(-n);
                        if (NearJunction(ctx, p) || ctx.InLake(edge[i]) || OnOtherDeck(edge[i], ri) || NearHouse(edge[i]) || ctx.InKeepClear(edge[i], 1f)) continue;
                        float rise = ctx.H(p + n * (half + 2.8f)) - hc;
                        if (rise > 1.4f) { kind[i] = 1; top[i] = Mathf.Clamp(rise + 0.4f, 1.2f, 4.8f); }
                        else if (rise < -2.2f) { kind[i] = 2; top[i] = 0.65f; }
                    }
                    float run = 0f;
                    for (int i = 0; i + 1 < path.Count; i++)
                    {
                        float seg = Vector2.Distance(edge[i], edge[i + 1]);
                        float u0 = run;
                        run += seg;
                        if (kind[i] == 0 || kind[i] != kind[i + 1] || seg > 4f) continue;
                        Vector2 a = edge[i], b = edge[i + 1];
                        float a0 = baseY[i] - 0.3f, b0 = baseY[i + 1] - 0.3f, a1 = baseY[i] + top[i], b1 = baseY[i + 1] + top[i + 1];
                        Vector3 toward = face[i];
                        Vector2 back = new Vector2(-toward.x, -toward.z) * 0.35f;
                        if (kind[i] == 1)
                        {
                            var c = walls.At(a);
                            WallFace(c[0], a, b, a0, b0, a1, b1, u0, u0 + seg, toward, 2.5f);
                            c[0].Quad(V(a, a1), V(b, b1), V(b + back, b1), V(a + back, a1), new Rect(cap, Vector2.zero), Vector3.up);
                            nWall++;
                        }
                        else
                        {
                            var c = parapets.At(a);
                            Vector2 uv = ((int)(u0 / 3f)) % 3 == 2 ? black : white;   // whitewash with a black band every few metres
                            c[0].Quad(V(a, a0), V(b, b0), V(b, b1), V(a, a1), new Rect(uv, Vector2.zero), toward);
                            c[0].Quad(V(b + back, b0), V(a + back, a0), V(a + back, a1), V(b + back, b1), new Rect(uv, Vector2.zero), -toward);
                            c[0].Quad(V(a, a1), V(b, b1), V(b + back, b1), V(a + back, a1), new Rect(cap, Vector2.zero), Vector3.up);
                            nPara++;
                        }
                        BlockBothWays(col, V(a, a0), V(b, b0), V(b, b1 + 0.3f), V(a, a1 + 0.3f), toward);
                    }
                }
            }
            walls.Flush();
            parapets.Flush();
            ctx.MeshObject("Road walls (collision)", parent, new MeshBuilder(), ctx.mats.atlas, col, false);
            Debug.Log($"[Nainital] road walls: {nWall * 2} m of retaining wall, {nPara * 2} m of parapet");
        }
    }
}

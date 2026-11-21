using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        // The town at night (owner, 21 Nov 2026: "raat ki overall lighting pe kaam krna pdega...abhi bhi bahut area balckout
        // ya dimmed hain"). NightCheck (174 street-level views at 21:00) found 43 of them over 70 % black: lamps stood on the
        // main roads only, 42 m apart on alternate sides (84 m on one side), never on a lane, a footpath or a short road, and
        // a lane with houses on both sides had no room for a pole, so it got none. Now:
        // - every road among the houses is lit: main roads ~32 m, lanes ~32 m, footpaths and steps ~28 m, a short lane one
        //   lamp in its middle; out of town the main roads keep about the old spacing and lanes / paths stay dark;
        // - no pole inside a house (the capsule must be clear), the other side tried, then a lamp on the house wall;
        // - no lamp within ~15 m of one already there (the Mall's, Tallital's, another road's at a junction);
        // - about half the houses facing a road keep a bulb on by the door (BuildPorchLights): small warm lights.

        static Dictionary<Vector2Int, int> houseCells;

        static int HousesNear(WorldCtx ctx, Vector2 p)
        {
            if (houseCells == null)
            {
                houseCells = new Dictionary<Vector2Int, int>();
                var h = ctx.world.houses;
                for (int i = 0; i + 8 < h.Length; i += 9)
                {
                    var k = new Vector2Int(Mathf.FloorToInt(h[i] / 20f), Mathf.FloorToInt(h[i + 1] / 20f));
                    houseCells[k] = houseCells.TryGetValue(k, out var c) ? c + 1 : 1;
                }
                if (ctx.world.osmBuildings != null)
                    foreach (var b in ctx.world.osmBuildings)
                    {
                        if (b.pts == null || b.pts.Length < 2) continue;
                        var k = new Vector2Int(Mathf.FloorToInt(b.pts[0] / 20f), Mathf.FloorToInt(b.pts[1] / 20f));
                        houseCells[k] = houseCells.TryGetValue(k, out var c) ? c + 1 : 1;
                    }
            }
            int n = 0, gx = Mathf.FloorToInt(p.x / 20f), gz = Mathf.FloorToInt(p.y / 20f);
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                    n += houseCells.TryGetValue(new Vector2Int(gx + dx, gz + dz), out var c) ? c : 0;
            return n;
        }

        const int SolidMask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << 10));
        static readonly Collider[] solidHits = new Collider[8];

        /// <summary>Something built (a house, a wall) where a pole would stand (the ground itself does not count).</summary>
        static bool SolidAt(WorldCtx ctx, Vector2 p)
        {
            float y = ctx.H(p);
            int n = Physics.OverlapCapsuleNonAlloc(new Vector3(p.x, y + 0.6f, p.y), new Vector3(p.x, y + 5.2f, p.y), 0.22f, solidHits, SolidMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!(solidHits[i] is TerrainCollider)) return true;
            return false;
        }

        static void BuildRoadLamps(WorldCtx ctx)
        {
            houseCells = null;
            Physics.SyncTransforms();
            var parent = new GameObject("Street Lights").transform;
            parent.SetParent(ctx.worldRoot, false);
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            var clear = Clearance(ctx);
            int n = 0, poles = 0, walls = 0, none = 0, near = 0, before = ctx.lampHeads.Count;

            // lamps placed so far, on a 16 m grid
            var placed = new Dictionary<Vector2Int, List<Vector2>>();
            void Mark(Vector2 q)
            {
                var k = new Vector2Int(Mathf.FloorToInt(q.x / 16f), Mathf.FloorToInt(q.y / 16f));
                if (!placed.TryGetValue(k, out var l)) placed[k] = l = new List<Vector2>();
                l.Add(q);
            }
            bool Near(Vector2 q, float r)
            {
                int gx = Mathf.FloorToInt(q.x / 16f), gz = Mathf.FloorToInt(q.y / 16f);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (placed.TryGetValue(new Vector2Int(gx + dx, gz + dz), out var l))
                            foreach (var o in l) if ((o - q).sqrMagnitude < r * r) return true;
                return false;
            }
            foreach (var h in ctx.lampHeads) Mark(new Vector2(h.x, h.z));

            void Flush(bool last)
            {
                if (!last && ++n % 120 != 0) return;
                ctx.MeshObject("Street Lights " + (last ? "last" : (n / 120).ToString()), parent, mb, ctx.mats.atlas, col);
                mb = new MeshBuilder();
                col = new MeshBuilder();
            }

            // a lamp on the house wall beside the road (the lane has no room for a pole)
            bool WallLamp(Vector2 p, Vector2 side, float reach)
            {
                float y0 = ctx.H(p);
                foreach (var s in new[] { side, -side })
                {
                    var from = new Vector3(p.x, y0 + 1.6f, p.y);
                    var dir = new Vector3(s.x, 0f, s.y);
                    if (!Physics.Raycast(from, dir, out var hit, reach, SolidMask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider is TerrainCollider || Mathf.Abs(hit.normal.y) > 0.35f) continue;
                    float y = y0 + 4.3f;
                    // the wall must go up that high (a low parapet is not a house)
                    if (!Physics.Raycast(new Vector3(p.x, y, p.y), dir, out var hit2, reach + 0.6f, SolidMask, QueryTriggerInteraction.Ignore) ||
                        Mathf.Abs(hit2.distance - hit.distance) > 0.8f)
                    {
                        y = y0 + 2.9f;
                        if (!Physics.Raycast(new Vector3(p.x, y, p.y), dir, out hit2, reach + 0.6f, SolidMask, QueryTriggerInteraction.Ignore) ||
                            Mathf.Abs(hit2.distance - hit.distance) > 0.8f) continue;
                    }
                    Vector3 nrm = new Vector3(hit2.normal.x, 0f, hit2.normal.z).normalized;
                    Vector3 at = new Vector3(hit2.point.x, y, hit2.point.z) + nrm * 0.02f;
                    Quaternion rot = Quaternion.LookRotation(nrm, Vector3.up);
                    using (PropKit.At(mb, at, rot))
                    {
                        Vector3 bulb = PropKit.WallLamp(mb);
                        ctx.lampHeads.Add(at + rot * bulb);
                    }
                    Mark(new Vector2(at.x, at.z));
                    return true;
                }
                return false;
            }

            foreach (var r in ctx.world.roads)
            {
                if (r.kind == "track" || r.kind == "bridleway" || IsMallDeck(r.name)) continue;
                bool foot = r.kind == "path" || r.kind == "footway" || r.kind == "steps";
                bool lane = r.kind == "residential" || r.kind == "living_street" || r.kind == "service" || r.kind == "pedestrian";
                var line = Geo.ToPoints(r.pts);
                if (line.Length < 2) continue;
                float length = 0f;
                for (int i = 1; i < line.Length; i++) length += Vector2.Distance(line[i - 1], line[i]);
                var pts = Resample(line, foot ? 28f : 32f, false);
                int first = 1, last = pts.Count - 2;
                if (pts.Count < 3)
                {
                    if (length < 14f) continue;
                    pts = new List<Vector2> { pts[0], (pts[0] + pts[pts.Count - 1]) * 0.5f, pts[pts.Count - 1] };   // a short lane: one in its middle
                    first = last = 1;
                }
                for (int i = first; i <= last; i++)
                {
                    Vector2 p = pts[i];
                    if (ctx.InLake(p) || ctx.LakeDistance(p) < 18f) continue;
                    bool town = HousesNear(ctx, p) >= 3;
                    if (!town && (foot || lane)) continue;      // lanes and paths are lit among the houses only
                    if (!town && i % 4 == 1) continue;           // out of town the main roads keep about the old 42 m
                    if (Near(p, foot ? 13f : 15f)) { near++; continue; }   // one is there already (a junction, the Mall, Tallital)
                    Vector2 tan = (pts[i + 1] - pts[i - 1]).normalized;
                    Vector2 sideV = new Vector2(-tan.y, tan.x) * ((foot || lane) ? 1f : (i % 2 == 0 ? 1f : -1f));
                    float off = r.width * 0.5f + (foot ? 0.45f : 0.7f);
                    bool done = false;
                    foreach (var s in new[] { sideV, -sideV })
                    {
                        Vector2 lp = p + s * off;
                        if (clear.Blocked(lp, 0.4f) || SolidAt(ctx, lp)) continue;   // never on another road, never inside a house
                        Vector3 basePos = new Vector3(lp.x, ctx.H(lp), lp.y);
                        Quaternion rot = Quaternion.LookRotation(new Vector3(-s.x, 0, -s.y));
                        using (PropKit.At(mb, basePos, rot))
                        {
                            Vector3 bulb = PropKit.RoadLamp(mb);
                            ctx.lampHeads.Add(basePos + rot * bulb);
                        }
                        col.Box(basePos + Vector3.up * 2.8f, new Vector3(0.25f, 5.6f, 0.25f), Vector2.zero);
                        Mark(lp);
                        poles++;
                        done = true;
                        break;
                    }
                    if (!done)
                    {
                        if (town && WallLamp(p, sideV, off + 5f)) { walls++; done = true; }
                        else none++;
                    }
                    if (done) Flush(false);
                }
            }
            Flush(true);
            Debug.Log($"[Nainital] street lamps: {poles} on poles, {walls} on house walls, {none} spots with no room, {near} skipped (a lamp there already); lamps total {ctx.lampHeads.Count} (were {before})");
        }

        /// <summary>About half the houses facing a road (within ~22 m) keep a bulb on by the door at night: a little warm
        /// light on the wall towards the road (LampManager lights the nearest few for real).</summary>
        static void BuildPorchLights(WorldCtx ctx)
        {
            Physics.SyncTransforms();
            var parent = new GameObject("Porch Lights").transform;
            parent.SetParent(ctx.worldRoot, false);
            // road points every 4 m on a 12 m grid
            var roads = new Dictionary<Vector2Int, List<Vector2>>();
            foreach (var r in ctx.world.roads)
            {
                if (r.kind == "track" || r.kind == "bridleway") continue;
                foreach (var q in Resample(Geo.ToPoints(r.pts), 4f, false))
                {
                    var k = new Vector2Int(Mathf.FloorToInt(q.x / 12f), Mathf.FloorToInt(q.y / 12f));
                    if (!roads.TryGetValue(k, out var l)) roads[k] = l = new List<Vector2>();
                    l.Add(q);
                }
            }
            bool NearestRoad(Vector2 p, float max, out Vector2 best)
            {
                best = p;
                float bd = max * max;
                int span = Mathf.CeilToInt(max / 12f);
                int gx = Mathf.FloorToInt(p.x / 12f), gz = Mathf.FloorToInt(p.y / 12f);
                bool any = false;
                for (int dx = -span; dx <= span; dx++)
                    for (int dz = -span; dz <= span; dz++)
                        if (roads.TryGetValue(new Vector2Int(gx + dx, gz + dz), out var l))
                            foreach (var q in l) { float d = (q - p).sqrMagnitude; if (d < bd) { bd = d; best = q; any = true; } }
                return any;
            }
            var mb = new MeshBuilder();
            int made = 0, tried = 0, noWall = 0;
            var h = ctx.world.houses;
            for (int i = 0; i + 8 < h.Length; i += 9)
            {
                Vector2 c = new Vector2(h[i], h[i + 1]);
                float w = h[i + 2], d = h[i + 3], yaw = h[i + 4];
                if (ctx.InLake(c)) continue;
                uint hash = (uint)(Mathf.RoundToInt(c.x * 3f) * 73856093 ^ Mathf.RoundToInt(c.y * 3f) * 19349663);
                if (hash % 100 >= 50) continue;   // about half keep a light on
                if (!NearestRoad(c, 22f + Mathf.Max(w, d) * 0.5f, out var road)) continue;
                tried++;
                // the face towards the road
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                Vector2 to = (road - c).normalized;
                Vector3 bestN = Vector3.zero;
                float bestDot = -2f, half = 0f, along = 0f;
                foreach (var (n3, hl, al) in new[] { (q * Vector3.forward, d * 0.5f, w), (q * Vector3.back, d * 0.5f, w), (q * Vector3.right, w * 0.5f, d), (q * Vector3.left, w * 0.5f, d) })
                {
                    float dot = n3.x * to.x + n3.z * to.y;
                    if (dot > bestDot) { bestDot = dot; bestN = n3; half = hl; along = al; }
                }
                Vector3 side = Vector3.Cross(Vector3.up, bestN);
                float shift = ((hash >> 8) % 100 / 100f - 0.5f) * Mathf.Min(along * 0.5f, 2.5f);   // not always the middle of the wall
                Vector2 face = c + new Vector2(bestN.x, bestN.z) * half + new Vector2(side.x, side.z) * shift;
                float y = ctx.H(face) + 2.45f;
                // the house really stands there (some are dropped by the builder): its wall within reach, upright
                Vector3 probe = new Vector3(face.x, y, face.y) + bestN * 1.2f;
                if (!Physics.Raycast(probe, -bestN, out var hit, 2.2f, SolidMask, QueryTriggerInteraction.Ignore) ||
                    hit.collider is TerrainCollider || Mathf.Abs(hit.normal.y) > 0.35f) { noWall++; continue; }
                Vector3 nrm = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                Vector3 at = hit.point + nrm * 0.01f;
                Quaternion rot = Quaternion.LookRotation(nrm, Vector3.up);
                using (PropKit.At(mb, at, rot))
                {
                    Vector3 bulb = PropKit.PorchBulb(mb);
                    ctx.porchLights.Add(at + rot * bulb);
                }
                made++;
                if (made % 400 == 0)
                {
                    ctx.MeshObject("Porch Lights " + made / 400, parent, mb, ctx.mats.atlas, null);
                    mb = new MeshBuilder();
                }
            }
            ctx.MeshObject("Porch Lights last", parent, mb, ctx.mats.atlas, null);
            Debug.Log($"[Nainital] porch lights: {made} (of {tried} houses facing a road and picked; {noWall} without a wall there)");
        }
    }
}

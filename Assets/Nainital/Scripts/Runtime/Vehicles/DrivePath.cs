using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A way a car can really drive from where it stands to a place and heading (owner, 22 Nov 2026: "AI glt mudne pe jab
    /// back krti hai to same direction me phirse chli jaati hai", "gaadi fsgyi hai"): TrafficCar asks for one round a
    /// junction island, into a hairpin, out of where it is wedged and back from pointing the wrong way. A small hybrid A*:
    /// one-metre moves, forward or back, on five locks; each tested with the car's own body box (static things and standing
    /// vehicles; people step aside, moving vehicles are waited for) on ground it can climb.
    /// </summary>
    public static class DrivePath
    {
        public struct Point
        {
            public Vector3 pos, forward;
            public int gear;   // +1 forward, -1 back: the gear that brings it here
        }

        struct Node
        {
            public Vector3 p, f;
            public float yaw, g, h;
            public int parent, gear, depth;
        }

        const float Step = 1f, Cell = 0.6f, YawCell = 15f, Range = 30f, Climb = 0.4f;
        const int MaxExpand = 500;
        static readonly float[] Locks = { 0f, -0.5f, 0.5f, -1f, 1f };
        static readonly List<Node> nodes = new List<Node>(5000);
        static readonly List<int> open = new List<int>(2000);
        static readonly HashSet<long> closed = new HashSet<long>();
        static readonly Collider[] hits = new Collider[16];

        /// <summary>Searches / found (checks: TrafficLab).</summary>
        public static int Asked, Found;

        /// <summary>The way into `result` (the start first). half: the body's half width (x) and half length (z).</summary>
        public static bool Find(Rigidbody self, Vector3 start, Vector3 forward, Vector3 half, float maxSteer, float wheelBase,
                                Vector3 goal, Vector3 goalForward, List<Point> result)
        {
            result.Clear();
            Asked++;
            int groundMask = ~((1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9) | (1 << 2) | (1 << 4));
            goalForward.y = 0f;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-3f || goalForward.sqrMagnitude < 1e-3f) return false;
            if (!Ground(goal, goal.y, 1.6f, groundMask, out float goalY)) return false;
            goal.y = goalY;
            if (!Free(self, goal, goalForward.normalized, half, 1f)) return false;   // something stands on the goal

            if (Ground(start, start.y, 1.6f, groundMask, out float startY)) start.y = startY;
            float goalYaw = Mathf.Atan2(goalForward.x, goalForward.z) * Mathf.Rad2Deg;
            float yaw0 = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float bend = Mathf.Tan(maxSteer * 0.9f * Mathf.Deg2Rad) / Mathf.Max(wheelBase, 1f);   // radians a metre on full lock

            nodes.Clear();
            open.Clear();
            closed.Clear();
            nodes.Add(new Node { p = start, f = forward.normalized, yaw = yaw0, parent = -1, gear = 0, h = Estimate(start, yaw0, goal, goalYaw, bend) });
            open.Add(0);
            int expanded = 0, last = -1;
            while (open.Count > 0 && expanded < MaxExpand)
            {
                int at = 0;
                float least = float.MaxValue;
                for (int k = 0; k < open.Count; k++)
                {
                    float f = nodes[open[k]].g + nodes[open[k]].h;
                    if (f < least) { least = f; at = k; }
                }
                int index = open[at];
                open[at] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                var n = nodes[index];
                if (!closed.Add(Key(n.p - start, n.yaw, n.gear))) continue;
                expanded++;
                Vector3 to = goal - n.p;
                to.y = 0f;
                if (n.gear >= 0 && to.sqrMagnitude < 1.6f * 1.6f && Mathf.Abs(Mathf.DeltaAngle(n.yaw, goalYaw)) < 35f) { last = index; break; }

                for (int gear = 1; gear >= -1; gear -= 2)
                    foreach (float turn in Locks)
                    {
                        float yaw = n.yaw + gear * turn * bend * Step * Mathf.Rad2Deg;
                        float mid = (n.yaw + yaw) * 0.5f * Mathf.Deg2Rad, rad = yaw * Mathf.Deg2Rad;
                        Vector3 p = n.p + new Vector3(Mathf.Sin(mid), 0f, Mathf.Cos(mid)) * (gear * Step);
                        Vector3 f = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                        if ((p - start).sqrMagnitude > Range * Range || closed.Contains(Key(p - start, yaw, gear))) continue;
                        if (!Ground(p, n.p.y, 1.2f, groundMask, out float y) || Mathf.Abs(y - n.p.y) > Climb) continue;
                        p.y = y;
                        // the end that leads must have ground too (no nose over an edge, none into the hillside)
                        Vector3 lead = p + f * (gear * half.z);
                        if (!Ground(lead, y, Climb * half.z + 0.9f, groundMask, out float leadY) || Mathf.Abs(leadY - y) > Climb * half.z + 0.25f) continue;
                        Vector3 pitched = f;
                        pitched.y = (leadY - y) / half.z * gear;
                        // it stands pressed on something: the first two metres may brush it
                        if (!Free(self, p, pitched.normalized, half, n.depth < 2 ? 0.88f : 1f)) continue;
                        float cost = Step * (gear > 0 ? 1f : 1.8f) + Mathf.Abs(turn) * 0.1f + (n.gear != 0 && gear != n.gear ? 2.5f : 0f);
                        nodes.Add(new Node { p = p, f = f, yaw = yaw, g = n.g + cost, h = Estimate(p, yaw, goal, goalYaw, bend), parent = index, gear = gear, depth = n.depth + 1 });
                        open.Add(nodes.Count - 1);
                    }
            }
            if (last < 1) return false;
            for (int k = last; k >= 0; k = nodes[k].parent) result.Add(new Point { pos = nodes[k].p, forward = nodes[k].f, gear = nodes[k].gear });
            result.Reverse();
            Found++;
            return true;
        }

        static float Estimate(Vector3 p, float yaw, Vector3 goal, float goalYaw, float bend)
        {
            Vector3 d = goal - p;
            d.y = 0f;
            return 1.4f * (d.magnitude + Mathf.Abs(Mathf.DeltaAngle(yaw, goalYaw)) * Mathf.Deg2Rad / Mathf.Max(bend, 0.02f) * 0.4f);
        }

        static long Key(Vector3 rel, float yaw, int gear)
        {
            long x = Mathf.RoundToInt(rel.x / Cell) + 512, z = Mathf.RoundToInt(rel.z / Cell) + 512;
            long a = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / YawCell) % 24;
            return (x << 24) | (z << 10) | (a << 2) | (long)(gear + 1);
        }

        static bool Ground(Vector3 p, float near, float reach, int mask, out float y)
        {
            y = near;
            if (!Physics.Raycast(new Vector3(p.x, near + reach, p.z), Vector3.down, out var hit, reach * 2f, mask, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < 0.7f) return false;
            y = hit.point.y;
            return true;
        }

        /// <summary>The body's box clear, 0.4 to 1.5 m over the ground there.</summary>
        static bool Free(Rigidbody self, Vector3 p, Vector3 f, Vector3 half, float shrink)
        {
            int n = Physics.OverlapBoxNonAlloc(p + Vector3.up * 0.95f, new Vector3(half.x * shrink, 0.55f, half.z * shrink), hits,
                                               Quaternion.LookRotation(f), ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                if (c is TerrainCollider) continue;   // the ground is the raycasts' business
                var body = c.attachedRigidbody;
                if (body && body == self) continue;
                if (body && !body.isKinematic && body.linearVelocity.sqrMagnitude > 2.25f) continue;   // driving: waited for, not planned round
                return false;
            }
            return true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Where a traffic vehicle goes at the end of its road, chosen ahead of time so it can signal (owner, 21 Nov 2026:
    /// "gaadi agar aage kahin turn hogi to indicator degi pehle"). TrafficCar and TrafficBike plan the next road ~30 m
    /// before the end, show the indicator of the turn it makes (more than 25 degrees; a turn round = right, as on Indian
    /// roads), take that road when they get there and keep signalling a moment into it.
    /// </summary>
    public static class RoadChoice
    {
        public const float PlanAhead = 30f, TurnAngle = 25f, HoldAfter = 2.5f;

        public const float HairpinAngle = 115f;

        /// <summary>The ways onward from this end of `road` (end = 0 or the last index): (road, entry index, direction),
        /// never the wrong way down a one-way road. A hairpin (over HairpinAngle: back down the road just driven, the
        /// acute arm of a Y) is offered only where there is nothing gentler, as a driver would choose - unless `all` (a
        /// route needs it).</summary>
        public static List<(int r, int i, int d)> Onward(RoadNetwork net, int road, int endIdx, bool all = false)
        {
            var gentle = new List<(int r, int i, int d)>();
            var sharp = new List<(int r, int i, int d)>();
            int e = endIdx <= 0 ? 0 : 1;
            var links = net.Links(road, e);
            if (links == null) return gentle;
            var p = net.roads[road].pts;
            int end = e == 0 ? 0 : p.Length - 1, before = e == 0 ? Mathf.Min(3, p.Length - 1) : Mathf.Max(0, p.Length - 4);
            Vector3 hin = p[end] - p[before];
            hin.y = 0f;
            foreach (var (r, i) in links)
            {
                if (r == road) continue;
                var q = net.roads[r].pts;
                int ow = net.roads[r].oneWay;
                for (int d = 1; d >= -1; d -= 2)
                {
                    if (ow != 0 && d != ow) continue;
                    if (d > 0 ? i >= q.Length - 2 : i <= 1) continue;   // no road left that way / its exit end
                    Vector3 hout = q[Mathf.Clamp(i + 3 * d, 0, q.Length - 1)] - q[i];
                    hout.y = 0f;
                    (Vector3.Angle(hin, hout) > HairpinAngle ? sharp : gentle).Add((r, i, d));
                }
            }
            if (all) gentle.AddRange(sharp);
            return gentle.Count > 0 ? gentle : sharp;
        }

        /// <summary>The indicator for going from the end of `road` (driving `dir`) onto `next`: -1 left, +1 right, 0 on
        /// (straighter than TurnAngle).</summary>
        public static int Sign(RoadNetwork net, int road, int dir, (int r, int i, int d) next)
        {
            var p = net.roads[road].pts;
            int end = dir > 0 ? p.Length - 1 : 0, before = Mathf.Clamp(end - dir, 0, p.Length - 1);
            Vector3 hin = p[end] - p[before];
            var q = net.roads[next.r].pts;
            int a = Mathf.Clamp(next.i, 0, q.Length - 1), b = Mathf.Clamp(next.i + next.d, 0, q.Length - 1);
            Vector3 hout = q[b] - q[a];
            hin.y = hout.y = 0f;
            if (hin.sqrMagnitude < 1e-4f || hout.sqrMagnitude < 1e-4f) return 0;
            float ang = Vector3.SignedAngle(hin, hout, Vector3.up);   // + = clockwise from above = a right turn
            return Mathf.Abs(ang) < TurnAngle ? 0 : (ang > 0f ? 1 : -1);
        }
    }

    /// <summary>One vehicle's plan for the end of its road and the indicator it shows (used by TrafficCar / TrafficBike).</summary>
    public class TurnPlan
    {
        public (int r, int i, int d) next = (-1, 0, 0);
        /// <summary>A road this vehicle has to take next (a route: TrafficCar.FollowRoute), with the way to drive it; null
        /// = pick one at random.</summary>
        public (int r, int d)? want;
        int road = -1, dir;
        bool planned, uturn;
        int sign, holdSign;
        float hold;
        public int Signal => planned ? sign : hold > 0f ? holdSign : 0;

        /// <summary>Every frame while driving: plans once the end of the road is PlanAhead away.</summary>
        public void Tick(RoadNetwork net, int curRoad, int curDir, Vector3 pos, float dt)
        {
            hold -= dt;
            if (planned && (road != curRoad || dir != curDir)) planned = false;   // turned round / put back on a lane
            if (planned) return;
            var p = net.roads[curRoad].pts;
            int end = curDir > 0 ? p.Length - 1 : 0;
            Vector3 to = p[end] - pos; to.y = 0f;
            if (to.magnitude > RoadChoice.PlanAhead) return;
            var ok = RoadChoice.Onward(net, curRoad, end);
            road = curRoad; dir = curDir; planned = true;
            (int r, int i, int d)? wanted = null;
            if (want.HasValue)
                foreach (var o in RoadChoice.Onward(net, curRoad, end, true))
                    if (o.r == want.Value.r && o.d == want.Value.d) { wanted = o; break; }
            if (wanted.HasValue) { next = wanted.Value; uturn = false; sign = RoadChoice.Sign(net, curRoad, curDir, next); }
            else if (ok.Count > 0) { next = ok[Random.Range(0, ok.Count)]; uturn = false; sign = RoadChoice.Sign(net, curRoad, curDir, next); }
            else { next = (-1, 0, 0); uturn = net.roads[curRoad].oneWay == 0; sign = uturn ? 1 : 0; }
        }

        /// <summary>At the end of the road: the planned road if there is one for this road (else null), and the indicator
        /// carries on a moment into it.</summary>
        public (int r, int i, int d)? Take(int curRoad, int curDir)
        {
            if (!planned || road != curRoad || dir != curDir) return null;
            planned = false;
            holdSign = sign; hold = RoadChoice.HoldAfter;
            if (next.r < 0) return null;
            return next;
        }

        /// <summary>A turn off the middle of the road coming up (a route): its indicator.</summary>
        public void Branching(int turnSign)
        {
            planned = false;
            holdSign = turnSign;
            hold = RoadChoice.HoldAfter;
        }

        /// <summary>Turning round (a three-point turn): the right indicator through it.</summary>
        public void TurningRound() { planned = false; holdSign = 1; hold = 0.5f; }
    }
}

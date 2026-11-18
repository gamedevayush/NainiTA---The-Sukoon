using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A townsperson on a Splendor or a scooty in the traffic (user, 2026-11-18: "normal traffic me bhi ye bikes or
    /// scooty add krdena"). Keeps well to the left of the lane, goes on at road ends like the cars (TrafficCar), slows
    /// and waits for anything in front, stops short of an accident (IncidentManager) and moves over for a siren behind.
    /// At a dead end it simply turns round. Hit by the player's car the rider is thrown off (may die: the police and
    /// the ambulance come) and the bike falls over.
    /// </summary>
    [RequireComponent(typeof(BikeRide))]
    public class TrafficBike : MonoBehaviour
    {
        public RoadNetwork net;
        public float cruise = 10f;

        int road, idx, dir;
        float stuck, hornCd, pull;
        bool down;
        BikeRide bike;
        NpcWalker rider;
        static int obstacleMask = -1;

        public bool Down => down;

        public void Begin(RoadNetwork n, int roadIndex, int startIdx, int direction)
        {
            net = n;
            road = roadIndex;
            idx = startIdx;
            dir = n.roads[roadIndex].oneWay != 0 ? n.roads[roadIndex].oneWay : direction;
            bike = GetComponent<BikeRide>();
            if (obstacleMask < 0) obstacleMask = (1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9);
            bike.Place(Lane(idx), Lane(idx + dir) - Lane(idx));
            var pm = PedestrianManager.Instance;
            if (pm) rider = pm.MakeRider(bike.model, bike.SeatLocal);
            bike.OnCrash += Thrown;
        }

        Vector3[] Pts => net.roads[road].pts;

        Vector3 Lane(int i)
        {
            var pts = Pts;
            i = Mathf.Clamp(i, 0, pts.Length - 1);
            int j = Mathf.Clamp(i + dir, 0, pts.Length - 1);
            if (j == i) j = Mathf.Clamp(i - dir, 0, pts.Length - 1);
            Vector3 d = (j > i ? pts[j] - pts[i] : pts[i] - pts[j]) * (dir > 0 ? 1 : -1);
            d.y = 0;
            if (d.sqrMagnitude < 1e-4f) return pts[i];
            Vector3 left = Vector3.Cross(d.normalized, Vector3.up);
            float w = net.roads[road].width;
            float off = net.roads[road].oneWay != 0 ? w * 0.28f : Mathf.Max(1.5f, w * 0.36f);   // two-wheelers keep to the edge
            return pts[i] + left * (off + pull * 0.6f);
        }

        void Advance()
        {
            idx += dir;
            var pts = Pts;
            if (idx > 0 && idx < pts.Length - 1) return;
            var links = net.Links(road, idx <= 0 ? 0 : 1);
            var ok = new List<(int r, int i, int d)>();
            if (links != null)
                foreach (var (r, i) in links)
                {
                    if (r == road) continue;
                    var q = net.roads[r].pts;
                    int ow = net.roads[r].oneWay;
                    if (ow != 0)
                    {
                        if ((ow > 0 && i >= q.Length - 2) || (ow < 0 && i <= 1)) continue;
                        ok.Add((r, i, ow));
                    }
                    else ok.Add((r, i, i <= 0 ? 1 : i >= q.Length - 1 ? -1 : (Random.value < 0.5f ? 1 : -1)));
                }
            if (ok.Count > 0)
            {
                var c = ok[Random.Range(0, ok.Count)];
                road = c.r;
                idx = c.i;
                dir = c.d;
            }
            else if (net.roads[road].oneWay == 0) { dir = -dir; idx = Mathf.Clamp(idx, 0, pts.Length - 1); }   // a bike just turns round
            else Destroy(gameObject);
        }

        Vector3 Aim(float look)
        {
            Vector3 prev = transform.position;
            for (int k = 1; k < 12; k++)
            {
                int i = idx + dir * k;
                if (i < 0 || i >= Pts.Length) break;
                Vector3 q = Lane(i);
                float d = Vector3.Distance(new Vector3(prev.x, 0, prev.z), new Vector3(q.x, 0, q.z));
                if (d >= look) return prev + (q - prev) * (look / Mathf.Max(d, 0.01f));
                look -= d;
                prev = q;
            }
            return prev;
        }

        void FixedUpdate()
        {
            if (!net || !bike || down) return;
            if (bike.Crashed) return;
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            for (int guard = 0; guard < 4; guard++)
            {
                Vector3 nx = Lane(idx + dir) - pos;
                nx.y = 0;
                if (nx.magnitude < 2f || (nx.magnitude < 7f && Vector3.Dot(nx, bike.Forward) < 0f)) { Advance(); if (!this || down) return; }
                else break;
            }
            float speed = bike.Speed;
            Vector3 ahead = Lane(idx + dir * 3) - pos;
            ahead.y = 0;
            float bend = Vector3.Angle(bike.Forward, ahead);
            float target = Mathf.Lerp(cruise, 3.5f, Mathf.Clamp01(bend / 75f));

            // something in front
            float look = 4f + speed * 1.1f;
            Vector3 from = pos + Vector3.up * 0.8f + bike.Forward * (bike.length * 0.5f + 0.2f);
            if (Physics.SphereCast(from, 0.45f, bike.Forward, out var hit, look, obstacleMask, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(transform))
                target = hit.distance < 2.5f ? 0f : Mathf.Min(target, (hit.distance - 2f) * 0.8f);

            // an accident ahead: stop short; a siren behind: over to the left, slow
            if (IncidentManager.HoldAhead(pos, bike.Forward, out float hd)) target = Mathf.Min(target, Mathf.Max(0f, (hd - 8f) * 0.6f));
            float want = SirenNear(pos, bike.Forward) ? 1f : 0f;
            pull = Mathf.MoveTowards(pull, want, dt * 0.8f);
            if (want > 0f) target = Mathf.Min(target, 3f);

            bike.Drive(Aim(Mathf.Clamp(speed * 0.8f, 3f, 9f)), target, dt);

            if (speed < 0.3f) stuck += dt; else stuck = 0f;
            hornCd -= dt;
            if (stuck > 2f && hornCd <= 0f && AudioManager.Instance) { AudioManager.Instance.HornAt(pos + Vector3.up, false); hornCd = Random.Range(5f, 10f); }
            if (stuck > 30f && !Seen()) Destroy(gameObject);
        }

        /// <summary>An emergency vehicle with its siren on, close behind or coming at us.</summary>
        public static bool SirenNear(Vector3 pos, Vector3 fwd)
        {
            foreach (var e in EmergencyLights.All)
            {
                if (!e || !e.siren) continue;
                Vector3 rel = e.transform.position - pos;
                rel.y = 0f;
                float d = rel.magnitude;
                if (d > 32f || d < 0.5f) continue;
                Vector3 ef = e.transform.forward;
                ef.y = 0f;
                float along = Vector3.Dot(rel, fwd);
                if (along < -1f && Vector3.Dot(ef, fwd) > 0.3f) return true;      // behind, going our way
                if (along > 1f && Vector3.Dot(ef, fwd) < -0.3f) return true;      // ahead, coming at us
            }
            return false;
        }

        bool Seen()
        {
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            return vp.z > 0f && vp.z < 120f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }

        /// <summary>The player's car hit the bike: the rider flies off.</summary>
        void Thrown(Vector3 carVelocity, GameObject by)
        {
            down = true;
            if (!rider) { Debug.Log($"[Nainital] bike {name} knocked over, no rider"); return; }
            var r = rider;
            rider = null;
            Vector3 side = Vector3.Cross(Vector3.up, carVelocity.sqrMagnitude > 0.01f ? carVelocity.normalized : transform.forward);
            r.LeaveCar(transform.position + side * 0.6f, side);
            r.Knock(carVelocity * 0.7f + Vector3.up * 4f);
            var h = r.GetComponent<Health>();
            bool hurt = h && h.Damage(carVelocity.magnitude * 4f, transform.position, by);
            Debug.Log($"[Nainital] bike {name} knocked over at {transform.position:F0} ({carVelocity.magnitude:F0} m/s): rider {r.npcName} hurt {hurt} hp {(h ? h.current : -1):F0}");
        }

        void OnDestroy()
        {
            if (rider) Destroy(rider.gameObject);
        }
    }
}

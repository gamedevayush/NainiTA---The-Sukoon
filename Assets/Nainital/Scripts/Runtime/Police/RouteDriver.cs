using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Drives a real car (CarController physics, like TrafficCar) to a goal along the roads (RoadRoute): the police Bolero
    /// and the 108 ambulance. Steers for a point up the route, slows for bends and for anything in front (people
    /// always; cars it waits behind, sounding its siren: traffic pulls over), backs up and tries again when stuck, and
    /// when nobody is looking hops past a jam it cannot clear. Near a goal that is off the road (the player in a park)
    /// it can leave the road and go straight for it (offRoad).
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class RouteDriver : MonoBehaviour
    {
        public float cruise = 15f;
        public float arriveDist = 7f;
        public bool offRoad;        // allowed to leave the road for the last stretch
        public bool stop;           // brake and stay
        public bool ram;            // a car in front that is the player's: keep on (police chase)

        public Vector3 Goal { get; private set; }
        public bool HasGoal { get; private set; }
        public bool Arrived { get; private set; }
        public float Speed => car ? car.ForwardSpeed : 0f;
        public float Blocked => blockedT;
        public bool HasRoute => route.Count > 1;
        public float RouteLeft { get; private set; }
        public string State => $"ri {ri}/{route.Count} stuck {stuckT:F1} blocked {blockedT:F1} rev {reverseT:F1} avoid {avoidT:F1}{(avoid > 0 ? "R" : "L")} hops {hopCount:F0} thr {(car ? car.aiThrottle : 0):F2} steer {(car ? car.aiSteer : 0):F0} hb {(car ? car.aiHandbrake : 0):F0} at {transform.position:F0} fwd {transform.forward:F1}";

        readonly List<Vector3> route = new List<Vector3>();
        int ri;
        float replan, blockedT, stuckT, reverseT, hopCount, avoidT, avoid, askT;
        float reverseSteer;
        CarController car;
        Rigidbody rb;
        VehicleFactory.Spec spec;
        static int obstacleMask = -1;

        public void Begin()
        {
            car = GetComponent<CarController>();
            rb = GetComponent<Rigidbody>();
            spec = car.Spec;
            var info = GetComponent<VehicleInfo>();
            if (info) { spec.length = info.length; spec.width = info.width; }
            car.ai = true;
            rb.isKinematic = false;
            if (obstacleMask < 0) obstacleMask = (1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9);
            replan = Random.Range(0f, 0.5f);
        }

        /// <summary>Head for g. A goal that moved far (or `now`) is planned again at once.</summary>
        public void SetGoal(Vector3 g, bool now = false)
        {
            bool far = !HasGoal || (g - Goal).sqrMagnitude > 20f * 20f;
            Goal = g;
            HasGoal = true;
            if (far || now) replan = 0f;
        }

        public void ClearGoal() { HasGoal = false; route.Clear(); Arrived = false; }

        void Plan()
        {
            replan = Random.Range(2.6f, 3.4f);
            if (!RoadRoute.ReadyAny()) return;
            if (!RoadRoute.Find(transform.position, Goal, route)) { route.Clear(); return; }
            // start from the route point ahead of the car, not one behind it
            ri = 0;
            Vector3 pos = transform.position;
            float best = float.MaxValue;
            for (int i = 0; i < Mathf.Min(route.Count, 12); i++)
            {
                float d = Flat(route[i] - pos).sqrMagnitude;
                if (d < best) { best = d; ri = i; }
            }
            if (ri + 1 < route.Count && Vector3.Dot(Flat(route[ri] - pos), transform.forward) < 0f) ri++;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        float SteerTo(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(local.z, 0.1f)) * Mathf.Rad2Deg * 1.15f, -spec.steer, spec.steer);
        }

        Vector3 Aim(Vector3 pos, float look)
        {
            Vector3 prev = pos;
            for (int i = ri; i < route.Count; i++)
            {
                float d = Flat(route[i] - prev).magnitude;
                if (d >= look) return prev + (route[i] - prev) * (look / Mathf.Max(d, 0.01f));
                look -= d;
                prev = route[i];
            }
            return offRoad ? Goal : prev;
        }

        void OnCollisionEnter(Collision c) => Touched(c);
        void OnCollisionStay(Collision c) => Touched(c);

        /// <summary>It hit something: off the accelerator, brake, back out on the lock away from it (owner, 22 Nov 2026:
        /// "thuke ruke or back krke nikaal le"). A police car ramming the player's car in a chase is meant to.</summary>
        void Touched(Collision c)
        {
            if (!car || stop || !HasGoal || Arrived) return;
            int layer = c.collider.gameObject.layer;
            if (layer == 8 || layer == 9) return;
            if (ram && c.rigidbody && CarController.Driving && c.rigidbody.gameObject == CarController.Driving.gameObject) return;
            float end = reverseT > 0f ? -1f : 1f;
            if (end > 0f && car.aiThrottle <= 0.05f && car.ForwardSpeed < 0.6f) return;
            for (int i = 0; i < c.contactCount; i++)
            {
                var k = c.GetContact(i);
                if (k.normal.y > 0.6f) continue;
                Vector3 local = transform.InverseTransformPoint(k.point);
                if (local.z * end < spec.length * 0.3f || Vector3.Dot(k.normal, transform.forward) * end > -0.35f) continue;
                if (end < 0f) reverseT = 0f;   // as far back as there is room
                else
                {
                    reverseT = 1.8f;
                    reverseSteer = (Mathf.Abs(local.x) > 0.25f ? Mathf.Sign(local.x) : -Mathf.Sign(car.aiSteer == 0f ? 1f : car.aiSteer)) * spec.steer;
                    stuckT = 0f;
                    avoidT = 0f;
                    hopCount++;
                }
                car.aiThrottle = 0f;
                car.aiHandbrake = 1f;
                return;
            }
        }

        void Brake()
        {
            float fwd = car.ForwardSpeed;
            car.aiSteer = 0f;
            car.aiThrottle = Mathf.Abs(fwd) > 0.4f ? -Mathf.Sign(fwd) * Mathf.Clamp01(Mathf.Abs(fwd) * 0.4f + 0.3f) : 0f;
            car.aiHandbrake = Mathf.Abs(fwd) < 1.5f ? 1f : 0f;
        }

        void FixedUpdate()
        {
            if (!car) return;
            if (car.wrecked) { car.aiThrottle = 0f; car.aiHandbrake = 1f; return; }
            if (stop || !HasGoal) { Brake(); return; }
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            float fwd = car.ForwardSpeed, speed = Mathf.Abs(fwd);

            replan -= dt;
            if (replan <= 0f) Plan();

            // backing up out of a jam
            if (reverseT > 0f)
            {
                reverseT -= dt;
                car.aiHandbrake = 0f;
                car.aiSteer = reverseSteer;
                car.aiThrottle = fwd > 0.5f ? -1f : -0.55f;
                return;
            }

            // along the route: drop the points passed
            while (ri < route.Count - 1)
            {
                Vector3 to = Flat(route[ri] - pos);
                if (to.magnitude < 4f || (to.magnitude < 12f && Vector3.Dot(to, transform.forward) < 0f) ||
                    (RoadRoute.Net && RoadRoute.Net.InIsland(route[ri], pos, spec.width * 0.5f))) ri++;
                else break;
            }
            float toGoal = Flat(Goal - pos).magnitude;
            Vector3 end = route.Count > 0 ? route[route.Count - 1] : Goal;
            float toEnd = Flat(end - pos).magnitude;
            bool direct = route.Count < 2 || (offRoad && (toGoal < 30f || ri >= route.Count - 1));
            Vector3 aim = direct ? Goal : Aim(pos, Mathf.Clamp(speed * 0.9f, 5f, 14f));
            if (!direct && RoadRoute.Net) aim = RoadRoute.Net.RoundIslands(pos, aim, spec.width * 0.5f);
            float left = direct ? toGoal : toEnd + (offRoad ? Flat(Goal - end).magnitude : 0f);
            RouteLeft = left;
            Arrived = left < arriveDist || (!offRoad && toGoal < arriveDist);
            if (Arrived) { Brake(); blockedT = 0f; return; }

            car.aiSteer = SteerTo(aim);
            // bends ahead set the speed; slow down near the goal
            Vector3 far = direct ? Goal : Aim(pos, 22f);
            float bend = Vector3.Angle(transform.forward, Flat(far - pos));
            float target = Mathf.Lerp(cruise, 4.5f, Mathf.Clamp01(bend / 70f));
            target = Mathf.Min(target, 2.5f + left * 0.45f);

            // something in front: people it waits for; a car it goes round if there is room (on the right first, the
            // passing side), else waits and asks it to make way (TrafficCar.MakeWay)
            float look = 5f + speed * 1.3f;
            Vector3 from = pos + Vector3.up * 1f + transform.forward * (spec.length * 0.5f + 0.3f);
            bool wall = false;
            if (Physics.SphereCast(from, 0.9f, transform.forward, out var hit, look, obstacleMask, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(transform))
            {
                var other = hit.rigidbody ? hit.rigidbody.GetComponent<CarController>() : null;
                bool playerCar = other && other == CarController.Driving;
                bool person = hit.collider.gameObject.layer == 8 || hit.collider.gameObject.layer == 9;
                if (ram && playerCar) target = Mathf.Min(target, other.Speed + hit.distance * 0.8f);
                else
                {
                    if (!person && avoidT <= 0f)
                    {
                        float side = Clear(1f) ? 1f : Clear(-1f) ? -1f : 0f;
                        if (side != 0f) { avoid = side; avoidT = 1.6f; }
                    }
                    if (avoidT <= 0f)
                    {
                        target = hit.distance < 4f ? 0f : Mathf.Min(target, (hit.distance - 3f) * 0.7f);
                        wall = true;
                        if (!person && blockedT > 3.5f && askT <= 0f && hit.rigidbody)
                        {
                            var tc = hit.rigidbody.GetComponent<TrafficCar>();
                            if (tc) { tc.MakeWay(pos); askT = 5f; }
                        }
                    }
                }
            }
            askT -= dt;
            if (avoidT > 0f)   // round it: off to the side for a moment, slowly
            {
                avoidT -= dt;
                car.aiSteer = Mathf.Clamp(car.aiSteer + avoid * 22f, -spec.steer, spec.steer);
                target = Mathf.Min(target, 5f);
            }

            float err = target - fwd;
            car.aiThrottle = Mathf.Clamp(err * 0.4f, -1f, 1f);
            if (target < 0.3f && speed < 1.2f) { car.aiThrottle = 0f; car.aiHandbrake = 1f; }
            else car.aiHandbrake = 0f;

            // stuck: held up by a car in front for long, or pushing into something
            if (speed < 0.6f) { if (wall) blockedT += dt; else stuckT += dt; }
            else { blockedT = Mathf.Max(0f, blockedT - dt * 2f); stuckT = 0f; }
            if (stuckT > 1.2f)   // pressing on and not moving: back up on the other lock
            {
                stuckT = 0f;
                reverseT = 1.4f;
                reverseSteer = -Mathf.Sign(car.aiSteer == 0f ? 1f : car.aiSteer) * spec.steer;
                hopCount++;
            }
            if ((blockedT > 9f || hopCount > 3) && !Seen())   // a jam it cannot clear and nobody sees: on past it
            {
                Hop();
                blockedT = 0f;
                hopCount = 0;
            }
        }

        /// <summary>Room to pull out to this side (+1 right, -1 left): nothing within 8 m that way, and ground there
        /// at about the road's height (not a drop off the hillside).</summary>
        bool Clear(float side)
        {
            Vector3 dir = Quaternion.AngleAxis(28f * side, Vector3.up) * transform.forward;
            Vector3 from = transform.position + Vector3.up * 1f + transform.forward * (spec.length * 0.4f);
            if (Physics.SphereCast(from, spec.width * 0.45f, dir, out var h, 8f, ~((1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore) &&
                !h.collider.transform.IsChildOf(transform) && !(h.collider is TerrainCollider && Vector3.Angle(h.normal, Vector3.up) < 30f))
                return false;
            Vector3 probe = from + dir * 6f;
            if (!Physics.Raycast(probe + Vector3.up * 2f, Vector3.down, out var g, 6f, ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer)), QueryTriggerInteraction.Ignore))
                return false;
            return Mathf.Abs(g.point.y - transform.position.y) < 1.2f;
        }

        bool Seen()
        {
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            return vp.z > 0f && vp.z < 140f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }

        /// <summary>Put the car 25-35 m further along its route, facing on (only when off screen).</summary>
        void Hop()
        {
            if (route.Count < 2) return;
            int j = ri;
            float d = 0f;
            while (j < route.Count - 1 && d < 30f) { d += Vector3.Distance(route[j], route[j + 1]); j++; }
            Vector3 p = route[j];
            Vector3 f = j < route.Count - 1 ? route[j + 1] - route[j] : route[j] - route[Mathf.Max(0, j - 1)];
            f.y = 0f;
            if (Physics.CheckBox(p + Vector3.up * 1.2f, new Vector3(spec.width * 0.5f, 0.8f, spec.length * 0.5f), Quaternion.LookRotation(f.sqrMagnitude > 0.01f ? f : transform.forward), 1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore))
                return;   // something there too; try later
            rb.position = p + Vector3.up * 0.5f;
            rb.rotation = Quaternion.LookRotation(f.sqrMagnitude > 0.01f ? f.normalized : transform.forward, Vector3.up);
            rb.linearVelocity = rb.rotation * Vector3.forward * 4f;
            rb.angularVelocity = Vector3.zero;
            ri = j;
        }
    }
}

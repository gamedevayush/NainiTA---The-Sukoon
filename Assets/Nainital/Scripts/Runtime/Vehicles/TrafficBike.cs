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
        float stuck, hornCd, pull, dipperCd;
        readonly TurnPlan plan = new TurnPlan();
        VehicleLamps lampsRef;
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
            bike.avoidStatics = true;   // it steers round boards, posts, parapets (it moves itself)
            if (obstacleMask < 0) obstacleMask = (1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9);
            bike.Place(Lane(idx), Lane(idx + dir) - Lane(idx));
            var pm = PedestrianManager.Instance;
            var vp = bike.Parts;
            if (pm && vp && vp.HasSetup)
            {
                // the unbranded set: the rider on the setup's seat, hands on the grips, feet on the pegs; one in three
                // carries someone behind (a pillion, or side-saddle - whatever the setup's second seat is)
                rider = pm.MakeSeated(vp, 0);
                if (vp.SeatCount > 1 && Random.value < 0.35f) pillion = pm.MakeSeated(vp, 1);
            }
            else if (pm) rider = pm.MakeRider(bike.model, bike.SeatLocal);
            bike.OnCrash += Thrown;
            if (!GetComponent<PlayerBike>()) gameObject.AddComponent<PlayerBike>();   // the player can take it (E)
        }

        PlayerBike giveTo;
        PlayerController taker;
        float pullT;

        /// <summary>The player asked for this bike (PlayerBike): pull up, everyone off, then it is the player's.</summary>
        public void RequestTakeover(PlayerBike pb, PlayerController p) { giveTo = pb; taker = p; pullT = 0f; }

        /// <summary>Stopped for the player: the rider and the pillion get off at their way out (the owner's rule: whoever
        /// gets in, everyone else gets out), then the player mounts and the traffic lets the bike go.</summary>
        void HandOver()
        {
            var pb = giveTo; var p = taker;
            giveTo = null; taker = null;
            if (!pb || !p || (p.transform.position - transform.position).sqrMagnitude > 12f * 12f) { if (pb) pb.Released(); return; }
            var vp = bike.Parts;
            string who = null;
            foreach (var (n, seat) in new[] { (rider, 0), (pillion, 1) })
            {
                if (!n) continue;
                Vector3 face = -transform.right;
                Vector3 at = vp && vp.HasSetup ? vp.WayOut(seat, out face) : transform.position - transform.right * 0.9f + transform.forward * (seat == 0 ? 0f : -0.6f);
                n.LeaveCar(at, face);
                if (who == null) who = n.npcName;
            }
            rider = null; pillion = null;   // theirs no more (OnDestroy would take them with it)
            if (who != null)
            {
                string[] lines = { "Arey! Meri bike!", "Bhai sahab, ye kya kar rahe ho?", "Le jao, le jao... police ko bataunga!", "Oye! Ruk!" };
                GameEvents.ShowToast(who + ": \"" + lines[Random.Range(0, lines.Length)] + "\"", 2.5f);
            }
            if (lampsRef) lampsRef.signal = 0;
            pb.Mount(p);
            Destroy(this);   // TrafficManager drops it; BikeRide stays, the player rides it now
        }

        NpcWalker pillion;

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
            return pts[i] + left * (off + net.roads[road].Shift(dir, i) + pull * 0.6f - passOff);
        }

        // round a vehicle standing in the way that will not move (TrafficCar.WontMove), on its right (owner, 21 Nov 2026:
        // "gaadi atke na")
        Rigidbody passing;
        float passOff, passWant, passT, waitT;

        float slowT;
        static readonly Collider[] rightHits = new Collider[16];

        /// <summary>Nobody (a vehicle, the player) in the space to the right of its lane for `d` m ahead, but the one it passes.</summary>
        bool RightClear(Vector3 pos, float d, Rigidbody passed)
        {
            Vector3 f = bike.Forward, right = Vector3.Cross(Vector3.up, f);
            Vector3 c = pos + f * (d * 0.5f) + right * 2f + Vector3.up;
            int n = Physics.OverlapBoxNonAlloc(c, new Vector3(1.6f, 1.5f, d * 0.5f), rightHits, Quaternion.LookRotation(f),
                                               (1 << VehicleFactory.VehicleLayer) | (1 << 8), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var o = rightHits[i].attachedRigidbody;
                Transform t = o ? o.transform : rightHits[i].transform;
                if (t == transform || t.IsChildOf(transform) || (passed && o == passed)) continue;
                return false;
            }
            return true;
        }

        void StartPass(Rigidbody o, Vector3 pos)
        {
            var info = o.GetComponent<VehicleInfo>();
            float bw = info ? info.width : (o.GetComponent<BikeRide>() ? 0.8f : 1.9f);
            Vector3 right = Vector3.Cross(Vector3.up, bike.Forward);
            float want = Vector3.Dot(o.position - pos, right) + bw * 0.5f + 0.9f;
            if (want <= 0.3f) return;
            var r = net.roads[road];
            passWant = Mathf.Min(want, r.oneWay != 0 ? r.width * 0.55f : Mathf.Max(1.5f, r.width * 0.36f) * 2f);
            passing = o;
            passT = 0f;
            waitT = 0f;
            TrafficCar.Passes++;
        }

        void Advance()
        {
            idx += dir;
            var pts = Pts;
            if (idx > 0 && idx < pts.Length - 1) return;
            // the road planned (and signalled) 30 m back, else one picked now
            var planned = plan.Take(road, dir);
            if (planned.HasValue) { road = planned.Value.r; idx = planned.Value.i; dir = planned.Value.d; return; }
            var ok = RoadChoice.Onward(net, road, idx);
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
            if (!net || !bike) return;
            if (down) { if (fallenRider) GetBackOn(Time.fixedDeltaTime); return; }
            if (bike.Crashed) { if (giveTo) { giveTo.Released(); giveTo = null; } return; }
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            if (giveTo)   // pulling up for the player
            {
                bike.Drive(transform.position + bike.Forward * 4f, 0f, dt);
                if (lampsRef) lampsRef.signal = -1;   // over to the left
                if (bike.Speed < 0.3f && (pullT += dt) > 0.35f) HandOver();
                return;
            }
            for (int guard = 0; guard < 4; guard++)
            {
                Vector3 lp = Lane(idx + dir), nx = lp - pos;
                nx.y = 0;
                if (nx.magnitude < 2f || (nx.magnitude < 7f && Vector3.Dot(nx, bike.Forward) < 0f) || net.InIsland(lp, pos, 0.5f)) { Advance(); if (!this || down) return; }
                else break;
            }
            // the indicator for the turn planned ahead (owner, 21 Nov 2026)
            if (!lampsRef) lampsRef = GetComponent<VehicleLamps>();
            plan.Tick(net, road, dir, pos, dt);
            if (lampsRef) lampsRef.signal = plan.Signal;
            float speed = bike.Speed;
            Vector3 ahead = Lane(idx + dir * 3) - pos;
            ahead.y = 0;
            float bend = Vector3.Angle(bike.Forward, ahead);
            float target = Mathf.Lerp(cruise, 3.5f, Mathf.Clamp01(bend / 75f));

            // going round a standing vehicle: until it is behind
            if (passing)
            {
                passT += dt;
                if (Vector3.Dot(passing.position - pos, bike.Forward) < -(bike.length * 0.5f + 3f) || passT > 12f) passing = null;
            }
            passOff = Mathf.MoveTowards(passOff, passing ? passWant : 0f, dt * 1.6f);

            // something in front
            float look = 4f + speed * 1.1f;
            Vector3 from = pos + Vector3.up * 0.8f + bike.Forward * (bike.length * 0.5f + 0.2f);
            Rigidbody blocker = null;
            if (Physics.SphereCast(from, 0.45f, bike.Forward, out var hit, look, obstacleMask, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(transform) && !(passing && hit.rigidbody == passing && hit.distance > 0.5f))
            {
                target = hit.distance < 2.5f ? 0f : Mathf.Min(target, (hit.distance - 2f) * 0.8f);
                int hl = hit.collider.gameObject.layer;
                if (hl != 8 && hl != 9 && hit.rigidbody && hit.rigidbody.gameObject != gameObject) blocker = hit.rigidbody;
            }
            if (!blocker) blocker = bike.BlockedBy;   // stopped against its side, out of the look ahead
            if (blocker && !passing && TrafficCar.WontMove(blocker)) { waitT += dt; if (waitT > 2f) StartPass(blocker, pos); }
            else waitT = 0f;
            // a slower one ahead (a bus, an auto, a truck): past it on the right when nothing is coming in that space for
            // ~3 s of closing (owner, 23 Nov 2026: "aage waala uske comparison me hlke hain or saamne se koi vehicle nhi aara
            // to overtake krle")
            if (blocker && !passing && !TrafficCar.WontMove(blocker) && net.roads[road].width >= 5f)
            {
                float leadV = Vector3.Dot(blocker.linearVelocity, bike.Forward);
                slowT = leadV < cruise - 2f && leadV < cruise * 0.8f ? slowT + dt : 0f;
                if (slowT > 1.5f && RightClear(pos, Mathf.Clamp((cruise + 12f) * 3f, 25f, 75f), blocker))
                {
                    StartPass(blocker, pos);
                    slowT = 0f;
                    if (passing) TrafficCar.Overtakes++;
                }
            }
            else slowT = 0f;

            // an accident ahead: stop short; a siren behind: over to the left, slow
            if (IncidentManager.HoldAhead(pos, bike.Forward, out float hd)) target = Mathf.Min(target, Mathf.Max(0f, (hd - 8f) * 0.6f));
            float want = SirenNear(pos, bike.Forward) ? 1f : 0f;
            pull = Mathf.MoveTowards(pull, want, dt * 0.8f);
            if (want > 0f) target = Mathf.Min(target, 3f);

            bike.Drive(net.RoundIslands(pos, Aim(Mathf.Clamp(speed * 0.8f, 3f, 9f)), 0.5f), target, dt);

            if (speed < 0.3f) stuck += dt; else stuck = 0f;
            hornCd -= dt;
            if (stuck > 2f && hornCd <= 0f) { VehicleSound.HornOf(gameObject, pos + Vector3.up, false); hornCd = Random.Range(5f, 10f); }   // its own horn
            dipperCd -= dt;   // held up: a flick of the headlight now and then
            if (stuck > 2.5f && dipperCd <= 0f)   // the dipper, with the horn (owner, 22 Nov 2026)
            {
                if (lampsRef && Random.value < 0.45f) { lampsRef.Flash(); TrafficCar.Dippers++; VehicleSound.HornOf(gameObject, pos + Vector3.up, false); hornCd = Random.Range(5f, 10f); }
                dipperCd = Random.Range(3f, 7f);
            }
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

        /// <summary>Knocked over (a vehicle hit it, it met a wall hard: BikeRide.Hit): the rider and the pillion fly off. Up to
        /// MaxFalls times they get up, stand the bike up and ride on (owner, 22 Nov 2026: "4 baar tk to girke vaapas same
        /// vehicle pe baithke chle jaaye...agar bahut tej thoka to gira rehjaaye"): not after a hard hit (HardFall), nor
        /// when the rider is dead or badly hurt.</summary>
        void Thrown(Vector3 carVelocity, GameObject by)
        {
            down = true;
            falls++;
            fallenRider = fallenPillion = null;
            float impact = carVelocity.magnitude;
            bool hard = impact > HardFall || falls > MaxFalls;
            if (pillion)
            {
                var q = pillion;
                pillion = null;
                Vector3 sd = Vector3.Cross(Vector3.up, carVelocity.sqrMagnitude > 0.01f ? carVelocity.normalized : transform.forward);
                q.LeaveCar(transform.position - transform.forward * 0.5f + sd * 0.4f, sd);
                q.Knock(carVelocity * 0.6f + Vector3.up * 3.5f);
                var qh = q.GetComponent<Health>();
                if (qh) qh.Damage(impact * 3f, transform.position, by);
                if (!hard) fallenPillion = q;
            }
            if (!rider) { Debug.Log($"[Nainital] bike {name} knocked over, no rider"); return; }
            var r = rider;
            rider = null;
            Vector3 side = Vector3.Cross(Vector3.up, carVelocity.sqrMagnitude > 0.01f ? carVelocity.normalized : transform.forward);
            r.LeaveCar(transform.position + side * 0.6f, side);
            r.Knock(carVelocity * 0.7f + Vector3.up * 4f);
            var h = r.GetComponent<Health>();
            bool hurt = h && h.Damage(impact * 4f, transform.position, by);
            if (!hard) { fallenRider = r; upT = 0f; walkT = -1f; }
            Debug.Log($"[Nainital] bike {name} knocked over at {transform.position:F0} ({impact:F0} m/s), fall {falls}: rider {r.npcName} hurt {hurt} hp {(h ? h.current : -1):F0} - " +
                      (hard ? (impact > HardFall ? "a hard hit, they stay down" : $"fall {falls}, they stay down") : "they will get back on"));
        }

        public const int MaxFalls = 4;
        /// <summary>Thrown at more than this (m/s, ~30 km/h): nobody gets up and rides on.</summary>
        public const float HardFall = 8.5f;
        int falls;
        NpcWalker fallenRider, fallenPillion;
        float upT, walkT;

        /// <summary>Thrown riders back on (Thrown): they lie, get up (NpcWalker's knock, 3.5 s), walk back to the bike, stand it
        /// up and sit down, then it rides on along its route. Badly hurt (under a quarter of their health), dead, taken by
        /// someone else or not back within 25 s: they stay where they are (walk home), the bike lies.</summary>
        void GetBackOn(float dt)
        {
            upT += dt;
            if (!Fit(fallenRider) || upT > 25f) { GiveUp(upT > 25f ? "not back in 25 s" : Why(fallenRider)); return; }
            if (fallenPillion && !Fit(fallenPillion))
            {
                // hurt badly: not walking to the bike (where "home" would make them vanish), home instead
                var pm0 = PedestrianManager.Instance;
                if (fallenPillion.gameObject.activeInHierarchy && !fallenPillion.InCar && pm0) fallenPillion.GoHome(pm0.NearestDoor(fallenPillion.transform.position));
                fallenPillion = null;
            }
            if (upT < 4.2f || !bike.Settled) return;   // on the ground, then getting up
            Vector3 c = transform.position;
            if (walkT < 0f)
            {
                walkT = 0f;
                fallenRider.GoHome(c);   // walks to the bike (re-seated at 2 m, before "home" at 1.3 m)
                if (fallenPillion) fallenPillion.GoHome(c);
            }
            walkT += dt;
            var vp = bike.Parts;
            bool riderBack = rider;
            if (!riderBack && Flat(fallenRider.transform.position - c) < 2f)
            {
                bike.Recover();   // the rider stands it up
                Seat(fallenRider, 0, vp);
                rider = fallenRider;
                riderBack = true;
            }
            if (riderBack && fallenPillion && !pillion && Flat(fallenPillion.transform.position - transform.position) < 2f && vp && vp.SeatCount > 1)
            {
                Seat(fallenPillion, 1, vp);
                pillion = fallenPillion;
            }
            bool pillionDone = !fallenPillion || pillion || walkT > 8f;
            if (!riderBack || !pillionDone) return;
            if (fallenPillion && !pillion) fallenPillion.GoHome(PedestrianManager.Instance ? PedestrianManager.Instance.NearestDoor(fallenPillion.transform.position) : fallenPillion.transform.position);
            fallenRider = fallenPillion = null;
            down = false;
            stuck = 0f;
            passing = null;
            Debug.Log($"[Nainital] bike {name}: back on after fall {falls} ({upT:F0} s), rides on at {transform.position:F0}");
        }

        static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        static bool Fit(NpcWalker n)
        {
            if (!n || !n.gameObject.activeInHierarchy || n.InCar) return false;
            var h = n.GetComponent<Health>();
            return !h || (!h.IsDead && h.current >= h.max * 0.25f);
        }

        static string Why(NpcWalker n)
        {
            if (!n) return "gone";
            if (!n.gameObject.activeInHierarchy) return "taken away";
            if (n.InCar) return "in a vehicle";
            var h = n.GetComponent<Health>();
            return h ? (h.IsDead ? "dead" : $"hurt, hp {h.current:F0}/{h.max:F0}") : "?";
        }

        void Seat(NpcWalker n, int seat, VehicleParts vp)
        {
            var pm = PedestrianManager.Instance;
            if (pm) pm.Disown(n);
            if (vp && vp.HasSetup) n.EnterSeat(vp, seat);
            else n.EnterBike(bike.model, bike.SeatLocal);
        }

        /// <summary>Not getting back on: they walk home, the bike stays down.</summary>
        void GiveUp(string why)
        {
            var pm = PedestrianManager.Instance;
            foreach (var n in new[] { fallenRider, fallenPillion })
                if (n && n.gameObject.activeInHierarchy && !n.InCar && pm) n.GoHome(pm.NearestDoor(n.transform.position));
            if (fallenRider) Debug.Log($"[Nainital] bike {name}: its rider does not get back on (fall {falls}, {upT:F1} s): {why}");
            fallenRider = fallenPillion = null;
        }

        void OnDestroy()
        {
            if (bike) bike.OnCrash -= Thrown;   // the player picked it up and rides it (PlayerBike): no thrown handler left behind
            if (fallenRider || fallenPillion) GiveUp("the bike is gone (despawned / the player's)");   // walking back to it: home instead
            if (rider) Destroy(rider.gameObject);
            if (pillion) Destroy(pillion.gameObject);
        }
    }
}

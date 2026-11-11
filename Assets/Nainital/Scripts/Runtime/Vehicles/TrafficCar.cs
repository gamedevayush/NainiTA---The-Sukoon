using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// AI traffic: drives on the LEFT lane (India) along OSM roads, slows for bends, stops for anything
    /// in front, continues onto connected roads at junctions, otherwise turns around.
    /// </summary>
    public class TrafficCar : MonoBehaviour
    {
        public RoadNetwork net;
        public CarType type;
        public Transform[] wheels;
        public float cruise = 11f;

        int road, idx, dir;
        float speed, spin, stuck, hornCooldown, blocked;
        bool passed;
        VehicleFactory.Spec spec;
        Terrain terrain;
        static int obstacleMask = -1;

        public float Speed => speed;

        public void Begin(RoadNetwork n, int roadIndex, int startIdx, int direction)
        {
            net = n;
            road = roadIndex;
            idx = startIdx;
            dir = direction;
            spec = VehicleFactory.GetSpec(type);
            var info = GetComponent<VehicleInfo>();
            if (info) { spec.wheelRadius = info.wheelRadius; spec.length = info.length; spec.width = info.width; spec.wheelBase = info.length * 0.6f; }
            terrain = Terrain.activeTerrain;
            speed = cruise * 0.6f;
            if (obstacleMask < 0) obstacleMask = (1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9);
            Vector3 p = Lane(idx);
            transform.position = Ground(p);
            Vector3 f = Next() - p;
            f.y = 0;
            if (f.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
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
            Vector3 left = Vector3.Cross(d.normalized, Vector3.up);   // Unity: forward x up = left
            float off = Mathf.Max(1.1f, net.roads[road].width * 0.25f);
            return pts[i] + left * off;
        }

        Vector3 Next() => Lane(idx + dir);

        static readonly int groundMask = ~((1 << VehicleFactory.VehicleLayer) | (1 << 8) | (1 << 9) | (1 << 2) | (1 << 4));

        /// <summary>The surface under p: the road deck where there is one (roads stand 0.17 m proud of the ground, the
        /// Mall's decks more), else the terrain.</summary>
        Vector3 Ground(Vector3 p)
        {
            float t = terrain ? terrain.SampleHeight(p) + terrain.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, t) + 2.5f, p.z), Vector3.down, out var hit, 8f, groundMask, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y + 0.02f;
            else p.y = t + 0.05f;
            return p;
        }

        void Advance()
        {
            idx += dir;
            var pts = Pts;
            if (idx > 0 && idx < pts.Length - 1) return;
            var links = net.Links(road, idx <= 0 ? 0 : 1);
            if (links != null && links.Count > 0)
            {
                var (r, i) = links[Random.Range(0, links.Count)];
                var q = net.roads[r].pts;
                road = r;
                idx = i;
                dir = i <= 0 ? 1 : i >= q.Length - 1 ? -1 : (Random.value < 0.5f ? 1 : -1);
            }
            else
            {
                dir = -dir;
                idx = Mathf.Clamp(idx, 0, pts.Length - 1);
            }
        }

        void Update()
        {
            if (!net) return;
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            Vector3 target = Next();
            Vector3 to = target - pos;
            to.y = 0;
            if (to.magnitude < 2f) { Advance(); target = Next(); to = target - pos; to.y = 0; }

            // bends ahead
            Vector3 ahead = Lane(idx + dir * 4) - pos;
            ahead.y = 0;
            float bend = Vector3.Angle(transform.forward, ahead);
            float targetSpeed = Mathf.Lerp(cruise, 4f, Mathf.Clamp01(bend / 70f));

            // something in front? (cars, people, the player)
            float look = 5f + speed * 1.2f;
            Vector3 from = pos + Vector3.up * 1f + transform.forward * (spec.length * 0.5f + 0.3f);
            if (Physics.SphereCast(from, 0.9f, transform.forward, out var hit, look, obstacleMask, QueryTriggerInteraction.Ignore) &&
                !hit.collider.transform.IsChildOf(transform))
                targetSpeed = hit.distance < 4f ? 0f : Mathf.Min(targetSpeed, hit.distance * 0.6f);

            speed = Mathf.MoveTowards(speed, targetSpeed, (targetSpeed < speed ? 9f : 3f) * dt);

            // hill-road manners: the horn when held up, and before a blind bend
            var au = AudioManager.Instance;
            hornCooldown -= dt;
            if (targetSpeed < 0.5f && speed < 1f) blocked += dt; else blocked = 0f;
            if (au && hornCooldown <= 0f)
            {
                if (blocked > 1.2f || (bend > 45f && speed > 5f && Random.value < 0.3f))
                {
                    au.HornAt(pos + Vector3.up, type == CarType.Bus || type == CarType.Pickup);
                    hornCooldown = Random.Range(4f, 9f);
                }
                else if (bend > 45f) hornCooldown = 2.5f;   // one chance per bend
            }
            var cam = Camera.main;   // going past the listener
            if (au && cam)
            {
                float d2 = (cam.transform.position - pos).sqrMagnitude;
                if (!passed && d2 < 81f && speed > 5f) { passed = true; au.PassBy(pos, speed); }
                else if (passed && d2 > 400f) passed = false;
            }
            if (to.sqrMagnitude > 1e-4f)
            {
                Vector3 dirN = to.normalized;
                Vector3 np = pos + Vector3.Lerp(transform.forward, dirN, 0.35f).normalized * speed * dt;
                np = Ground(np);
                // pitch with the slope
                Vector3 fwdFlat = Vector3.Slerp(new Vector3(transform.forward.x, 0, transform.forward.z).normalized, dirN, dt * 4f);
                Vector3 front = Ground(np + fwdFlat * spec.wheelBase * 0.5f), back = Ground(np - fwdFlat * spec.wheelBase * 0.5f);
                Vector3 f3 = (front - back).normalized;
                transform.SetPositionAndRotation(np, Quaternion.LookRotation(f3, Vector3.up));
            }
            if (speed < 0.2f) stuck += dt; else stuck = 0f;
            if (stuck > 12f) { dir = -dir; stuck = 0f; }   // give up waiting, turn around

            spin += speed / spec.wheelRadius * Mathf.Rad2Deg * dt;
            if (wheels != null)
                for (int i = 0; i < wheels.Length; i++)
                    if (wheels[i]) wheels[i].localRotation = Quaternion.Euler(spin, 0, 0);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A story person walking a path outside a scene (bible 0.2: Uncle walks Nikhil from the lake to the Zoo Road house):
    /// along the points at its pace, on the ground, turning smoothly; when the one following falls `waitDist` behind it
    /// stops and turns to look back, and walks on when he is close again. Arrived = at the last point.
    /// </summary>
    public class StoryWalker : MonoBehaviour
    {
        public List<Vector3> path = new List<Vector3>();
        public float speed = 1.5f, waitDist = 11f, resumeDist = 6f;
        public Transform follower;
        public HumanoidRig rig;
        public bool Arrived { get; private set; }
        /// <summary>Tests only (StoryTest.FastWalk): everyone walks this many times faster.</summary>
        public static float TestSpeedScale = 1f;
        public bool Waiting { get; private set; }
        int next = 1;
        float speedNow;

        static readonly int GroundMask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer));

        public static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, p.y + 40f, p.z), Vector3.down, out var hit, 200f, GroundMask, QueryTriggerInteraction.Ignore)) return hit.point;
            var t = Terrain.activeTerrain;
            return t ? new Vector3(p.x, t.SampleHeight(p) + t.transform.position.y, p.z) : p;
        }

        void Update()
        {
            if (Arrived || path.Count < 2) { Idle(); return; }
            float dt = Time.deltaTime;
            Vector3 me = transform.position;
            if (follower)
            {
                Vector3 d = follower.position - me; d.y = 0f;
                if (!Waiting && d.magnitude > waitDist) Waiting = true;
                else if (Waiting && d.magnitude < resumeDist) Waiting = false;
                if (Waiting)
                {
                    speedNow = Mathf.MoveTowards(speedNow, 0f, dt * 3f);
                    if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-4f * dt));
                    Idle();
                    return;
                }
            }
            Vector3 to = path[next] - me; to.y = 0f;
            if (to.magnitude < 0.6f)
            {
                next++;
                if (next >= path.Count) { Arrived = true; Idle(); return; }
                to = path[next] - me; to.y = 0f;
            }
            speedNow = Mathf.MoveTowards(speedNow, speed * TestSpeedScale, dt * 2.5f * TestSpeedScale);
            Vector3 step = to.normalized * Mathf.Min(to.magnitude, speedNow * dt);
            transform.position = Ground(me + step);
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-6f * dt));
            if (rig) { rig.speed = speedNow; rig.grounded = true; }
        }

        void Idle() { if (rig) { rig.speed = 0f; rig.grounded = true; } }

        /// <summary>The path as it would walk on: from the point nearest `p` onward (a retry puts it back there).</summary>
        public void RestartFrom(Vector3 p)
        {
            Arrived = false; Waiting = false; speedNow = 0f;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < path.Count; i++) { float d = (path[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            next = Mathf.Min(best + 1, path.Count - 1);
            transform.position = Ground(path[best]);
        }
    }
}

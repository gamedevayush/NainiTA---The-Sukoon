using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>"call GymCamProbe.Run" in Play: where the camera is against what it follows, and what a sphere cast from its
    /// pivot to it hits (every collider on the way, nearest first) - for the gym workout view (22 Nov 2026).
    /// "call GymCamProbe.Ground": the terrain round each gym frame against the ground the frame stands on.</summary>
    public static class GymCamProbe
    {
        /// <summary>The terrain; in a batch run (no scene open) the game scene is opened first.</summary>
        static Terrain FindTerrain()
        {
            var t = Terrain.activeTerrain ? Terrain.activeTerrain : Object.FindAnyObjectByType<Terrain>();
            if (!t && !Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Nainital/Scenes/Nainital.unity");
                t = Object.FindAnyObjectByType<Terrain>();
            }
            return t;
        }

        public static void Run()
        {
            var tpc = ThirdPersonCamera.Instance;
            if (!tpc) { Debug.Log("[Nainital] camprobe: no camera"); return; }
            Transform t = tpc.OverrideTarget ? tpc.OverrideTarget : tpc.target;
            Vector3 pivot = t.position + Vector3.up * tpc.height;
            Vector3 cam = tpc.transform.position;
            Vector3 d = cam - pivot;
            Debug.Log($"[Nainital] camprobe: follows {t.name} at {t.position:F2}; yaw {tpc.yaw:F0} pitch {tpc.pitch:F0}; camera {cam:F2}, {d.magnitude:F2} m from the pivot {pivot:F2}");
            var pc = PlayerController.Instance;
            if (pc) Debug.Log($"[Nainital] camprobe: player at {pc.transform.position:F2} facing {pc.transform.eulerAngles.y:F0}");
            Vector3 back = -(Quaternion.Euler(tpc.pitch, tpc.yaw, 0f) * Vector3.forward);
            var hits = Physics.SphereCastAll(pivot, 0.25f, back, 5f, tpc.collisionMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
                Debug.Log($"[Nainital] camprobe:   hit {h.collider.name} ({h.collider.GetType().Name}, layer {h.collider.gameObject.layer}, parent {(h.collider.transform.parent ? h.collider.transform.parent.name : "-")}) at {h.distance:F2} m, point {h.point:F2}");
            foreach (var c in Physics.OverlapSphere(pivot, 0.25f, tpc.collisionMask, QueryTriggerInteraction.Ignore))
                Debug.Log($"[Nainital] camprobe:   pivot inside {c.name} ({c.GetType().Name}, layer {c.gameObject.layer})");
        }

        /// <summary>Cross-sections of the Lower Mall lake side along the open gym (the Street View car's track GymA -> GymB in
        /// LandmarkBuilder.LowerMallSights): every 6 m along, the terrain from 4 m on the hill side to 22 m towards the lake,
        /// as height over the track's ground, plus the first non-terrain collider from above where there is one.</summary>
        public static void Profile()
        {
            var terrain = FindTerrain();
            if (!terrain) { Debug.Log("[Nainital] gymprofile: no terrain"); return; }
            float H(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
            Vector2 a = new Vector2(784.6f, -646.8f), b = new Vector2(748.3f, -591.3f);
            Vector2 along = (b - a).normalized, lake = new Vector2(-along.y, along.x);
            if (lake.x > 0f) lake = -lake;   // the lake lies west of this stretch
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2));
            var sb = new System.Text.StringBuilder($"[Nainital] gymprofile: along {along}, lake side {lake}; columns = metres from the track (+ = lake)\n");
            for (float s = -6f; s <= Vector2.Distance(a, b) + 6f; s += 6f)
            {
                Vector2 t = a + along * s;
                float h0 = H(t);
                sb.Append($"  s {s,5:F0} ({t.x:F0},{t.y:F0}) track ground {h0:F2}:");
                for (float d = -4f; d <= 22f; d += 2f)
                {
                    Vector2 q = t + lake * d;
                    float h = H(q) - h0;
                    string what = "";
                    if (Physics.Raycast(new Vector3(q.x, h0 + 15f, q.y), Vector3.down, out var hit, 40f, mask, QueryTriggerInteraction.Ignore) && !(hit.collider is TerrainCollider))
                        what = "[" + hit.collider.name.Substring(0, Mathf.Min(6, hit.collider.name.Length)) + (hit.point.y - h0).ToString("+0.0;-0.0") + "]";
                    sb.Append($" {d:F0}:{h:+0.0;-0.0}{what}");
                }
                sb.Append('\n');
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>Every collider a ray from above meets (full names, heights over the track's ground) every 1 m across
        /// the Lower Mall lake side at three places along the gym, plus where the lake polygon starts.</summary>
        public static void Across()
        {
            var terrain = FindTerrain();
            if (!terrain) { Debug.Log("[Nainital] gymacross: no terrain"); return; }
            Vector2 a = new Vector2(784.6f, -646.8f), b = new Vector2(748.3f, -591.3f);
            Vector2 along = (b - a).normalized, lake = new Vector2(-along.y, along.x);
            if (lake.x > 0f) lake = -lake;
            var sb = new System.Text.StringBuilder("[Nainital] gymacross:\n");
            foreach (float s in new[] { 0f, 24f, 48f, 66f })
            {
                Vector2 t = a + along * s;
                float h0 = terrain.SampleHeight(new Vector3(t.x, 0f, t.y)) + terrain.transform.position.y;
                sb.Append($"  s {s} track ground {h0:F2}\n");
                for (float d = -6f; d <= 16f; d += 1f)
                {
                    Vector2 q = t + lake * d;
                    var hits = Physics.RaycastAll(new Vector3(q.x, h0 + 20f, q.y), Vector3.down, 40f, ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
                    sb.Append($"    d {d,3:F0}:");
                    foreach (var h in hits) sb.Append($" {h.collider.name}@{h.point.y - h0:+0.00;-0.00}");
                    sb.Append('\n');
                }
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>The ground along the foot of Lower Mall's parapet through the park: every 2 m along, the first point
        /// where the ground is 0.2 m under the track's ground (the wall's foot, roughly), then the ground 0.5 / 1 / 1.5 / 2.5 m
        /// further out, and every collider a ray from above meets at the foot + 1 m.</summary>
        public static void Wall()
        {
            var terrain = FindTerrain();
            if (!terrain) return;
            float H(Vector2 p) => terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y;
            var sb = new System.Text.StringBuilder("[Nainital] gymwall:\n");
            for (float s = -4f; s <= 72f; s += 2f)
            {
                Vector2 t = JoggersPark.World(s, 0f);
                float h0 = H(t), d = 0f;
                while (d < 25f && H(JoggersPark.World(s, d)) > h0 - 0.2f) d += 0.25f;
                sb.Append($"  s {s,3:F0}: foot d {d:F1};");
                foreach (float o in new[] { 0.5f, 1f, 1.5f, 2.5f }) sb.Append($" +{o}:{H(JoggersPark.World(s, d + o)) - h0:+0.00;-0.00}");
                Vector2 q = JoggersPark.World(s, d + 1f);
                var hits = Physics.RaycastAll(new Vector3(q.x, h0 + 10f, q.y), Vector3.down, 30f, ~((1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
                foreach (var h in hits) sb.Append($" [{h.collider.name}@{h.point.y - h0:+0.0;-0.0}]");
                sb.Append('\n');
            }
            Debug.Log(sb.ToString());
        }

        public static void Ground()
        {
            var terrain = FindTerrain();
            if (!terrain) { Debug.Log("[Nainital] gymground: no terrain"); return; }
            float H(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
            foreach (var g in Object.FindObjectsByType<GymStation>(FindObjectsSortMode.None))
            {
                Vector3 p = g.transform.position, floor = p - Vector3.up * 0.9f;
                var sb = new System.Text.StringBuilder();
                sb.Append($"[Nainital] gymground '{g.kind}' at {p:F1}: floor {floor.y:F2}, terrain here {H(p) - floor.y:+0.00;-0.00} m;");
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    Vector3 q = p + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 1.2f;
                    sb.Append($" {i * 45}:{H(q) - floor.y:+0.0;-0.0}");
                }
                if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 10f, ~((1 << 8) | (1 << 9) | (1 << 2)), QueryTriggerInteraction.Ignore))
                    sb.Append($"; first ground from above: {hit.collider.name} at {hit.point.y - floor.y:+0.00;-0.00}");
                Debug.Log(sb.ToString());
            }
        }
    }
}

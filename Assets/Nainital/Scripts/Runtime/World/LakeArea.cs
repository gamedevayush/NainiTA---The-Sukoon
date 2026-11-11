using UnityEngine;

namespace Nainital
{
    /// <summary>Naini Lake outline + water level, used for swimming, boats and yachts.</summary>
    public class LakeArea : MonoBehaviour
    {
        public static LakeArea Instance { get; private set; }

        public Vector2[] outline;       // world XZ, real OSM shape
        public Vector2[] innerOutline;  // shrunk ~6 m: safe area for boats
        public float waterY;

        public static float WaterY => Instance ? Instance.waterY : -10000f;

        /// <summary>Centre of the lake outline (world XZ, at water level).</summary>
        public Vector3 Centre
        {
            get
            {
                if (outline == null || outline.Length == 0) return transform.position;
                Vector2 c = Vector2.zero;
                foreach (var p in outline) c += p;
                c /= outline.Length;
                return new Vector3(c.x, waterY, c.y);
            }
        }

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        public static bool Contains(Vector3 world) =>
            Instance && Instance.outline != null && Geo.PointInPolygon(Instance.outline, new Vector2(world.x, world.z));

        public static bool ContainsInner(Vector3 world) =>
            Instance && Instance.innerOutline != null && Geo.PointInPolygon(Instance.innerOutline, new Vector2(world.x, world.z));

        public static Vector3 RandomPointInside(System.Random rng)
        {
            if (!Instance) return Vector3.zero;
            var poly = Instance.innerOutline;
            Vector2 min = poly[0], max = poly[0];
            foreach (var p in poly) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            for (int i = 0; i < 200; i++)
            {
                var q = new Vector2(Mathf.Lerp(min.x, max.x, (float)rng.NextDouble()), Mathf.Lerp(min.y, max.y, (float)rng.NextDouble()));
                if (Geo.PointInPolygon(poly, q)) return new Vector3(q.x, Instance.waterY, q.y);
            }
            return new Vector3(poly[0].x, Instance.waterY, poly[0].y);
        }
    }
}

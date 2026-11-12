using System.Text;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Debug: what stands between Lower Mall's parapet and the water (Bridge/arg.txt: "x,z,dx,dz").</summary>
    public static class BankProbe
    {
        public static void Run()
        {
            var arg = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim().Split(',');
            float x = float.Parse(arg[0]), z = float.Parse(arg[1]), dx = float.Parse(arg[2]), dz = float.Parse(arg[3]);
            var t = Terrain.activeTerrain;
            var sb = new StringBuilder("[BankProbe]\n");
            Physics.SyncTransforms();
            for (float d = 0; d <= 36f; d += 2f)
            {
                var p = new Vector3(x + dx * d, 0, z + dz * d);
                float th = t ? t.SampleHeight(p) + t.transform.position.y : float.NaN;
                sb.Append($"d {d,4:F0}  terrain {th:F2}  hits:");
                var hits = Physics.RaycastAll(new Vector3(p.x, 900f, p.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits) sb.Append($" {h.collider.name}@{h.point.y:F2}");
                sb.Append('\n');
            }
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.name == "Water") { sb.Append($"water y {r.bounds.max.y:F2}\n"); break; }
            Debug.Log(sb.ToString());
        }
    }
}

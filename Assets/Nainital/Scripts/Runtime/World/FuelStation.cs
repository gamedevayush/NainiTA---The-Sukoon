using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A petrol pump's forecourt (owner, 21 Nov 2026, FREEROAM #4: only the real pumps of the map, a fake brand, refuelling
    /// costs money). Built by LandmarkBuilder.PetrolPumps (Tallital, Sukhatal): this transform sits on the forecourt, local
    /// +z away from the road, +x along it; the area is the box `size` round it (from a little out on the road's edge to the
    /// back of the forecourt). A plain box test, no physics collider, so nobody's raycast or trigger code ever meets it.
    /// The player's vehicle that stops inside is filled by <see cref="VehicleFuel"/>.
    /// </summary>
    public class FuelStation : MonoBehaviour
    {
        /// <summary>Every pump in the world (two).</summary>
        public static readonly List<FuelStation> All = new List<FuelStation>();

        /// <summary>The fake brand (its letters are on the canopy) and the place, e.g. "Himgiri" + "Tallital".</summary>
        public string brand = "", place = "";
        /// <summary>The zone's name (what the HUD shows on arrival, the map's blip).</summary>
        public string title = "";
        /// <summary>The area: width along the road (x), height (y, from 2 m under the forecourt), depth away from it (z).</summary>
        public Vector3 size = new Vector3(14f, 7f, 13f);

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>True when the world point is over the forecourt.</summary>
        public bool Contains(Vector3 p)
        {
            Vector3 l = transform.InverseTransformPoint(p);
            return Mathf.Abs(l.x) <= size.x * 0.5f && Mathf.Abs(l.z) <= size.z * 0.5f && l.y > -2f && l.y < size.y - 2f;
        }

        /// <summary>The pump whose forecourt holds p, or null.</summary>
        public static FuelStation At(Vector3 p)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] && All[i].Contains(p)) return All[i];
            return null;
        }

        /// <summary>The nearest pump (straight line) and how far it is, or null when there is none.</summary>
        public static FuelStation Nearest(Vector3 p, out float distance)
        {
            FuelStation best = null;
            distance = float.MaxValue;
            for (int i = 0; i < All.Count; i++)
            {
                var s = All[i];
                if (!s) continue;
                float d = Vector3.Distance(p, s.transform.position);
                if (d < distance) { distance = d; best = s; }
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.72f, 0.16f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f - 2f, 0f), size);
        }
    }
}

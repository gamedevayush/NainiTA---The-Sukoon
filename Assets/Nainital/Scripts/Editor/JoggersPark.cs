using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// City Joggers Park, the open gym on Lower Mall's lake side (owner, 22 Nov 2026: "na to park ka koi gate hai, na vo flat
    /// hai...up down terrain hai..pehle terrain theek kro wahan ki...then gym equipments do"). One place for what the data
    /// fix (the shore), the Lower Mall bank (the rolling ground, the trees, the level spots), the parapet (two openings, no
    /// gate) and the gym builder (the equipment, after the last ground step) share. Positions are in the frame of the Street
    /// View car's track along this stretch (19 Nov route, panoramas 50-56): s metres along it (north), d metres out
    /// towards the lake.
    /// </summary>
    public static class JoggersPark
    {
        public static readonly Vector2 A = new Vector2(784.6f, -646.8f), B = new Vector2(748.3f, -591.3f);
        public static readonly Vector2 Along = (B - A).normalized;
        public static readonly Vector2 Lake = new Vector2(-Along.y, Along.x);   // (-0.84, -0.55): the lake lies west of here

        /// <summary>The park along the track, and how far beyond its ends the ground blends back into the bank.</summary>
        public const float S0 = -6f, S1 = 74f, Blend = 10f;

        /// <summary>Two openings in the Lower Mall parapet into the park (no gate), their middles along the track.</summary>
        public static readonly float[] Openings = { 9f, 47f };
        public const float OpeningHalf = 1.6f;

        /// <summary>
        /// The real shore along the park, read off Esri World Imagery (22 Nov 2026, zoom 18 with 5 m ticks across the
        /// track): the trees on the bank reach 20-28 m out from the track and the water's edge lies a few metres inside
        /// their crowns. The lake outline had one vertex here, ~95 m from the next ones, cutting straight across the park
        /// 4-9 m inside the real edge, so the bank was 2-8 m wide and fell 5 m to the water. (s, d) pairs.
        /// </summary>
        public static readonly Vector2[] Shore =
        {
            new Vector2(-20f, 25f), new Vector2(0f, 23.5f), new Vector2(15f, 23f), new Vector2(30f, 22.5f),
            new Vector2(45f, 21f), new Vector2(58f, 18.5f), new Vector2(70f, 15f), new Vector2(82f, 11f),
        };

        public static Vector2 Track(Vector2 p) { var q = p - A; return new Vector2(Vector2.Dot(q, Along), Vector2.Dot(q, Lake)); }
        public static Vector2 World(float s, float d) => A + Along * s + Lake * d;

        /// <summary>1 inside the park's stretch, falling to 0 over `Blend` m beyond its ends; 0 away from the track.</summary>
        public static float Weight(Vector2 p)
        {
            var t = Track(p);
            if (t.y < -12f || t.y > 40f) return 0f;
            return Mathf.Clamp01(Mathf.Min(t.x - (S0 - Blend), (S1 + Blend) - t.x) / Blend);
        }

        /// <summary>A point of the parapet line inside one of the openings.</summary>
        public static bool InOpening(Vector2 p)
        {
            var t = Track(p);
            if (t.y < -12f || t.y > 30f) return false;
            foreach (float s in Openings) if (Mathf.Abs(t.x - s) < OpeningHalf) return true;
            return false;
        }

        /// <summary>1 in an opening, 0 from 3.4 m along from its middle: the ground there comes up to the road.</summary>
        public static float OpeningK(Vector2 p)
        {
            var t = Track(p);
            if (t.y < -12f || t.y > 40f) return 0f;
            float k = 0f;
            foreach (float s in Openings) k = Mathf.Max(k, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(OpeningHalf, OpeningHalf + 1.8f, Mathf.Abs(t.x - s))));
            return k;
        }

        /// <summary>The equipment spots (set by the Lower Mall bank, which levels the ground under them and keeps its trees
        /// off them; built on by LandmarkBuilder.BuildJoggersPark after the last ground step): the middle, the top bar's
        /// direction, the kind (0 pull-up frame with the swing seats, 1 pedals, 2 twister).</summary>
        public static readonly List<(Vector2 p, Vector2 along, int kind)> Spots = new List<(Vector2, Vector2, int)>();

        public static bool NearSpot(Vector2 p, float r)
        {
            foreach (var s in Spots) if ((s.p - p).sqrMagnitude < r * r) return true;
            return false;
        }
    }
}

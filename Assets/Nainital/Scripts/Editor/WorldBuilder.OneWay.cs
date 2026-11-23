using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>
        /// The Mallital one-way pair's barricades (owner, 24 Nov 2026: "dono jagah un wanted jaane waali direction me
        /// barricade hoga...left me barricade honge..with one way diversion board..but make sure sirf ek lane me barricade
        /// ho..taaki traffic normally jaaye"). At the end each one-way piece is LEFT by (the Mall end of the Bada Bazar road; the SBM tiraha on
        /// the Jama Masjid road) (where a car would wrongly turn in),
        /// 3.5 m inside the road: yellow police barricades with a red NO ENTRY board across the left half as seen from the
        /// junction (the half a wrong-way car would take) - from the centre line + 0.25 m to the kerb; the other half stays
        /// open for the traffic coming out in its own left lane (Path.keepLeft). Beside them on that kerb a blue ONE WAY
        /// board on a pole, facing the junction. Built on the final ground (after "Roads into the terrain").
        /// </summary>
        static void BuildOneWayBarricades(WorldCtx ctx)
        {
            var parent = new GameObject("One-way barricades (Mallital)").transform;
            parent.SetParent(ctx.worldRoot, false);
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            Vector2 yellow = Atlas.SolidUV(Pal.BoatYellow), red = Atlas.SolidUV(Pal.TempleRed), white = Atlas.SolidUV(Pal.ShirtWhite),
                    blue = Atlas.SolidUV(Pal.BoatBlue), iron = Atlas.SolidUV(Pal.IronBlack);
            int made = 0;
            foreach (var r in ctx.world.roads)
            {
                if (!VehicleKinds.Contains(r.kind)) continue;
                var pieces = OneWayPieces(Geo.ToPoints(r.pts));
                if (pieces == null) continue;
                foreach (var (line, dir) in pieces)
                {
                if (dir == 0 || line.Length < 2) continue;
                // the end the traffic leaves by, and the way into the road from it
                int end = dir > 0 ? line.Length - 1 : 0, step = dir > 0 ? -1 : 1;
                Vector2 at = line[end];
                float want = 3.5f, walked = 0f;
                int i = end;
                while (i + step >= 0 && i + step < line.Length)
                {
                    float seg = Vector2.Distance(line[i], line[i + step]);
                    if (walked + seg >= want) { at = Vector2.Lerp(line[i], line[i + step], (want - walked) / seg); break; }
                    walked += seg;
                    i += step;
                    at = line[i];
                }
                Vector2 inward = (line[Mathf.Clamp(i + step, 0, line.Length - 1)] - line[i]).normalized;
                if (inward.sqrMagnitude < 0.5f) inward = (line[end + step] - line[end]).normalized;
                Vector3 in3 = new Vector3(inward.x, 0f, inward.y);
                Vector3 left3 = Vector3.Cross(in3, Vector3.up);   // the left of someone looking into the road from the junction
                Vector2 left = new Vector2(left3.x, left3.z);
                float half = r.width * 0.5f;
                float from = 0.25f, to = half - 0.15f, span = to - from;
                Vector2 mid = at + left * ((from + to) * 0.5f);
                float y = ctx.H(mid);
                var rot = Quaternion.LookRotation(-in3);   // faces the junction
                Vector3 c = new Vector3(mid.x, y, mid.y);
                // the barricade: two rails, end legs, a red board with NO ENTRY in the middle
                mb.Box(c + Vector3.up * 0.55f, new Vector3(span, 0.08f, 0.08f), rot, yellow);
                mb.Box(c + Vector3.up * 0.95f, new Vector3(span, 0.08f, 0.08f), rot, yellow);
                for (float s = -1f; s <= 1.01f; s += span > 2.6f ? 1f : 2f)
                    mb.Box(c + rot * Vector3.right * (s * span * 0.5f) + Vector3.up * 0.5f, new Vector3(0.08f, 1f, 0.5f), rot, yellow);
                // red and white stripes on the lower rail's face, then the board
                int stripes = Mathf.Max(3, Mathf.RoundToInt(span / 0.4f));
                for (int k = 0; k < stripes; k++)
                {
                    float sx = -span * 0.5f + (k + 0.5f) * span / stripes;
                    mb.Box(c + rot * new Vector3(sx, 0.55f, 0.045f), new Vector3(span / stripes * 0.5f, 0.07f, 0.01f), rot, red);
                }
                mb.Box(c + Vector3.up * 0.75f + rot * Vector3.forward * 0.03f, new Vector3(Mathf.Min(1.6f, span * 0.6f), 0.36f, 0.04f), rot, red);
                Label(parent, null, "NO ENTRY", c + Vector3.up * 0.75f + rot * Vector3.forward * 0.06f, -in3, 0.26f, Color.white);
                col.Box(c + Vector3.up * 0.5f, new Vector3(span, 1f, 0.5f), rot, Vector2.zero);
                // the ONE WAY board on a pole at that kerb, facing the junction
                Vector2 kerb = at + left * (half + 0.35f);
                float ky = ctx.H(kerb);
                Vector3 k3 = new Vector3(kerb.x, ky, kerb.y);
                mb.Cylinder(k3, 0.045f, 0.045f, 2.6f, 8, iron, false);
                Vector3 board = k3 + Vector3.up * 2.25f + rot * Vector3.forward * 0.05f;
                mb.Box(board, new Vector3(1.1f, 0.55f, 0.04f), rot, blue);
                mb.Box(board + rot * Vector3.forward * 0.015f, new Vector3(1.0f, 0.45f, 0.02f), rot, white);
                mb.Box(board + rot * Vector3.forward * 0.03f, new Vector3(0.94f, 0.39f, 0.02f), rot, blue);
                Label(parent, null, "ONE WAY", board + Vector3.up * 0.08f + rot * Vector3.forward * 0.05f, -in3, 0.22f, Color.white);
                Label(parent, null, "DIVERSION", board - Vector3.up * 0.13f + rot * Vector3.forward * 0.05f, -in3, 0.12f, Color.white);
                col.Box(k3 + Vector3.up * 1.3f, new Vector3(0.12f, 2.6f, 0.12f), Quaternion.identity, Vector2.zero);
                made++;
                Debug.Log($"[Nainital] one-way barricade at ({mid.x:F1}, {mid.y:F1}) across {span:F1} m of a {r.width:F1} m road (left half from the junction), board at ({kerb.x:F1}, {kerb.y:F1})");
                }
            }
            ctx.MeshObject("One-way barricades and boards", parent, mb, ctx.mats.atlas, col, true);
            if (made != 2) Debug.LogWarning($"[Nainital] one-way barricades: {made} built, 2 expected (OneWayPieces)");
        }
    }
}

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
            // Owner, 26 Nov 2026: "barricades jahan tumne lgaaye the wahin rkhdo but single mesh me poora barricade taaki main
            // move krsku..har barricade ko invidually join krdo..taaki main ek ek ko set krsku". Each barricade (rails, legs,
            // stripes, red board, NO ENTRY letters) is ONE object with ONE mesh and its pivot on the ground at its middle, and
            // each ONE WAY board (pole, board, letters) is another; both built in their own local space, so moving / turning
            // one in the editor moves all of it. Keep a move with Nainital > Keep my move (names are numbered, stable).
            var parent = new GameObject("One-way barricades (Mallital)").transform;
            parent.SetParent(ctx.worldRoot, false);
            Vector2 yellow = Atlas.SolidUV(Pal.BoatYellow), red = Atlas.SolidUV(Pal.TempleRed), white = Atlas.SolidUV(Pal.ShirtWhite),
                    blue = Atlas.SolidUV(Pal.BoatBlue), iron = Atlas.SolidUV(Pal.IronBlack);
            var id = Quaternion.identity;
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
                var rot = Quaternion.LookRotation(-in3);   // faces the junction: local +z = towards the junction
                made++;

                // the barricade, in its own space (origin on the ground at its middle): two rails, end legs, stripes, the board
                var mb = new MeshBuilder();
                var col = new MeshBuilder();
                mb.Box(Vector3.up * 0.55f, new Vector3(span, 0.08f, 0.08f), id, yellow);
                mb.Box(Vector3.up * 0.95f, new Vector3(span, 0.08f, 0.08f), id, yellow);
                for (float s = -1f; s <= 1.01f; s += span > 2.6f ? 1f : 2f)
                    if (Mathf.Abs(s) > 0.5f) mb.Box(new Vector3(s * span * 0.5f, 0.5f, 0f), new Vector3(0.08f, 1f, 0.5f), id, yellow);
                    else mb.Box(new Vector3(0f, 0.5f, -0.12f), new Vector3(0.08f, 1f, 0.25f), id, yellow);   // the middle leg behind the board (it hid "NO ENTRY": 26 Nov)
                int stripes = Mathf.Max(3, Mathf.RoundToInt(span / 0.4f));
                for (int k = 0; k < stripes; k++)
                {
                    float sx = -span * 0.5f + (k + 0.5f) * span / stripes;
                    mb.Box(new Vector3(sx, 0.55f, 0.045f), new Vector3(span / stripes * 0.5f, 0.07f, 0.01f), id, red);
                }
                mb.Box(new Vector3(0f, 0.75f, 0.03f), new Vector3(Mathf.Min(1.6f, span * 0.6f), 0.36f, 0.04f), id, red);
                Letters(mb, "NO ENTRY", new Vector3(0f, 0.75f, 0.06f), Vector3.forward, 0.26f, Color.white);
                col.Box(Vector3.up * 0.5f, new Vector3(span, 1f, 0.5f), id, Vector2.zero);
                var bar = ctx.MeshObject($"Barricade {made}", parent, mb, ctx.mats.atlas, col, true);
                bar.transform.SetPositionAndRotation(new Vector3(mid.x, ctx.H(mid), mid.y), rot);

                // the ONE WAY board on a pole at that kerb, facing the junction, its own object (origin at the pole's foot)
                Vector2 kerb = at + left * (half + 0.35f);
                var bb = new MeshBuilder();
                var bcol = new MeshBuilder();
                bb.Cylinder(Vector3.zero, 0.045f, 0.045f, 2.6f, 8, iron, false);
                Vector3 board = new Vector3(0f, 2.25f, 0.05f);
                bb.Box(board, new Vector3(1.1f, 0.55f, 0.04f), id, blue);
                bb.Box(board + Vector3.forward * 0.015f, new Vector3(1.0f, 0.45f, 0.02f), id, white);
                bb.Box(board + Vector3.forward * 0.03f, new Vector3(0.94f, 0.39f, 0.02f), id, blue);
                Letters(bb, "ONE WAY", board + new Vector3(0f, 0.08f, 0.05f), Vector3.forward, 0.22f, Color.white);
                Letters(bb, "DIVERSION", board + new Vector3(0f, -0.13f, 0.05f), Vector3.forward, 0.12f, Color.white);
                bcol.Box(Vector3.up * 1.3f, new Vector3(0.12f, 2.6f, 0.12f), id, Vector2.zero);
                var sign = ctx.MeshObject($"One way board {made}", parent, bb, ctx.mats.atlas, bcol, true);
                sign.transform.SetPositionAndRotation(new Vector3(kerb.x, ctx.H(kerb), kerb.y), rot);

                Debug.Log($"[Nainital] one-way barricade {made} at ({mid.x:F1}, {mid.y:F1}) across {span:F1} m of a {r.width:F1} m road (left half from the junction), board at ({kerb.x:F1}, {kerb.y:F1})");
                }
            }
            if (made != 2) Debug.LogWarning($"[Nainital] one-way barricades: {made} built, 2 expected (OneWayPieces)");
        }

        /// <summary>Label()'s solid 3D letters, written into a given mesh (so a movable prop stays one mesh).</summary>
        static void Letters(MeshBuilder mb, string text, Vector3 pos, Vector3 facing, float height, Color col)
        {
            int front = NearestPal(col);
            int side = front == Pal.IronBlack || front == Pal.WoodDark ? Pal.StoneGrey : (col.grayscale > 0.6f ? Pal.StoneGrey : Pal.IronBlack);
            Glyph3D.Write(mb, text, LabelFace(text), pos + facing.normalized * 0.02f, facing, height * 0.72f,
                          Mathf.Clamp(height * 0.12f, 0.02f, 0.12f), float.MaxValue, Atlas.SolidUV(front), Atlas.SolidUV(side));
        }
    }
}

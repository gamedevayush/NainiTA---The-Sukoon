using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Capitol Cinema (user's Street View and Google Maps photos, 2026-11-14: "capitol mein aur details do, neeche brands
    /// hain; ye lake se thoda pehle ruka hai; capitol aur ye area stairs se neeche utarne ke baad hai"). On its real
    /// OpenStreetMap footprint (20 m front to the park and the Mall end, 35 m deep), down at the level of the ground in front:
    /// the walk to Naina Devi passes higher up on its west side behind a stone retaining wall, with a flight of stone stairs
    /// down at the front corner. The facade: a ground floor of shops between square pillars on red-brown bases (Galaxy 12D,
    /// Chai Sutta Bar, Cafeteria, Box Office) under striped awnings and black wall lanterns; a red band; an upper loggia of
    /// three bays with white lattice balcony rails under red "THE / Pahari / STORE" boards; the "ASSEMBLY HALL" frieze;
    /// the red CAPITOL sign with its blue CINEMA strip, a film billboard and a dish above; red pyramid roofs on the corner
    /// blocks. Beside it toward the lake, set back from the water: the low white Video Games / Zoom Land hall with red bands.
    /// </summary>
    public static partial class WorldBuilder
    {
        static void CapitolCinema(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 c2, Vector2 f2, float W, float D)
        {
            Vector2 r2 = new Vector2(f2.y, -f2.x);
            // the floor is the ground in front (the low side), the ground under and round the building cut to it
            float y = Mathf.Min(ctx.H(c2 + f2 * (D * 0.5f + 3f)), ctx.H(c2 + f2 * (D * 0.5f + 3f) + r2 * (W * 0.4f)), ctx.H(c2 + f2 * (D * 0.5f + 3f) - r2 * (W * 0.4f)));
            LandmarkBuilder.CutGround(ctx, c2, f2, W * 0.5f + 1f, D * 0.5f + 4f, y - 0.1f);
            var rot = Quaternion.LookRotation(Flat(f2));
            Vector3 c = V(c2, y), F = Flat(f2), R = Flat(r2);
            Vector3 P(float x, float h, float z) => c + R * x + Vector3.up * h + F * z;   // x across the front, z out of it
            Vector2 white = Atlas.WallSolid(0), red = Atlas.SolidUV(Pal.TempleRed), maroon = Atlas.SolidUV(Pal.WoodDark), black = Atlas.SolidUV(Pal.IronBlack),
                    blue = Atlas.SolidUV(Pal.BoatBlue), glow = Atlas.SolidUV(Pal.WindowGlow), cream = Atlas.SolidUV(Pal.TempleWhite), conc = Atlas.MaterialUV(Atlas.MatConcrete),
                    yellow = Atlas.SolidUV(Pal.BoatYellow), stone = Atlas.MaterialUV(Atlas.MatStone);
            const float H = 9.2f, G = 3.7f;            // roof line, top of the ground floor
            float fz = D * 0.5f;
            // the body, a plinth, the red band between the floors all round, a cornice
            mb.Box(c + Vector3.up * (H * 0.5f - 0.5f), new Vector3(W, H + 1f, D), rot, white);
            col.Box(c + Vector3.up * (H * 0.5f), new Vector3(W, H, D), rot, Vector2.zero);
            mb.Box(c + Vector3.up * 0.2f, new Vector3(W + 0.15f, 0.4f, D + 0.15f), rot, maroon);
            mb.Box(c + Vector3.up * G, new Vector3(W + 0.1f, 0.28f, D + 0.1f), rot, red);
            mb.Box(c + Vector3.up * (H + 0.12f), new Vector3(W + 0.5f, 0.24f, D + 0.5f), rot, white);
            mb.Box(c + Vector3.up * (H + 0.45f), new Vector3(W + 0.1f, 0.5f, D + 0.1f), rot, white);   // parapet

            // ground floor: six pillars, five shop fronts with their boards
            float[] px = { -0.5f, -0.3f, -0.1f, 0.1f, 0.3f, 0.5f };
            string[] boards = { "GALAXY 12D", "CHAI SUTTA BAR", "CINEMA", "CAFETERIA", "BOX OFFICE" };
            Vector2[] boardCol = { black, black, red, red, red };
            Color[] boardInk = { Color.white, new Color(1f, 0.6f, 0.2f), Color.white, Color.white, Color.white };
            var holder = new GameObject("Capitol Cinema signs").transform;
            holder.SetParent(parent, false);
            for (int k = 0; k < px.Length; k++)
            {
                float x = px[k] * (W - 0.8f);
                mb.Box(P(x, G * 0.5f, fz + 0.25f), new Vector3(0.8f, G, 0.5f), rot, white);
                mb.Box(P(x, 0.35f, fz + 0.3f), new Vector3(0.95f, 0.7f, 0.62f), rot, maroon);                 // red-brown base
                mb.Box(P(x, 2.6f, fz + 0.56f), new Vector3(0.14f, 0.3f, 0.1f), rot, black);                  // the lantern
                mb.Box(P(x, 2.35f, fz + 0.62f), new Vector3(0.22f, 0.32f, 0.22f), rot, glow);
                if (k == px.Length - 1) continue;
                float x1 = px[k + 1] * (W - 0.8f), mid = (x + x1) * 0.5f, bw = x1 - x - 0.85f;
                mb.Box(P(mid, 1.25f, fz + 0.04f), new Vector3(bw, 2.5f, 0.06f), rot, k == 2 ? maroon : black);   // open fronts, a wooden door in the middle
                mb.Box(P(mid, 1.2f, fz + 0.08f), new Vector3(bw * 0.7f, 2.0f, 0.03f), rot, glow);
                mb.Box(P(mid, 2.95f, fz + 0.1f), new Vector3(bw + 0.1f, 0.62f, 0.08f), rot, boardCol[k]);
                Label(holder, font, boards[k], P(mid, 2.95f, fz + 0.15f), F, 0.24f, boardInk[k]);
                if (k == 1 || k == 3)   // striped awnings over the cafe fronts
                    for (int st = 0; st < 6; st++)
                    {
                        float s0 = mid - bw * 0.5f + st * bw / 6f;
                        Vector2 sc = st % 2 == 0 ? Atlas.SolidUV(Pal.ShirtWhite) : Atlas.SolidUV(Pal.StoneGrey);
                        mb.QuadDouble(P(s0, 2.55f, fz + 0.1f), P(s0 + bw / 6f, 2.55f, fz + 0.1f), P(s0 + bw / 6f, 2.05f, fz + 1.2f), P(s0, 2.05f, fz + 1.2f), new Rect(sc, Vector2.zero), Vector3.up);
                    }
            }
            // upper floor: pilasters with capitals, three loggia bays with lattice rails and the red store boards
            for (int k = 0; k < 4; k++)
            {
                float x = (-0.21f + k * 0.14f) * W;
                mb.Box(P(x, (G + H) * 0.5f, fz + 0.2f), new Vector3(0.55f, H - G, 0.4f), rot, white);
                mb.Box(P(x, H - 0.75f, fz + 0.3f), new Vector3(0.75f, 0.25f, 0.55f), rot, cream);
            }
            string[] store = { "THE", "Pahari", "STORE" };
            for (int k = 0; k < 3; k++)
            {
                float x = (-0.14f + k * 0.14f) * W;
                mb.Box(P(x, 5.6f, fz - 0.25f), new Vector3(W * 0.14f - 0.6f, 2.4f, 0.05f), rot, glow);         // the lit room behind
                mb.Box(P(x, 4.6f, fz + 0.22f), new Vector3(W * 0.14f - 0.55f, 0.06f, 0.05f), rot, cream);    // lattice rail
                for (float s = -W * 0.07f + 0.5f; s < W * 0.07f - 0.45f; s += 0.22f)
                    mb.Box(P(x + s, 4.3f, fz + 0.22f), new Vector3(0.04f, 0.6f, 0.04f), rot, cream);
                mb.Box(P(x, 7.25f, fz + 0.3f), new Vector3(W * 0.14f - 0.7f, 0.55f, 0.06f), rot, red);
                Label(holder, font, store[k], P(x, 7.25f, fz + 0.35f), F, 0.32f, new Color(1f, 0.85f, 0.3f));
            }
            // the outer bays: a window each with a small sloping hood
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * W * 0.36f;
                mb.Box(P(x, 5.9f, fz + 0.04f), new Vector3(1.2f, 1.7f, 0.06f), rot, maroon);
                mb.Box(P(x, 5.9f, fz + 0.08f), new Vector3(0.9f, 1.4f, 0.04f), rot, glow);
                mb.Box(P(x, 6.95f, fz + 0.35f), new Vector3(1.5f, 0.08f, 0.6f), rot * Quaternion.Euler(-14f, 0f, 0f), white);
            }
            // the frieze and the sign
            mb.Box(P(0f, 8.3f, fz + 0.45f), new Vector3(W * 0.5f, 0.6f, 0.2f), rot, cream);
            Label(holder, font, "ASSEMBLY HALL", P(0f, 8.3f, fz + 0.57f), F, 0.32f, new Color(0.78f, 0.74f, 0.66f));
            mb.Box(P(-W * 0.04f, H + 1.3f, fz - 0.6f), new Vector3(W * 0.48f, 1.3f, 0.12f), rot, red);
            mb.Box(P(-W * 0.04f, H + 0.45f, fz - 0.52f), new Vector3(W * 0.16f, 0.42f, 0.08f), rot, blue);
            Label(holder, font, "CAPITOL", P(-W * 0.04f, H + 1.3f, fz - 0.52f), F, 0.95f, Color.white);
            Label(holder, font, "CINEMA", P(-W * 0.04f, H + 0.45f, fz - 0.46f), F, 0.32f, Color.white);
            mb.Box(P(W * 0.25f, H + 1.2f, fz - 0.6f), new Vector3(W * 0.14f, 0.7f, 0.08f), rot, Atlas.SolidUV(Pal.ShirtWhite));   // bookmyshow board
            // the film billboard on a frame behind the sign, and the dish
            mb.Box(P(-W * 0.1f, H + 4.4f, fz - 3.5f), new Vector3(W * 0.42f, 3.6f, 0.12f), rot, Atlas.SolidUV(Pal.JacketBlue));
            mb.Box(P(-W * 0.18f, H + 4.4f, fz - 3.42f), new Vector3(W * 0.12f, 3.0f, 0.04f), rot, Atlas.SolidUV(Pal.SareeOrange));
            mb.Box(P(W * 0.0f, H + 4.6f, fz - 3.42f), new Vector3(W * 0.14f, 2.2f, 0.04f), rot, yellow);
            foreach (float sx in new[] { -0.28f, 0.08f }) mb.Box(P(sx * W, H + 2.0f, fz - 3.55f), new Vector3(0.14f, 3.4f, 0.14f), rot, black);
            mb.Cylinder(P(-W * 0.36f, H + 0.7f, fz - 2f), 0.6f, 0.05f, 0.3f, 10, Atlas.SolidUV(Pal.ShirtWhite), true, true);
            // red pyramid roofs on the corner blocks (the left one a little tower)
            mb.Box(P(-W * 0.5f + 2.2f, H + 1.2f, fz - 2.5f), new Vector3(4.4f, 2.4f, 5f), rot, white);
            mb.Pyramid(P(-W * 0.5f + 2.2f, H + 2.4f, fz - 2.5f), 5f, 5.6f, 1.6f, red);
            mb.Pyramid(P(W * 0.5f - 2.2f, H + 0.7f, fz - 2.5f), 4.6f, 5.2f, 1.3f, red);
            // the long sides: windows in the red band's rhythm, the Galaxy board and the waffles sign by the front corner
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 side = R * sx;
                var srot = Quaternion.LookRotation(side);
                for (float z = -fz + 3f; z < fz - 2f; z += 4.2f)
                {
                    mb.Box(c + side * (W * 0.5f + 0.03f) + F * z + Vector3.up * 5.8f, new Vector3(1.0f, 1.5f, 0.05f), srot, maroon);
                    mb.Box(c + side * (W * 0.5f + 0.03f) + F * z + Vector3.up * 1.6f, new Vector3(0.9f, 2.2f, 0.05f), srot, z > fz - 9f ? glow : maroon);
                }
                mb.Box(c + side * (W * 0.5f + 0.07f) + F * (fz - 2f) + Vector3.up * 3.0f, new Vector3(2.6f, 0.7f, 0.06f), srot, black);
                Label(holder, font, sx < 0f ? "GALAXY Entertainment 12D" : "HOT WAFFLES", c + side * (W * 0.5f + 0.12f) + F * (fz - 2f) + Vector3.up * 3.0f, side, 0.18f, Color.white);
            }

            // the walk to Naina Devi passes higher up on the west: a stone retaining wall along that side, stairs down at the front
            Vector2 west = ctx.LakeDistance(c2 + r2 * 12f) > ctx.LakeDistance(c2 - r2 * 12f) ? r2 : -r2;
            Vector2 wallA = c2 + west * (W * 0.5f + 4f) + f2 * (fz + 6f), wallB = c2 + west * (W * 0.5f + 4f) - f2 * (fz + 2f);
            float walkY = Mathf.Max(ctx.H(wallA + west * 2f), ctx.H(wallB + west * 2f), ctx.H((wallA + wallB) * 0.5f + west * 2f));
            float rise = walkY - y;
            if (rise > 0.6f)
            {
                var wrot = Quaternion.LookRotation(Flat((wallB - wallA).normalized));
                float wl = Vector2.Distance(wallA, wallB);
                mb.Box(V((wallA + wallB) * 0.5f, y + rise * 0.5f), new Vector3(0.6f, rise, wl), wrot, stone);
                col.Box(V((wallA + wallB) * 0.5f, y + rise * 0.5f), new Vector3(0.6f, rise, wl), wrot, Vector2.zero);
                mb.Box(V((wallA + wallB) * 0.5f, walkY + 0.25f), new Vector3(0.7f, 0.5f, wl), wrot, stone);   // the coping people sit on
            }
            // the stairs from the walk come down along the retaining wall at the front (WorldBuilder.CapitolGround.cs)
            CapitolWallAndStairs(ctx, mb, col);
            // the paved lane in front and down the sides
            mb.Box(c + F * (fz + 4f) + Vector3.up * 0.02f, new Vector3(W + 8f, 0.04f, 8f), rot, Atlas.SolidUV(Pal.TempleRed));
            ctx.InfoSign("Capitol Cinema", "1930s ka purana cinema hall (Assembly Hall), Mallital mein Flats ke kinare. Neeche Galaxy 12D, Chai Sutta Bar aur The Pahari Store.",
                         V(c2 + f2 * (fz + 6f) + r2 * (W * 0.5f + 1f), y), rot, parent);
        }

        /// <summary>The low white Video Games / Zoom Land hall beside the Capitol toward the lake, red bands, a red board,
        /// set back from the water behind the lakeside walk.</summary>
        static void VideoGamesHall(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 c2, Vector2 f2, float W, float D)
        {
            Vector2 r2 = new Vector2(f2.y, -f2.x);
            Vector2 lake = ctx.LakeDistance(c2 + r2 * 12f) < ctx.LakeDistance(c2 - r2 * 12f) ? r2 : -r2;
            Vector2 at = c2 + lake * (W * 0.5f + 5.5f) + f2 * (D * 0.5f - 7f);
            if (ctx.LakeDistance(at) < 12f) return;   // keep the lakeside walk clear
            float y = ctx.H(at);
            var rot = Quaternion.LookRotation(Flat(f2));
            Vector3 c = V(at, y), F = Flat(f2);
            Vector2 white = Atlas.WallSolid(0), red = Atlas.SolidUV(Pal.TempleRed);
            mb.Box(c + Vector3.up * 2.1f, new Vector3(10f, 4.2f, 12f), rot, white);
            col.Box(c + Vector3.up * 2.1f, new Vector3(10f, 4.2f, 12f), rot, Vector2.zero);
            mb.Box(c + Vector3.up * 4.2f, new Vector3(10.3f, 0.3f, 12.3f), rot, red);
            mb.Box(c + Vector3.up * 1.6f, new Vector3(10.1f, 0.25f, 12.1f), rot, red);
            for (int k = 0; k < 3; k++)
                mb.Box(c + F * 6.04f + Flat(r2) * (-3f + k * 3f) + Vector3.up * 1.1f, new Vector3(2f, 2.2f, 0.05f), rot, Atlas.SolidUV(k == 1 ? Pal.WoodDark : Pal.IronBlack));
            mb.Box(c + F * 6.1f + Vector3.up * 3.3f, new Vector3(6f, 0.9f, 0.08f), rot, red);
            var holder = new GameObject("Video Games sign").transform;
            holder.SetParent(parent, false);
            Label(holder, font, "VIDEO GAMES", c + F * 6.16f + Vector3.up * 3.3f, F, 0.45f, Color.white);
            Label(holder, font, "ZOOM LAND", c + F * 6.16f + Vector3.up * 4.75f, F, 0.4f, new Color(0.75f, 0.08f, 0.08f));
            mb.Box(c + F * 6.1f + Vector3.up * 4.75f, new Vector3(5f, 0.75f, 0.06f), rot, Atlas.SolidUV(Pal.ShirtWhite));
            ctx.keepClear.Add((at, 8f));
        }
    }
}

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
            // the floor is the low level the ground was shaped to (WorldBuilder.CapitolGround.cs); no cut of its own, it would
            // eat into the park's terrace and the walk
            float y = !float.IsNaN(capLowY) ? capLowY
                    : Mathf.Min(ctx.H(c2 + f2 * (D * 0.5f + 3f)), ctx.H(c2 + f2 * (D * 0.5f + 3f) + r2 * (W * 0.4f)), ctx.H(c2 + f2 * (D * 0.5f + 3f) - r2 * (W * 0.4f)));
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

            // the walk to Naina Devi passes higher up on the west: its retaining wall, the stairs down into the lane, the park's
            // walls and the red and grey paving (WorldBuilder.CapitolGround.cs)
            CapitolWallAndStairs(ctx, mb, col);
            ctx.InfoSign("Capitol Cinema", "1930s ka purana cinema hall (Assembly Hall), Mallital mein Flats ke kinare. Neeche Galaxy 12D, Chai Sutta Bar aur The Pahari Store.",
                         V(c2 + f2 * (fz + 6f) + r2 * (W * 0.5f + 1f), y), rot, parent);
        }

        /// <summary>The Video Games / Zoom Land hall beside the Capitol toward the lake (user, 2026-11-16: "ye video game waali
        /// building almost end tk hai, side se hlki si gali"; the satellite photo: a long dark-roofed hall from the Capitol to a
        /// narrow lane above the lakeside path). White, red bands, roller shutters and the boards on its front to the lane.</summary>
        static void VideoGamesHall(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 c2, Vector2 f2, float W, float D)
        {
            var g = CapitolGeo(ctx);
            if (g == null) return;
            float hw = g.hallS1 - g.hallS0, hd = g.hallA1 - g.hallA0;
            if (hw < 4f || hd < 6f) return;
            Vector2 at = CapW((g.hallA0 + g.hallA1) * 0.5f, (g.hallS0 + g.hallS1) * 0.5f);
            float y = !float.IsNaN(capLowY) ? capLowY : ctx.H(at);
            var rot = Quaternion.LookRotation(Flat(CapWalkDir));   // its front looks along the walk, onto the lane
            Vector3 c = V(at, y), F = Flat(CapWalkDir), R = Flat(CapLakeSide);
            Vector2 white = Atlas.WallSolid(0), red = Atlas.SolidUV(Pal.TempleRed), roof = Atlas.SolidUV(Pal.StoneGrey), dark = Atlas.SolidUV(Pal.IronBlack),
                    glow = Atlas.SolidUV(Pal.WindowGlow), shutter = Atlas.MaterialUV(Atlas.MatConcrete);
            const float H = 6.4f;
            float fz = hd * 0.5f;
            int bays = Mathf.Clamp(Mathf.RoundToInt(hw / 3.4f), 3, 5);
            float doorX = ((bays / 2 + 0.5f) / bays - 0.5f) * (hw - 1.2f);
            Vector3 P(float x, float yy, float z) => c + R * x + F * z + Vector3.up * yy;
            // a hollow hall now (owner, 23 Nov 2026: "capitol ke neeche video games ki dukaan me pool wgera, bowling and basic
            // playable games rkhne hain ... with proper interior lights"): walls, the door open in the middle bay, a floor and
            // a ceiling; inside, the games (VideoGamesInside)
            const float T = 0.25f;
            void WallBox(float x, float z, float sx, float sz, float y0, float y1)
            {
                mb.Box(P(x, (y0 + y1) * 0.5f, z), new Vector3(sx, y1 - y0, sz), rot, white);
                col.Box(P(x, (y0 + y1) * 0.5f, z), new Vector3(sx, y1 - y0, sz), rot, Vector2.zero);
            }
            WallBox(0f, -fz + T * 0.5f, hw, T, -0.4f, H);
            WallBox(-hw * 0.5f + T * 0.5f, 0f, T, hd, -0.4f, H);
            WallBox(hw * 0.5f - T * 0.5f, 0f, T, hd, -0.4f, H);
            float dl = doorX - 1.2f, dr = doorX + 1.2f;
            WallBox((-hw * 0.5f + dl) * 0.5f, fz - T * 0.5f, dl + hw * 0.5f, T, -0.4f, H);
            WallBox((dr + hw * 0.5f) * 0.5f, fz - T * 0.5f, hw * 0.5f - dr, T, -0.4f, H);
            WallBox(doorX, fz - T * 0.5f, 2.4f, T, 2.75f, H);
            mb.Box(P(0f, 0.06f, 0f), new Vector3(hw - 0.5f, 0.12f, hd - 0.5f), rot, Atlas.SolidUV(Pal.IronBlack));      // the floor
            col.Box(P(0f, 0.06f, 0f), new Vector3(hw - 0.5f, 0.12f, hd - 0.5f), rot, Vector2.zero);
            mb.Box(P(0f, H - 0.12f, 0f), new Vector3(hw - 0.5f, 0.12f, hd - 0.5f), rot, Atlas.SolidUV(Pal.StoneGrey));   // the ceiling
            mb.Box(c + Vector3.up * (H + 0.05f), new Vector3(hw - 0.3f, 0.1f, hd - 0.3f), rot, roof);          // the dark flat roof
            // the parapet, the red bands and the dark plinth: strips round the outside (not slabs through the room)
            void Ring(float y0, float hgt, float outset, Vector2 uv)
            {
                float yy = y0 + hgt * 0.5f;
                mb.Box(P(0f, yy, fz + outset * 0.5f), new Vector3(hw + outset * 2f, hgt, 0.12f + outset), rot, uv);
                mb.Box(P(0f, yy, -fz - outset * 0.5f), new Vector3(hw + outset * 2f, hgt, 0.12f + outset), rot, uv);
                mb.Box(P(hw * 0.5f + outset * 0.5f, yy, 0f), new Vector3(0.12f + outset, hgt, hd + outset * 2f), rot, uv);
                mb.Box(P(-hw * 0.5f - outset * 0.5f, yy, 0f), new Vector3(0.12f + outset, hgt, hd + outset * 2f), rot, uv);
            }
            Ring(H + 0.05f, 0.5f, 0.05f, white);
            Ring(H - 0.475f, 0.35f, 0.06f, red);
            Ring(2.475f, 0.25f, 0.06f, red);
            Ring(0f, 0.4f, 0.05f, Atlas.SolidUV(Pal.WoodDark));
            // the front to the lane: roller shutters either side of the open door, the red VIDEO GAMES board, ZOOM LAND above
            for (int k = 0; k < bays; k++)
            {
                float x = ((k + 0.5f) / bays - 0.5f) * (hw - 1.2f);
                if (k == bays / 2) continue;
                mb.Box(c + R * x + F * (fz + 0.03f) + Vector3.up * 1.15f, new Vector3(hw / bays - 0.9f, 2.3f, 0.06f), rot, shutter);
            }
            mb.Box(P(doorX, 2.85f, fz + 0.05f), new Vector3(2.7f, 0.2f, 0.12f), rot, Atlas.SolidUV(Pal.WoodDark));   // the door's head
            VideoGamesInside(ctx, parent, mb, col, font, c, F, R, rot, hw, hd, H, doorX);
            // the Capitol's front at night: washed violet and pink (the 2025 night walk, D:\Unity\ref\yt_nov23\mallnight 7:03)
            Vector2 cfr = c2 + f2 * (D * 0.5f + 1.5f), across = new Vector2(f2.y, -f2.x);
            float cy = !float.IsNaN(capLowY) ? capLowY : ctx.H(cfr);
            Accent(V(cfr, cy + 3f), new Color(0.72f, 0.3f, 1f), 11f, 16f);
            Accent(V(cfr + across * (W * 0.35f), cy + 3f), new Color(1f, 0.3f, 0.75f), 9f, 11f);
            Accent(V(cfr - across * (W * 0.35f), cy + 3f), new Color(0.45f, 0.35f, 1f), 9f, 11f);
            var holder = new GameObject("Video Games sign").transform;
            holder.SetParent(parent, false);
            mb.Box(c + F * (fz + 0.1f) + Vector3.up * 3.6f, new Vector3(Mathf.Min(hw - 2f, 9f), 1.0f, 0.08f), rot, red);
            Label(holder, font, "VIDEO GAMES", c + F * (fz + 0.16f) + Vector3.up * 3.6f, F, 0.55f, Color.white);
            mb.Box(c + F * (fz + 0.1f) + Vector3.up * 4.95f, new Vector3(Mathf.Min(hw - 4f, 6.5f), 0.85f, 0.06f), rot, Atlas.SolidUV(Pal.ShirtWhite));
            Label(holder, font, "ZOOM LAND", c + F * (fz + 0.16f) + Vector3.up * 4.95f, F, 0.45f, new Color(0.75f, 0.08f, 0.08f));
            // the long side above the narrow lane: windows and a side door; the back and the Capitol side are blank
            for (float z = -fz + 2.5f; z < fz - 2f; z += 3.6f)
            {
                mb.Box(c + R * (hw * 0.5f + 0.03f) + F * z + Vector3.up * 4.3f, new Vector3(0.05f, 1.2f, 1.6f), rot, dark);
                mb.Box(c + R * (hw * 0.5f + 0.05f) + F * z + Vector3.up * 4.3f, new Vector3(0.03f, 0.9f, 1.3f), rot, glow);
            }
            mb.Box(c + R * (hw * 0.5f + 0.04f) + F * (fz - 4f) + Vector3.up * 1.05f, new Vector3(0.06f, 2.1f, 1.1f), rot, Atlas.SolidUV(Pal.WoodDark));
            // a water tank and a dish on the roof
            mb.Cylinder(c + R * (hw * 0.25f) - F * (fz * 0.5f) + Vector3.up * (H + 0.1f), 0.7f, 0.7f, 1.3f, 10, Atlas.SolidUV(Pal.IronBlack), true, true);
            mb.Cylinder(c - R * (hw * 0.3f) + F * (fz * 0.4f) + Vector3.up * (H + 0.5f), 0.45f, 0.05f, 0.25f, 10, Atlas.SolidUV(Pal.ShirtWhite), true, true);
        }

        /// <summary>
        /// Inside the game parlour: neon lines along the ceiling's edges (violet, blue, pink), cool white ceiling lights, a
        /// row of arcade machines (ArcadeCabinet: Bandar Bhagao, Pahadi Dash, more of each) down the back wall's side, two
        /// pool tables in the middle (the Pool game), a bowling lane along the lake-side wall (BowlingLane builds its own
        /// lane, pins and ball at runtime), a counter by the door.
        /// </summary>
        static void VideoGamesInside(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector3 c, Vector3 F, Vector3 R, Quaternion rot, float hw, float hd, float H, float doorX)
        {
            float fz = hd * 0.5f;
            Vector3 P(float x, float yy, float z) => c + R * x + F * z + Vector3.up * yy;
            Vector2 dark = Atlas.SolidUV(Pal.IronBlack), red = Atlas.SolidUV(Pal.TempleRed), blue = Atlas.SolidUV(Pal.BoatBlue), yellow = Atlas.SolidUV(Pal.BoatYellow),
                    green = Atlas.SolidUV(Pal.BoatGreen), felt = Atlas.SolidUV(Pal.JacketGreen), wood = Atlas.SolidUV(Pal.WoodDark), glow = Atlas.SolidUV(Pal.WindowGlow);
            var holder = new GameObject("Game parlour (Zoom Land)").transform;
            holder.SetParent(parent, false);

            // the inside painted dark (a game zone, not a white store room): deep blue walls over a black dado, a black ceiling
            {
                Vector2 wallIn = Atlas.SolidUV(Pal.JacketBlue), dado = dark;
                float t = 0.26f;
                void Lining(float x, float z, float sx, float sz)
                {
                    mb.Box(P(x, 0.7f, z), new Vector3(sx, 1.2f, sz), rot, dado);
                    mb.Box(P(x, (1.3f + H - 0.15f) * 0.5f, z), new Vector3(sx, H - 0.15f - 1.3f, sz), rot, wallIn);
                }
                Lining(0f, -fz + t, hw - 2f * t, 0.02f);
                Lining(-hw * 0.5f + t, 0f, 0.02f, hd - 2f * t);
                Lining(hw * 0.5f - t, 0f, 0.02f, hd - 2f * t);
                float dl2 = doorX - 1.25f, dr2 = doorX + 1.25f;
                Lining((-hw * 0.5f + t + dl2) * 0.5f, fz - t, dl2 + hw * 0.5f - t, 0.02f);
                Lining((dr2 + hw * 0.5f - t) * 0.5f, fz - t, hw * 0.5f - t - dr2, 0.02f);
                mb.Box(P(0f, H - 0.19f, 0f), new Vector3(hw - 0.55f, 0.02f, hd - 0.55f), rot, dark);
            }
            // neon along the ceiling's edges
            Color[] neon = { LedViolet, LedBlue, LedPink };
            float ny = H - 0.35f;
            Led(P(-hw * 0.5f + 0.3f, ny, -fz + 0.3f), P(-hw * 0.5f + 0.3f, ny, fz - 0.3f), neon[0], 0.09f, 0.05f, 3f);
            Led(P(hw * 0.5f - 0.3f, ny, -fz + 0.3f), P(hw * 0.5f - 0.3f, ny, fz - 0.3f), neon[1], 0.09f, 0.05f, 3f);
            Led(P(-hw * 0.5f + 0.3f, ny, -fz + 0.3f), P(hw * 0.5f - 0.3f, ny, -fz + 0.3f), neon[2], 0.09f, 0.05f, 3f);
            Led(P(-hw * 0.5f + 0.3f, ny, fz - 0.3f), P(hw * 0.5f - 0.3f, ny, fz - 0.3f), neon[2], 0.09f, 0.05f, 3f);
            // the room's own lamps (owner, 23 Nov 2026: "upar lights theek lgaai hain..but neeche tk jaada light aari nhi hai"):
            // real lights 1.2 m under the ceiling in a 2 x 5 grid, reaching the floor, on day and night; the InteriorZone keeps
            // the sky's ambient out while you are inside and switches them on only near by
            var lamps = new System.Collections.Generic.List<Light>();
            Light Lamp(Vector3 at, Color col2, float range, float intensity)
            {
                var lg = new GameObject("Parlour light").AddComponent<Light>();
                lg.transform.SetParent(holder, false);
                lg.transform.position = at;
                lg.type = LightType.Point;
                lg.color = col2;
                lg.range = range;
                lg.intensity = intensity;
                lg.shadows = LightShadows.None;
                lamps.Add(lg);
                return lg;
            }
            for (float z = -fz + 3.2f; z < fz - 2f; z += 6.4f)
                foreach (float x in new[] { -hw * 0.25f, hw * 0.25f })
                {
                    mb.Box(P(x, H - 0.2f, z), new Vector3(1.2f, 0.05f, 0.3f), rot, glow);   // the tube's housing
                    Lamp(P(x, H - 1.2f, z), new Color(0.88f, 0.92f, 1f), 8.5f, 16f);
                }
            Lamp(P(-1.9f, 2.2f, -7f), new Color(0.75f, 0.35f, 1f), 6f, 7f);    // a violet glow over the arcade island
            Lamp(P(3.2f, 2.2f, -9.5f), new Color(1f, 0.35f, 0.7f), 5f, 6f);     // pink by the air hockey
            var zone = new GameObject("Game parlour (inside)").AddComponent<InteriorZone>();
            zone.transform.SetParent(holder, false);
            zone.transform.SetPositionAndRotation(P(0f, 0f, 0f), rot);
            zone.size = new Vector3(hw - 0.3f, H, hd - 0.3f);

            // the carpet: dark blue with neon squiggles and dots, as game zones have
            var carpet = new MeshBuilder();
            carpet.Quad(P(-hw * 0.5f + 0.3f, 0.125f, -fz + 0.3f), P(hw * 0.5f - 0.3f, 0.125f, -fz + 0.3f), P(hw * 0.5f - 0.3f, 0.125f, fz - 0.3f), P(-hw * 0.5f + 0.3f, 0.125f, fz - 0.3f),
                        new Rect(0f, 0f, (hw - 0.6f) / 3f, (hd - 0.6f) / 3f), Vector3.up);
            ctx.MeshObject("Game parlour carpet", holder, carpet, CarpetMaterial(), null, false);

            // neon boards on the walls: a dark panel, an LED frame, a word
            void Board(float x, float z, Vector3 facing, string word, Color col2, float w)
            {
                Vector3 at = P(x, 3.9f, z);
                var r = Quaternion.LookRotation(facing);
                mb.Box(at, new Vector3(w, 0.9f, 0.05f), r, dark);
                Vector3 side = r * Vector3.right * (w * 0.5f), up = Vector3.up * 0.45f, fw = facing * 0.05f;
                Led(at - side + up + fw, at + side + up + fw, col2, 0.08f, 0.045f, 3f);
                Led(at - side - up + fw, at + side - up + fw, col2, 0.08f, 0.045f, 3f);
                Led(at - side - up + fw, at - side + up + fw, col2, 0.08f, 0.045f, 3f);
                Led(at + side - up + fw, at + side + up + fw, col2, 0.08f, 0.045f, 3f);
                Label(holder, font, word, at + facing * 0.06f, facing, 0.42f, col2);
            }
            Board(-hw * 0.5f + 0.2f, -6f, R, "PLAY ZONE", new Color(0.4f, 0.95f, 1f), 3.6f);
            Board(-hw * 0.5f + 0.2f, 6f, R, "ARCADE", new Color(1f, 0.4f, 0.85f), 3f);
            Board(0f, -fz + 0.2f, F, "HIGH SCORE", new Color(1f, 0.85f, 0.2f), 3.8f);
            Board(hw * 0.5f - 0.2f, -10f, -R, "AIR HOCKEY", new Color(0.5f, 1f, 0.5f), 3.6f);

            // a roof the rain stops on (it only collides with the vehicle layer, WeatherSystem)
            var rainRoof = new GameObject("Rain roof (game parlour)") { layer = VehicleFactory.VehicleLayer };
            rainRoof.transform.SetParent(holder, false);
            rainRoof.transform.SetPositionAndRotation(P(0f, H + 0.2f, 0f), rot);
            rainRoof.AddComponent<BoxCollider>().size = new Vector3(hw + 0.4f, 0.3f, hd + 0.4f);
            // outside: a purple wash on the front, the door lit
            Accent(P(doorX, 3.2f, fz + 1.2f), new Color(0.8f, 0.4f, 1f), 9f, 10f);
            Led(P(-hw * 0.5f, 3.05f, fz + 0.12f), P(hw * 0.5f, 3.05f, fz + 0.12f), LedViolet, 0.1f, 0.05f, 2.8f);

            // the counter by the door
            mb.Box(P(doorX - 3.2f, 0.55f, fz - 2.2f), new Vector3(2.6f, 1.1f, 0.7f), rot, red);
            col.Box(P(doorX - 3.2f, 0.55f, fz - 2.2f), new Vector3(2.6f, 1.1f, 0.7f), rot, Vector2.zero);

            // the arcade machines: down the side away from the lake, facing in
            int made = 0;
            string[] titles = { "Bandar Bhagao", "Pahadi Dash", "Bandar Bhagao", "Pahadi Dash", "Bandar Bhagao", "Pahadi Dash" };
            Vector2[] bodies = { red, blue, yellow, green, red, blue };
            for (int k = 0; k < titles.Length; k++)
            {
                float z = fz - 5f - k * 1.6f;
                if (z < -fz + 2f) break;
                float x = -hw * 0.5f + 0.75f;
                Vector3 b = P(x, 0f, z);
                var faceIn = Quaternion.LookRotation(R);
                mb.Box(b + Vector3.up * 0.9f, new Vector3(1f, 1.8f, 0.8f), faceIn, bodies[k]);
                mb.Box(b + R * 0.41f + Vector3.up * 1.25f, new Vector3(0.75f, 0.55f, 0.02f), faceIn, glow);            // the screen
                mb.Box(b + R * 0.35f + Vector3.up * 1.95f, new Vector3(0.95f, 0.3f, 0.12f), faceIn, yellow);          // the marquee
                mb.Box(b + R * 0.55f + Vector3.up * 0.9f, new Vector3(0.9f, 0.08f, 0.35f), faceIn, dark);             // the panel
                col.Box(b + Vector3.up * 0.9f, new Vector3(1f, 1.8f, 0.8f), faceIn, Vector2.zero);
                Bulb(b + R * 0.42f + Vector3.up * 1.25f, k % 2 == 0 ? LedGreen : LedBlue, 0.32f, 1.1f);
                var go = new GameObject("Arcade " + titles[k]);
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(b + R * 1.0f, faceIn);
                var cab = go.AddComponent<ArcadeCabinet>();
                cab.game = titles[k] == "Bandar Bhagao" ? ArcadeScreen.Game.Bandar : ArcadeScreen.Game.Dash;
                cab.title = titles[k];
                made++;
            }

            // more machines (owner, 23 Nov 2026: "isme 5-10 or arcade games add kro...or waisi hi vibes do...abhi khaali sa basic sa
            // hai"): the wall row on to the back, an island of two rows back to back, two racing seats, two claw machines, a
            // basketball machine and an air hockey table
            Vector2[] palette = { red, blue, yellow, green, Atlas.SolidUV(Pal.SareePink), Atlas.SolidUV(Pal.JacketBlue) };
            void Cabinet(Vector3 b, Quaternion face, Vector2 body, string title, ArcadeScreen.Game game, Color screenCol)
            {
                Vector3 fwd = face * Vector3.forward, rgt = face * Vector3.right;
                mb.Box(b + Vector3.up * 0.9f, new Vector3(1f, 1.8f, 0.8f), face, body);
                mb.Box(b + fwd * 0.41f + Vector3.up * 1.25f, new Vector3(0.75f, 0.55f, 0.02f), face, glow);
                mb.Box(b + fwd * 0.35f + Vector3.up * 1.95f, new Vector3(0.95f, 0.3f, 0.12f), face, yellow);
                mb.Box(b + fwd * 0.55f + Vector3.up * 0.9f, new Vector3(0.9f, 0.08f, 0.35f), face, dark);
                col.Box(b + Vector3.up * 0.9f, new Vector3(1f, 1.8f, 0.8f), face, Vector2.zero);
                Bulb(b + fwd * 0.42f + Vector3.up * 1.25f, screenCol, 0.32f, 1.1f);
                Led(b + fwd * 0.42f + Vector3.up * 2.12f - rgt * 0.48f, b + fwd * 0.42f + Vector3.up * 2.12f + rgt * 0.48f, screenCol, 0.08f, 0.04f, 2.6f);
                var go = new GameObject("Arcade " + title);
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(b + fwd * 1.0f, face);
                var cab = go.AddComponent<ArcadeCabinet>();
                cab.game = game;
                cab.title = title;
                made++;
            }
            Color[] scr = { LedGreen, LedBlue, LedPink, LedGold };
            var inR = Quaternion.LookRotation(R);
            for (int k = 0; k < 7; k++)   // the wall row, on past the first six
            {
                float z = fz - 5f - (titles.Length + k) * 1.6f;
                if (z < -fz + 2.2f) break;
                Cabinet(P(-hw * 0.5f + 0.75f, 0f, z), inR, palette[k % palette.Length], k % 2 == 0 ? "Pahadi Dash" : "Bandar Bhagao", k % 2 == 0 ? ArcadeScreen.Game.Dash : ArcadeScreen.Game.Bandar, scr[k % scr.Length]);
            }
            for (int k = 0; k < 5; k++)   // the island: two rows back to back
            {
                float z = -4.6f - k * 1.15f;
                Cabinet(P(-2.4f, 0f, z), Quaternion.LookRotation(-R), palette[(k + 2) % palette.Length], "Bandar Bhagao", ArcadeScreen.Game.Bandar, scr[(k + 1) % scr.Length]);
                Cabinet(P(-1.55f, 0f, z), inR, palette[(k + 4) % palette.Length], "Pahadi Dash", ArcadeScreen.Game.Dash, scr[(k + 2) % scr.Length]);
            }
            for (int k = 0; k < 2; k++)   // two racing seats against the back wall: a bucket seat, a wheel, a big screen
            {
                Vector3 b = P(-4.2f + k * 1.9f, 0f, -fz + 1.6f);
                var face = Quaternion.LookRotation(-F);
                Vector3 fwd = -F;
                mb.Box(b + Vector3.up * 0.35f, new Vector3(1.4f, 0.7f, 2.2f), face, dark);
                mb.Box(b - fwd * 0.4f + Vector3.up * 0.95f, new Vector3(0.8f, 0.9f, 0.2f), face, red);
                mb.Box(b + Vector3.up * 0.8f, new Vector3(0.8f, 0.15f, 0.7f), face, red);
                mb.Cylinder(b + fwd * 0.55f + Vector3.up * 1.05f, 0.18f, 0.18f, 0.05f, 12, dark, true, true);
                mb.Box(b + fwd * 1.0f + Vector3.up * 1.5f, new Vector3(1.3f, 1.0f, 0.3f), face, blue);
                mb.Box(b + fwd * 0.84f + Vector3.up * 1.5f, new Vector3(1.15f, 0.8f, 0.02f), face, glow);
                Bulb(b + fwd * 0.84f + Vector3.up * 1.5f, LedBlue, 0.5f, 1f);
                col.Box(b + Vector3.up * 0.8f, new Vector3(1.4f, 1.6f, 2.2f), face, Vector2.zero);
                var go = new GameObject("Arcade racer " + (k + 1));
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(b - fwd * 1.4f, face);
                var cab = go.AddComponent<ArcadeCabinet>(); cab.game = ArcadeScreen.Game.Dash; cab.title = "Pahadi Racer"; cab.price = 30; made++;
            }
            for (int k = 0; k < 2; k++)   // two claw machines: a glass box of toys on a coloured base
            {
                Vector3 b = P(0.2f + k * 1.3f, 0f, -fz + 1.2f);
                var face = Quaternion.LookRotation(-F);
                mb.Box(b + Vector3.up * 0.5f, new Vector3(1.1f, 1.0f, 1.1f), face, k == 0 ? red : Atlas.SolidUV(Pal.SareePink));
                mb.Box(b + Vector3.up * 1.55f, new Vector3(1.05f, 1.1f, 1.05f), face, Atlas.MaterialUV(Atlas.MatGlass));
                mb.Box(b + Vector3.up * 2.2f, new Vector3(1.15f, 0.2f, 1.15f), face, yellow);
                for (int t = 0; t < 7; t++) mb.Sphere(b + Vector3.up * (1.1f + (t % 2) * 0.12f) + R * (((t * 37) % 7 - 3) * 0.11f) + F * (((t * 53) % 5 - 2) * 0.13f), Vector3.one * 0.13f, 6, 3, palette[t % palette.Length]);
                mb.Limb(b + Vector3.up * 2.05f, b + Vector3.up * 1.6f, 0.015f, 0.015f, 3, dark, false);
                Bulb(b + Vector3.up * 2.05f, LedPink, 0.35f, 1.4f);
                col.Box(b + Vector3.up * 1.1f, new Vector3(1.1f, 2.2f, 1.1f), face, Vector2.zero);
                var go = new GameObject("Claw machine " + (k + 1));
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(b + F * 1.0f, face);
                var cab = go.AddComponent<ArcadeCabinet>(); cab.game = ArcadeScreen.Game.Bandar; cab.title = "Claw Machine"; made++;
            }
            {   // a basketball machine: a long ramp up to a backboard with a hoop, lit
                Vector3 b = P(hw * 0.5f - 1.1f, 0f, -fz + 2.6f);
                var along = Quaternion.LookRotation(F);
                mb.Box(b + Vector3.up * 0.5f, new Vector3(1.1f, 1.0f, 3.0f), along, Atlas.SolidUV(Pal.JacketBlue));
                mb.Box(b + Vector3.up * 2.4f - F * 1.35f, new Vector3(1.1f, 1.1f, 0.08f), along, Atlas.SolidUV(Pal.ShirtWhite));
                mb.Cylinder(b + Vector3.up * 2.0f - F * 1.1f, 0.24f, 0.24f, 0.04f, 12, red, true, true);
                for (int t = 0; t < 4; t++) mb.Sphere(b + Vector3.up * 1.12f + F * (0.9f - t * 0.25f), Vector3.one * 0.12f, 6, 3, yellow);
                Led(b + Vector3.up * 2.98f - F * 1.32f - R * 0.55f, b + Vector3.up * 2.98f - F * 1.32f + R * 0.55f, LedGold, 0.08f, 0.045f, 3f);
                col.Box(b + Vector3.up * 1.2f, new Vector3(1.1f, 2.4f, 3.0f), along, Vector2.zero);
                var go = new GameObject("Arcade basketball");
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(b + F * 2.1f, Quaternion.LookRotation(-F));
                var cab = go.AddComponent<ArcadeCabinet>(); cab.game = ArcadeScreen.Game.Bandar; cab.title = "Basketball"; made++;
            }
            {   // an air hockey table
                Vector3 b = P(3.2f, 0f, -9.5f);
                mb.Box(b + Vector3.up * 0.4f, new Vector3(1.3f, 0.8f, 2.3f), rot, Atlas.SolidUV(Pal.JacketBlue));
                mb.Box(b + Vector3.up * 0.82f, new Vector3(1.15f, 0.03f, 2.15f), rot, Atlas.SolidUV(Pal.ShirtWhite));
                mb.Box(b + Vector3.up * 0.835f, new Vector3(1.15f, 0.01f, 0.03f), rot, red);
                foreach (float zz in new[] { -0.8f, 0.8f }) mb.Cylinder(b + F * zz + Vector3.up * 0.84f, 0.08f, 0.08f, 0.06f, 10, red, true);
                col.Box(b + Vector3.up * 0.45f, new Vector3(1.3f, 0.9f, 2.3f), rot, Vector2.zero);
                var go = new GameObject("Air hockey");
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(b + F * 1.6f, rot);
                var cab = go.AddComponent<ArcadeCabinet>(); cab.game = ArcadeScreen.Game.Pool; cab.title = "Air Hockey"; made++;
            }

            // two pool tables down the middle
            for (int k = 0; k < 2; k++)
            {
                float z = fz - 7f - k * 5.5f;
                Vector3 b = P(-hw * 0.05f, 0f, z);
                mb.Box(b + Vector3.up * 0.4f, new Vector3(1.3f, 0.8f, 2.5f), rot, wood);
                mb.Box(b + Vector3.up * 0.81f, new Vector3(1.12f, 0.03f, 2.3f), rot, felt);
                foreach (float sx in new[] { -0.6f, 0.6f }) mb.Box(b + R * sx + Vector3.up * 0.86f, new Vector3(0.12f, 0.1f, 2.5f), rot, wood);
                foreach (float sz in new[] { -1.2f, 1.2f }) mb.Box(b + F * sz + Vector3.up * 0.86f, new Vector3(1.3f, 0.1f, 0.12f), rot, wood);
                mb.Box(b + Vector3.up * (H - 1.6f), new Vector3(0.5f, 0.15f, 1.6f), rot, dark);                       // the lamp over it
                Bulb(b + Vector3.up * (H - 1.72f), new Color(1f, 0.9f, 0.7f), 0.4f, 1.2f);
                Lamp(b + Vector3.up * (H - 1.9f), new Color(1f, 0.88f, 0.68f), 4.5f, 10f);
                col.Box(b + Vector3.up * 0.45f, new Vector3(1.3f, 0.9f, 2.5f), rot, Vector2.zero);
                var go = new GameObject("Pool table " + (k + 1));
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(b + R * 1.0f, rot);
                var cab = go.AddComponent<ArcadeCabinet>();
                cab.game = ArcadeScreen.Game.Pool;
                cab.title = "Pool";
                cab.price = 30;
                made++;
            }

            // the bowling lane along the lake-side wall, its foul line near the front, rolling toward the back
            float laneLen = Mathf.Min(14f, hd - 9f);
            var lane = new GameObject("Bowling lane");
            lane.transform.SetParent(holder, false);
            lane.transform.SetPositionAndRotation(P(hw * 0.5f - 1.25f, 0.28f, fz - 4.5f), Quaternion.LookRotation(-F));   // on its own 16 cm base
            lane.AddComponent<BowlingLane>().length = laneLen;
            Label(holder, font, "BOWLING", P(hw * 0.5f - 1.25f, H - 1.4f, fz - 4.5f - laneLen - 0.6f), F, 0.5f, new Color(1f, 0.5f, 0.9f));
            Label(holder, font, "GAME ZONE", P(0f, H - 1.2f, -fz + 0.2f), F, 0.6f, new Color(0.6f, 0.9f, 1f));
            zone.lights = lamps.ToArray();
            Debug.Log($"[Nainital] game parlour: {made} machines / tables, {lamps.Count} lamps, bowling lane {laneLen:F1} m, hall {hw:F1} x {hd:F1} m");
        }

        /// <summary>The parlour's carpet: dark blue with neon squiggles, rings and dots (a texture made here, saved with the
        /// generated assets), 3 m a repeat.</summary>
        static Material CarpetMaterial()
        {
            string texPath = Gen + "/T_ArcadeCarpet.asset", matPath = Gen + "/M_ArcadeCarpet.mat";
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "T_ArcadeCarpet" };
            var px = new Color[N * N];
            Color bg = new Color(0.04f, 0.05f, 0.14f);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            var rnd = new System.Random(808);
            Color[] cols = { new Color(0.2f, 0.9f, 1f), new Color(1f, 0.3f, 0.8f), new Color(1f, 0.85f, 0.2f), new Color(0.5f, 1f, 0.4f) };
            void Dot(int x, int y, int r, Color c)
            {
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (dx * dx + dy * dy <= r * r) px[((y + dy + N * 4) % N) * N + ((x + dx + N * 4) % N)] = c;
            }
            for (int k = 0; k < 40; k++)
            {
                float x = rnd.Next(N), y = rnd.Next(N), a = (float)rnd.NextDouble() * 6.28f;
                var c = cols[k % cols.Length];
                for (int t = 0; t < 40; t++) { a += Mathf.Sin(t * 0.4f) * 0.35f; x += Mathf.Cos(a) * 1.2f; y += Mathf.Sin(a) * 1.2f; Dot((int)x, (int)y, 1, c); }
            }
            for (int k = 0; k < 22; k++)
            {
                int x = rnd.Next(N), y = rnd.Next(N), r = 4 + rnd.Next(5);
                var c = cols[(k + 1) % cols.Length];
                for (int t = 0; t < 60; t++) { float a = t / 60f * 6.28f; Dot(x + (int)(Mathf.Cos(a) * r), y + (int)(Mathf.Sin(a) * r), 0, c); }
            }
            for (int k = 0; k < 120; k++) Dot(rnd.Next(N), rnd.Next(N), rnd.Next(2), cols[k % cols.Length] * 0.9f);
            tex.SetPixels(px);
            tex.Apply(true);
            if (System.IO.File.Exists(texPath)) UnityEditor.AssetDatabase.DeleteAsset(texPath);
            UnityEditor.AssetDatabase.CreateAsset(tex, texPath);
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); UnityEditor.AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.05f);
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The hall's footprint as circles, for the plan (no generated houses there).</summary>
        static void KeepVideoGamesClear(WorldCtx ctx)
        {
            var g = CapitolGeo(ctx);
            if (g == null) return;
            float hw = g.hallS1 - g.hallS0, sc = (g.hallS0 + g.hallS1) * 0.5f;
            for (float a = g.hallA0; a <= g.hallA1 + 0.01f; a += Mathf.Max(2f, hw * 0.4f))
                ctx.keepClear.Add((CapW(a, sc), hw * 0.5f + 2.5f));
            ctx.keepClear.Add((CapW(g.hallA1, sc), hw * 0.5f + 2.5f));
        }
    }
}

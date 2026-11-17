using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Mall Road's shop fronts by kind (user, 2026-11-17: "mall road pe same type ki hi shops mat rakho ... kuch
    /// restaurant, kuch shops, kuch road shops, kuch rooftop cafe ... front look bhi random hoga", and the low-poly goods
    /// go: the fake interior shows them). Each building front on Upper Mall is cut into shop units of ~4 m; each unit
    /// gets a room (one of the Flow photos, Atlas.Room*) by where it stands, from the walk in REFERENCE_REPORT video 02
    /// (Tallital gate 4:20 to Mallital 28:20, written down every 8 s on 17 Nov): restaurants and hotels where the video
    /// has them, gift and woollen shops where it has them, shutters down on the quiet stretches. The unit's front is built
    /// to match: a glass front with mullions, kick plate and a door for restaurants, cafes, banks, opticians and hotels
    /// (a canopy and two pots at a hotel); an open front under a rolled-up shutter for shops; a shutter for a closed one,
    /// sometimes with a kiosk in front of it (a road shop). Frame colour, awning, door side and panes are random per unit.
    /// The room itself is ShopGlass.shader (photo on a plane behind the opening).
    /// </summary>
    public static partial class WorldBuilder
    {
        const int Shut = -1;

        /// <summary>Video 02 seconds -> the shops seen there (Atlas.Room*, Shut = shutter down), in walking order.</summary>
        static readonly (float from, float to, int[] rooms)[] MallSegments =
        {
            (260f, 450f,  new[] { Atlas.RoomPharmacy, Shut, Atlas.RoomHotelHeritage, Atlas.RoomLocalProduce, Shut, Atlas.RoomHotelDesk }),       // Tallital end: trees, hotels up the slope
            (450f, 505f,  new[] { Atlas.RoomRestPunjabi, Atlas.RoomLocalProduce, Atlas.RoomCafe, Atlas.RoomRestFamily }),                         // Darpan, the wine shop, the umbrella sit-out
            (505f, 670f,  new[] { Shut, Atlas.RoomPharmacy, Shut, Atlas.RoomLocalProduce, Atlas.RoomHotelHeritage, Shut, Atlas.RoomSouvenir }),  // the long stone wall, St. Francis: few shops
            (670f, 775f,  new[] { Atlas.RoomHotelHeritage, Atlas.RoomRestMulti, Atlas.RoomHotelDesk, Atlas.RoomBakery }),                         // Hotel India, Hotel Everest
            (775f, 855f,  new[] { Atlas.RoomWoollens, Atlas.RoomTravel, Atlas.RoomGiftGallery, Atlas.RoomIceCream, Atlas.RoomFastFood, Atlas.RoomEthnic }),   // Wildcraft, Darshan Travels, Diamond Gift Gallery, Amul
            (855f, 925f,  new[] { Shut, Atlas.RoomSouvenir }),
            (925f, 1020f, new[] { Atlas.RoomFastFood, Atlas.RoomFashion, Shut, Atlas.RoomEthnic }),                                              // Subway and the game parlour
            (1020f, 1215f, new[] { Atlas.RoomHotelDesk, Atlas.RoomRestMulti, Atlas.RoomBags, Atlas.RoomFashion, Atlas.RoomTibetanCraft, Atlas.RoomSouvenir,
                                   Atlas.RoomCafe, Atlas.RoomTravel, Atlas.RoomLocalProduce, Atlas.RoomHotelHeritage, Atlas.RoomGiftGallery, Atlas.RoomRestTibetan }),  // Gurdeep, Moonlight, Hotel Central, Fire & Ice: the busiest stretch
            (1215f, 1290f, new[] { Shut }),                                                                                                     // the wall with the mural
            (1290f, 1345f, new[] { Atlas.RoomBank, Atlas.RoomOptical, Atlas.RoomBooks, Atlas.RoomMobile, Atlas.RoomFashion, Atlas.RoomHotelDesk }),  // Nainital Bank, HDFC, Lenskart
            (1345f, 1400f, new[] { Atlas.RoomSouvenir, Atlas.RoomFootwear, Atlas.RoomGiftGallery, Atlas.RoomEthnic, Atlas.RoomCandles }),
            (1400f, 1450f, new[] { Atlas.RoomHotelHeritage, Atlas.RoomCafe, Atlas.RoomSouvenir, Shut }),                                        // The Grand Hotel
            (1450f, 1510f, new[] { Atlas.RoomBooks, Atlas.RoomCandles, Atlas.RoomGiftGallery, Atlas.RoomSweets }),                               // Kodak, Candle Bazaar
            (1510f, 1590f, new[] { Atlas.RoomRestPunjabi, Atlas.RoomFootwear, Atlas.RoomFashion, Atlas.RoomSweets, Atlas.RoomCafe, Atlas.RoomBakery }),  // Nanak, Bata, the cafe deck
            (1590f, 1650f, new[] { Shut, Atlas.RoomPharmacy, Shut }),                                                                           // the Methodist church
            (1650f, 1800f, new[] { Atlas.RoomShawls, Atlas.RoomWoollens, Atlas.RoomHotelDesk, Atlas.RoomShawls, Atlas.RoomFastFood }),           // shawls, Woodland, Hotel Flatties
        };
        static readonly int[] MallOther = { Atlas.RoomSouvenir, Atlas.RoomFashion, Atlas.RoomCafe, Shut, Atlas.RoomGiftGallery };

        /// <summary>The stretches busy enough for a rooftop cafe (video 02 seconds).</summary>
        internal static bool MallBusy(float sec) =>
            (sec > 670f && sec < 775f) || (sec > 1020f && sec < 1215f) || (sec > 1290f && sec < 1400f) || (sec > 1450f && sec < 1590f);

        static WorldCtx mallLineFor;
        static Vector2[] mallLine;
        static float[] mallLineS;
        static float mallLineLen;
        static (float progress, float sec)[] mallAnchors;

        /// <summary>0 at Upper Mall's Tallital end, 1 at Mallital: the direction of the walk in video 02.</summary>
        static float MallProgress(WorldCtx ctx, Vector2 p)
        {
            if (mallLineFor != ctx)
            {
                mallLineFor = ctx;
                mallLine = null;
                foreach (var r in ctx.world.roads)
                    if (r.name == "Upper Mall Road") { mallLine = Geo.ToPoints(r.pts); break; }
                if (mallLine != null)
                {
                    // the line runs from Mallital (north) to Tallital: walk it backwards
                    System.Array.Reverse(mallLine);
                    mallLineS = new float[mallLine.Length];
                    for (int i = 1; i < mallLine.Length; i++) mallLineS[i] = mallLineS[i - 1] + Vector2.Distance(mallLine[i - 1], mallLine[i]);
                    mallLineLen = Mathf.Max(1f, mallLineS[mallLine.Length - 1]);
                }
                // video 02: the Tallital gate at 4:20, St. Francis church up on the right at 8:48, the Grand Hotel arch at
                // 23:48, Mallital at 28:20
                var anchors = new List<(float, float)> { (0f, 260f), (1f, 1700f) };
                if (mallLine != null)
                {
                    var church = ctx.Poi("St. Francis");
                    var grand = ctx.Poi("The Grand Hotel");
                    if (church.HasValue) anchors.Add((ProgressOn(church.Value), 528f));
                    if (grand.HasValue) anchors.Add((ProgressOn(grand.Value), 1428f));
                }
                anchors.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                mallAnchors = anchors.ToArray();
            }
            return mallLine == null ? 0.5f : ProgressOn(p);
        }

        static float ProgressOn(Vector2 p)
        {
            float best = float.MaxValue, at = 0f;
            for (int i = 0; i + 1 < mallLine.Length; i++)
            {
                Vector2 a = mallLine[i], ab = mallLine[i + 1] - a;
                float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f), t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best) { best = d; at = mallLineS[i] + Mathf.Sqrt(len2) * t; }
            }
            return at / mallLineLen;
        }

        /// <summary>Where a point on Mall Road comes in video 02, in seconds.</summary>
        internal static float MallVideoSeconds(WorldCtx ctx, Vector2 p)
        {
            float t = MallProgress(ctx, p);
            for (int i = 0; i + 1 < mallAnchors.Length; i++)
            {
                var (t0, s0) = mallAnchors[i];
                var (t1, s1) = mallAnchors[i + 1];
                if (t <= t1 || i + 2 == mallAnchors.Length)
                    return Mathf.Lerp(s0, s1, Mathf.InverseLerp(t0, t1, t));
            }
            return 1000f;
        }

        static int MallSegmentAt(float sec)
        {
            for (int i = 0; i < MallSegments.Length; i++)
                if (sec >= MallSegments[i].from && sec < MallSegments[i].to) return i;
            return -1;
        }

        // ------------------------------------------------------------------ the fronts
        static readonly int[] FramePal = { Pal.WoodDark, Pal.WoodDark, Pal.WoodLight, Pal.StoneGrey, Pal.IronBlack, Pal.ShirtWhite, Pal.IronGreen, Pal.TempleRed };
        static readonly int[] AwningPal = { Pal.BoatBlue, Pal.Awning, Pal.BoatYellow, Pal.Canopy, Pal.JacketRed, Pal.BoatGreen, Pal.TempleRed };
        static readonly int[] KioskRooms = { Atlas.RoomFastFood, Atlas.RoomIceCream, Atlas.RoomCafe, Atlas.RoomSouvenir, Atlas.RoomTibetanCraft, Atlas.RoomMobile };

        sealed class MallFrontSet
        {
            public readonly Dictionary<int, int> next = new Dictionary<int, int>();
            /// <summary>Units built so far (centre, out, along, half width): two houses of the data overlap now and then,
            /// and their fronts on one line made two glass quads fight in the depth buffer (speckle, 17 Nov).</summary>
            public readonly List<(Vector3 c, Vector3 n, Vector3 a, float hw)> placed = new List<(Vector3, Vector3, Vector3, float)>();
            public int units, glassUnits, openUnits, shutUnits, kiosks, overlapped;
            public readonly int[] perRoom = new int[Atlas.RoomCols * Atlas.RoomRows];
        }

        /// <summary>The next room for a unit at video second `sec`: the stretch's list in order, so neighbours differ.</summary>
        static int NextMallRoom(MallFrontSet set, float sec)
        {
            int seg = MallSegmentAt(sec);
            int[] list = seg >= 0 ? MallSegments[seg].rooms : MallOther;
            set.next.TryGetValue(seg, out int k);
            set.next[seg] = k + 1;
            return list[k % list.Length];
        }

        /// <summary>One building's ground-floor front on Mall Road: wall = the facade plane at the shop floor's foot,
        /// floor = top of the step in front, w = frontage.</summary>
        static void MallFront(WorldCtx ctx, MallFrontSet set, MeshBuilder mb, MeshBuilder glass, MeshBuilder letters, Vector3 wall, Vector3 nrm, float floorY,
                              float houseFloor, float w, float sec, System.Random rnd)
        {
            // the opening stops under the first floor (its verandah slab is at houseFloor + 3): the step lifts the shop floor
            // to the road, often half a metre over the house's own floor, and a 2.75 m front ran into the slab and hid the
            // name boards (18 Nov)
            float H = Mathf.Clamp(houseFloor + 2.9f - floorY, 2.0f, 2.75f);
            Vector3 along = Vector3.Cross(Vector3.up, nrm).normalized;
            int units = Mathf.Max(1, Mathf.RoundToInt(w / 4.3f));
            float uw = w / units;
            for (int u = 0; u < units; u++)
            {
                Vector3 c = new Vector3(wall.x, floorY, wall.z) + along * ((u + 0.5f) * uw - w * 0.5f);
                bool clash = false;
                foreach (var b in set.placed)
                {
                    Vector3 dl = c - b.c;
                    dl.y = 0f;
                    if (Vector3.Dot(nrm, b.n) > 0.7f && Mathf.Abs(Vector3.Dot(dl, b.n)) < 1.2f && Mathf.Abs(Vector3.Dot(dl, b.a)) < uw * 0.5f + b.hw - 0.3f) { clash = true; break; }
                }
                if (clash) { set.overlapped++; continue; }
                set.placed.Add((c, nrm, along, uw * 0.5f));
                int room = NextMallRoom(set, sec);
                MallUnit(ctx, set, mb, glass, letters, c, nrm, along, uw, H, room, rnd);
            }
        }

        static void MallUnit(WorldCtx ctx, MallFrontSet set, MeshBuilder mb, MeshBuilder glass, MeshBuilder letters, Vector3 c, Vector3 nrm, Vector3 along,
                             float uw, float H, int room, System.Random rnd)
        {
            const float F = 0.12f;   // frame member; H: the opening's height above the floor
            var rot = Quaternion.LookRotation(nrm);
            Vector3 up = Vector3.up;
            Vector2 frame = Atlas.SolidUV(FramePal[rnd.Next(FramePal.Length)]);
            float half = uw * 0.5f, gx = half - F;
            set.units++;
            // posts each side and the header beam over the opening
            foreach (float s in new[] { -1f, 1f })
                mb.Box(c + along * (s * (half - F * 0.5f)) + nrm * 0.09f + up * (H * 0.5f), new Vector3(F, H, 0.18f), rot, frame);
            mb.Box(c + nrm * 0.09f + up * (H + 0.16f), new Vector3(uw, 0.32f, 0.2f), rot, frame);

            if (room == Shut || glass == null)
            {
                // rolling shutter, down (the facade art's shut shutter: wall colour 0's dark shop cell)
                Rect cell = Atlas.Facade(0, Atlas.StyleShopDark);
                var shutterUV = new Rect(cell.x + cell.width * 3f / 64f, cell.y, cell.width * 58f / 64f, cell.height * 37.8f / 64f);
                Vector3 s0 = c + nrm * 0.05f;
                mb.Quad(s0 - along * gx, s0 + along * gx, s0 + along * gx + up * (H - 0.3f), s0 - along * gx + up * (H - 0.3f), shutterUV, nrm);
                mb.Box(c + nrm * 0.14f + up * (H - 0.15f), new Vector3(uw - 2f * F, 0.3f, 0.3f), rot, Atlas.SolidUV(Pal.StoneGrey));
                set.shutUnits++;
                MallSign(mb, letters, c, nrm, uw, H, Shut, rnd);   // a shut shop keeps its board
                // a road shop set up on the step in front of the shut one (the pavement is too narrow beyond it)
                if (glass != null && rnd.NextDouble() < 0.6) MallKiosk(ctx, set, mb, glass, letters, c + nrm * 0.78f, nrm, along, rnd);
                return;
            }
            set.perRoom[room]++;
            MallSign(mb, letters, c, nrm, uw, H, room, rnd);
            bool eatery = Atlas.RoomIsEatery(room), desk = Atlas.RoomIsDesk(room), hotel = Atlas.RoomIsHotel(room);
            bool open = !eatery && !desk;
            // the room: one tagged quad over the opening (uv = metres from the opening's centre, to the right as seen
            // from the street, i.e. along -along; and up from the floor)
            Vector3 g = c + nrm * 0.05f;
            var tag = new Color32((byte)room, (byte)(open ? 255 : 0), 0, 255);
            glass.QuadTagged(g - along * gx, g + along * gx, g + along * gx + up * H, g - along * gx + up * H,
                             new Vector2(gx, 0f), new Vector2(-gx, 0f), new Vector2(-gx, H), new Vector2(gx, H), nrm, tag);
            if (open)
            {
                set.openUnits++;
                mb.Box(c + nrm * 0.14f + up * (H - 0.17f), new Vector3(uw - 2f * F, 0.34f, 0.26f), rot, Atlas.SolidUV(Pal.StoneGrey));   // the shutter, rolled up
                mb.Box(c + nrm * 0.18f + up * 0.03f, new Vector3(uw - 2f * F, 0.06f, 0.34f), rot, Atlas.MaterialUV(Atlas.MatMarble));    // threshold
            }
            else
            {
                set.glassUnits++;
                int panes = Mathf.Max(2, Mathf.RoundToInt(2f * gx / 1.2f));
                float pw = 2f * gx / panes;
                int door = hotel ? panes / 2 : (rnd.Next(2) == 0 ? 0 : panes - 1);
                Vector2 bar = rnd.NextDouble() < 0.5 ? frame : Atlas.SolidUV(Pal.StoneGrey);
                for (int k = 1; k < panes; k++)
                    mb.Box(c + along * (-gx + k * pw) + nrm * 0.09f + up * (H * 0.5f), new Vector3(0.06f, H, 0.08f), rot, bar);
                mb.Box(c + nrm * 0.09f + up * (H - 0.55f), new Vector3(2f * gx, 0.06f, 0.08f), rot, bar);   // transom, just under the name board
                for (int k = 0; k < panes; k++)
                {
                    float x = -gx + (k + 0.5f) * pw;
                    if (k == door)
                    {
                        float hx = x + (door == 0 ? 0.32f : -0.32f) * pw / 1.2f;
                        mb.Box(c + along * hx + nrm * 0.13f + up * 1.05f, new Vector3(0.035f, 0.6f, 0.04f), rot, Atlas.SolidUV(Pal.Gold));   // door pull
                        continue;
                    }
                    mb.Box(c + along * x + nrm * 0.09f + up * 0.21f, new Vector3(pw, 0.42f, 0.08f), rot, bar);   // kick plate
                }
                if (hotel)
                {
                    // entrance canopy and two pots by the door
                    float dx = -gx + (door + 0.5f) * pw;
                    Vector3 cc = c + along * dx;
                    mb.Box(cc + nrm * 0.8f + up * (H + 0.38f), new Vector3(Mathf.Min(uw * 0.8f, 3f), 0.12f, 1.5f), rot, Atlas.SolidUV(Pal.ShirtWhite));
                    mb.Box(cc + nrm * 1.52f + up * (H + 0.28f), new Vector3(Mathf.Min(uw * 0.8f, 3f), 0.3f, 0.06f), rot, Atlas.SolidUV(Pal.TempleRed));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 pot = cc + along * (s * (pw * 0.5f + 0.35f)) + nrm * 0.5f;
                        mb.Cylinder(pot, 0.2f, 0.25f, 0.45f, 8, Atlas.MaterialUV(Atlas.MatTempleOrange), true);
                        mb.Sphere(pot + up * 0.8f, new Vector3(0.36f, 0.42f, 0.36f), 7, 4, Atlas.SolidUV(Pal.Pine));
                    }
                }
            }
            // a cloth awning over the opening, on some
            if (!hotel && !desk && rnd.NextDouble() < 0.55)
            {
                var awn = new Rect(Atlas.SolidUV(AwningPal[rnd.Next(AwningPal.Length)]), Vector2.zero);
                Vector3 top = c + up * (H + 0.42f) + nrm * 0.2f, outer = c + up * (H + 0.02f) + nrm * 1.15f;
                Vector3 hw = along * half;
                mb.QuadDouble(top - hw, top + hw, outer + hw, outer - hw, awn, up);
                mb.QuadDouble(outer - hw + Vector3.down * 0.2f, outer + hw + Vector3.down * 0.2f, outer + hw, outer - hw, awn, nrm);
            }
        }

        /// <summary>A road shop: a small kiosk in front of a closed shop, its window open on the street (fast food, ice
        /// cream, chai, souvenirs ...), the room behind it a photo like the shops'.</summary>
        static void MallKiosk(WorldCtx ctx, MallFrontSet set, MeshBuilder mb, MeshBuilder glass, MeshBuilder letters, Vector3 at, Vector3 nrm, Vector3 along, System.Random rnd)
        {
            // it stands inside the step in front of the shop (built 1.4 m deep without a road check, like the step):
            // Upper Mall's road width reaches the shop line, so a road clearance test refused every one (17 Nov)
            var rot = Quaternion.LookRotation(nrm);
            Vector3 up = Vector3.up;
            Vector2 body = Atlas.SolidUV(new[] { Pal.BoatBlue, Pal.JacketRed, Pal.BoatYellow, Pal.BoatGreen, Pal.ShirtWhite }[rnd.Next(5)]);
            const float W = 1.6f, D = 1.2f, Ht = 2.3f;
            // body, roof with overhang, counter ledge; the window faces the street (nrm)
            mb.Box(at + up * 0.5f, new Vector3(W, 1.0f, D), rot, body);                                   // below the counter
            mb.Box(at + up * (Ht - 0.2f), new Vector3(W, 0.4f, D), rot, body);                            // above the window
            foreach (float s in new[] { -1f, 1f })
                mb.Box(at + along * (s * (W * 0.5f - 0.06f)) + up * 1.5f, new Vector3(0.12f, 1.0f, D), rot, body);   // window sides
            mb.Box(at - nrm * (D * 0.5f - 0.06f) + up * 1.5f, new Vector3(W, 1.0f, 0.12f), rot, body);    // back
            mb.Box(at + up * (Ht + 0.05f), new Vector3(W + 0.4f, 0.1f, D + 0.5f), rot, Atlas.SolidUV(Pal.TempleRed));   // roof
            mb.Box(at + nrm * (D * 0.5f + 0.1f) + up * 1.02f, new Vector3(W, 0.05f, 0.3f), rot, Atlas.MaterialUV(Atlas.MatWood));   // counter
            int room = KioskRooms[rnd.Next(KioskRooms.Length)];
            Vector3 g = at + nrm * (D * 0.5f - 0.02f);
            const float gx = W * 0.5f - 0.12f;
            var tag = new Color32((byte)room, 255, 0, 255);
            glass.QuadTagged(g - along * gx + up * 1.0f, g + along * gx + up * 1.0f, g + along * gx + up * 2.0f, g - along * gx + up * 2.0f,
                             new Vector2(gx, 1.0f), new Vector2(-gx, 1.0f), new Vector2(-gx, 2.0f), new Vector2(gx, 2.0f), nrm, tag);
            set.kiosks++;
            set.perRoom[room]++;
            // its name on the panel over the window
            string name = Glyph3D.NameFor(room, rnd);
            var st = Glyph3D.StyleFor(room);
            Glyph3D.Pick(st, rnd, out int _, out Vector2 front, out Vector2 side);
            if (letters != null) Glyph3D.Write(letters, name, st.face, at + nrm * (D * 0.5f) + up * (Ht - 0.2f), nrm, 0.17f, 0.03f, W - 0.2f, front, side);
        }

        /// <summary>The shop's name board over its opening (it hides the shutter box and the transom), the name in solid
        /// letters in the style of its kind (Glyph3D: font, board and letter colours; some letters glow at night).</summary>
        static void MallSign(MeshBuilder mb, MeshBuilder letters, Vector3 c, Vector3 nrm, float uw, float H, int room, System.Random rnd)
        {
            string name = Glyph3D.NameFor(room, rnd);
            var st = room >= 0 ? Glyph3D.StyleFor(room) : Glyph3D.StyleForName(name);
            Glyph3D.Pick(st, rnd, out int board, out Vector2 front, out Vector2 side);
            Vector3 mid = c + nrm * 0.2f + Vector3.up * (H - 0.13f);
            mb.Box(mid, new Vector3(uw - 0.1f, 0.7f, 0.1f), Quaternion.LookRotation(nrm), Atlas.SolidUV(board));
            if (letters != null) Glyph3D.Write(letters, name, st.face, mid + nrm * 0.05f, nrm, 0.3f, 0.05f, uw - 0.5f, front, side);
        }

        /// <summary>True within d metres of Upper Mall Road's line: the fronts MallShopDressing dresses (their boards
        /// come from MallSign, not from the street details' signs).</summary>
        internal static bool OnUpperMall(WorldCtx ctx, Vector2 p, float d)
        {
            MallProgress(ctx, p);
            return mallLine != null && Geo.DistanceToPolyline(mallLine, p, false) <= d;
        }
    }
}

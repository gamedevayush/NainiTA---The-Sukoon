using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class LandmarkBuilder
    {
        // ------------------------------------------------------------------ Snow View: the adventure park and its market
        // Owner, 23 Nov 2026: "upar 360 tower or uske paas ka market with real structure me nhi hai...pehle reseach kro ki
        // wahan kya kya hain...images ko dekhke bnaao..maps bhi dekna". From the 2024-25 videos (D:\Unity\ref\yt_nov23\
        // snowmkt, snow360, rope): "Adventure Park - Mountain Magic" behind a blue bottle-cap mosaic arch, its board listing
        // Tower 360, Bumping Car, Go-karting, Zip Line, Zip Cycle, Climbing Wall; the go-kart track a concrete yard edged with
        // tyre stacks beside blue-and-white tin sheds; the bumper cars under a green-framed tin roof inside red and yellow
        // rails; a grey climbing wall on a steel frame; a lane of shops on red-and-cream chequered tiles (shawls, Kashmiri
        // coats, caps), red paper lanterns, prayer flags overhead, the yellow VIEW POINT board, stone walls.

        // where they stand (game metres; the hill top between the ropeway, the Gol Market and Tower 360)
        internal static Vector2 SvKarts = new Vector2(600f, 585f), SvBumper = new Vector2(625f, 572f), SvArch = new Vector2(610f, 560f),
                                SvLaneA = new Vector2(599f, 514f), SvLaneB = new Vector2(629f, 528f), SvClimb = new Vector2(575f, 575f),
                                SvFood = new Vector2(690f, 562f), SvZipA = new Vector2(700f, 598f), SvZipB = new Vector2(650f, 612f);

        internal static IEnumerable<(Vector2 c, float r)> SnowViewParkSpots()
        {
            yield return (SvKarts, 17f);
            yield return (SvBumper, 9f);
            yield return (SvClimb, 6f);
            yield return (SvFood, 11f);
            yield return (SvZipA, 5f);
            yield return (SvZipB, 4f);
            Vector2 d = SvLaneB - SvLaneA;
            for (float t = 0f; t <= 1.001f; t += 0.2f) yield return (SvLaneA + d * t, 6f);
        }

        static void BuildSnowViewPark(WorldCtx ctx, Transform parent, System.Random rng)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var words = new GameObject("Snow View park boards").transform;
            words.SetParent(parent, false);
            Site Part(string name, Vector2 at, Vector2 face, float radius, System.Action<MeshBuilder, Site> build)
            {
                var site = MakeSite(ctx, at, face, radius, 0f);
                var mb = new MeshBuilder { M = Matrix4x4.TRS(site.origin, site.rot, Vector3.one) };
                build(mb, site);
                Emit(ctx, name, site, mb, parent);
                return site;
            }
            void Board(Site s, string text, Vector3 local, float h, Color c) =>
                WorldBuilder.Label(words, font, text, s.origin + s.rot * local, s.rot * Vector3.forward, h, c);

            foreach (var sp in SnowViewParkSpots()) ctx.keepClear.Add(sp);   // no tree or house on them
            var karts = Part("Go-karting (Snow View)", SvKarts, SvArch, 15f, (mb, s) => GoKartTrack(mb, s, rng));
            Board(karts, "GO KARTING", new Vector3(0f, 3.4f, 9.6f), 0.6f, new Color(1f, 0.85f, 0.1f));
            var bump = Part("Bumping cars (Snow View)", SvBumper, SvArch, 8f, (mb, s) => BumperCars(mb, s, rng));
            Board(bump, "BUMPING CAR", new Vector3(0f, 4.3f, 5.6f), 0.45f, Color.white);
            var climb = Part("Climbing wall (Snow View)", SvClimb, SvArch, 5f, (mb, s) => ClimbingWall(mb, s, rng));
            var arch = Part("Adventure Park gate (Snow View)", SvArch, SvLaneA, 4f, (mb, s) => MosaicArch(mb, s));
            Board(arch, "ADVENTURE PARK", new Vector3(0f, 4.9f, 0.5f), 0.5f, Color.white);
            Board(arch, "MOUNTAIN MAGIC", new Vector3(0f, 4.3f, 0.5f), 0.36f, new Color(1f, 0.85f, 0.2f));
            ChequeredLane(ctx, parent, words, font, rng);
            var food = Part("Snow View food court", SvFood, SvGolMarket, 10f, (mb, s) => FoodCourt(mb, s, rng));
            Board(food, "CHAI - MAGGI - MOMOS", new Vector3(0f, 3.1f, 5.2f), 0.4f, new Color(1f, 0.9f, 0.3f));
            ZipLine(ctx, parent);
            ctx.Zone("Adventure Park (Snow View)", new Vector3(SvKarts.x, ctx.H(SvKarts), SvKarts.y), 45f);
        }

        static void GoKartTrack(MeshBuilder mb, Site s, System.Random rng)
        {
            Vector2 conc = M(Atlas.MatConcrete), tyre = S(Pal.IronBlack), white = S(Pal.ShirtWhite), blue = S(Pal.BoatBlue), red = S(Pal.TempleRed),
                    yellow = S(Pal.BoatYellow), green = S(Pal.BoatGreen), orange = M(Atlas.MatTempleOrange), steel = S(Pal.StoneGrey);
            const float L = 30f, Wd = 17f;
            Plinth(mb, new Vector3(L, 0f, Wd), s.depth, conc);
            mb.Box(new Vector3(0f, 0.01f, 0f), new Vector3(L - 0.4f, 0.02f, Wd - 0.4f), S(Pal.Concrete));
            // tyre stacks: the outer edge and an island down the middle, two high, black with a white one every few
            void Stack(Vector3 p, int n)
            {
                for (int k = 0; k < n; k++) mb.Cylinder(p + Vector3.up * (k * 0.26f), 0.34f, 0.34f, 0.25f, 10, (((int)(p.x * 3 + p.z * 5) + k) % 5 == 0) ? white : tyre, true);
            }
            for (float x = -L * 0.5f + 0.6f; x <= L * 0.5f - 0.6f; x += 0.72f) { Stack(new Vector3(x, 0f, Wd * 0.5f - 0.5f), 2); Stack(new Vector3(x, 0f, -Wd * 0.5f + 0.5f), 2); }
            for (float z = -Wd * 0.5f + 1.2f; z <= Wd * 0.5f - 1.2f; z += 0.72f) { Stack(new Vector3(L * 0.5f - 0.5f, 0f, z), 2); Stack(new Vector3(-L * 0.5f + 0.5f, 0f, z), 2); }
            for (float x = -L * 0.5f + 7f; x <= L * 0.5f - 7f; x += 0.72f) Stack(new Vector3(x, 0f, 0f), 2);
            foreach (float x in new[] { -L * 0.5f + 7f, L * 0.5f - 7f }) for (int k = 0; k < 4; k++) Stack(new Vector3(x, 0f, (k - 1.5f) * 0.8f), 3);
            // the start line, chequered
            for (int k = 0; k < 10; k++) mb.Box(new Vector3(-2f + (k % 2) * 0.5f, 0.025f, Wd * 0.5f - 1.3f - k * 0.65f), new Vector3(0.5f, 0.01f, 0.65f), (k % 2 == 0) ? white : tyre);
            // the karts at the start: low frames, a seat, four fat wheels, a colour each
            int[] cols = { Pal.BoatGreen, Pal.BoatBlue, Pal.JacketRed, Pal.BoatYellow, Pal.BoatGreen };
            for (int k = 0; k < 5; k++)
            {
                Vector3 p = new Vector3(-5f - (k % 2) * 2.2f, 0f, Wd * 0.5f - 2.2f - (k / 2) * 1.8f);
                var r = Quaternion.Euler(0f, 90f, 0f);
                mb.Box(p + Vector3.up * 0.18f, new Vector3(0.95f, 0.12f, 1.7f), r, S(cols[k]));
                mb.Box(p + r * new Vector3(0f, 0.4f, -0.3f), new Vector3(0.5f, 0.45f, 0.12f), r, tyre);           // the seat back
                mb.Box(p + r * new Vector3(0f, 0.3f, 0.6f), new Vector3(0.7f, 0.22f, 0.4f), r, S(cols[k]));      // the nose
                foreach (float wx in new[] { -0.52f, 0.52f }) foreach (float wz in new[] { -0.6f, 0.6f })
                        mb.Cylinder(p + r * new Vector3(wx, 0.14f, wz), 0.15f, 0.15f, 0.16f, 8, tyre, true, true);
                mb.Limb(p + r * new Vector3(0f, 0.3f, 0.25f), p + r * new Vector3(0f, 0.55f, 0.05f), 0.02f, 0.02f, 4, steel, false);   // the wheel
            }
            // the sheds along the far side: blue and white tin, open fronts
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = new Vector3(-9f + k * 7f, 0f, -Wd * 0.5f - 3f);
                mb.Box(c + Vector3.up * 1.4f, new Vector3(6.2f, 2.8f, 0.15f), k % 2 == 0 ? blue : white);
                mb.Box(c + new Vector3(-3.05f, 1.4f, 1.5f), new Vector3(0.12f, 2.8f, 3f), white);
                mb.Box(c + new Vector3(0f, 2.95f, 1.5f), new Vector3(6.6f, 0.12f, 3.6f), Quaternion.Euler(-8f, 0f, 0f), red);
            }
            // flood lights on two poles
            foreach (float x in new[] { -L * 0.5f + 1f, L * 0.5f - 1f })
            {
                Vector3 p = new Vector3(x, 0f, Wd * 0.5f + 0.6f);
                mb.Cylinder(p, 0.08f, 0.06f, 7f, 6, steel, true);
                mb.Box(p + Vector3.up * 7f + Vector3.back * 0.3f, new Vector3(0.8f, 0.4f, 0.3f), Quaternion.Euler(30f, 0f, 0f), tyre);
                WorldBuilder.Bulb(mb.M.MultiplyPoint3x4(p + Vector3.up * 6.9f + Vector3.back * 0.5f), WorldBuilder.LedWhite, 0.5f, 2f);
                WorldBuilder.Accent(mb.M.MultiplyPoint3x4(p + Vector3.up * 6f + Vector3.back * 4f), new Color(0.92f, 0.96f, 1f), 16f, 22f);
            }
            // the board on its frame
            mb.Box(new Vector3(0f, 3.4f, Wd * 0.5f + 0.1f), new Vector3(5.6f, 1.1f, 0.1f), S(Pal.IronBlack));
            mb.Box(new Vector3(-2.6f, 1.6f, Wd * 0.5f + 0.1f), new Vector3(0.12f, 3.2f, 0.12f), steel);
            mb.Box(new Vector3(2.6f, 1.6f, Wd * 0.5f + 0.1f), new Vector3(0.12f, 3.2f, 0.12f), steel);
        }

        static void BumperCars(MeshBuilder mb, Site s, System.Random rng)
        {
            Vector2 steel = S(Pal.StoneGrey), red = S(Pal.TempleRed), yellow = S(Pal.BoatYellow), green = S(Pal.BoatGreen), dark = S(Pal.IronBlack), tin = S(Pal.ShirtWhite);
            const float L = 13f, Wd = 9f, H = 4f;
            Plinth(mb, new Vector3(L + 1f, 0f, Wd + 1f), s.depth, M(Atlas.MatConcrete));
            mb.Box(new Vector3(0f, 0.08f, 0f), new Vector3(L, 0.16f, Wd), steel);                       // the metal floor
            // the fence: red rails on yellow posts, open at the front
            void Fence(Vector3 a, Vector3 b)
            {
                mb.Limb(a + Vector3.up * 0.9f, b + Vector3.up * 0.9f, 0.05f, 0.05f, 5, red, false);
                mb.Limb(a + Vector3.up * 0.5f, b + Vector3.up * 0.5f, 0.05f, 0.05f, 5, red, false);
                for (float t = 0f; t <= 1.001f; t += 0.2f) mb.Box(Vector3.Lerp(a, b, t) + Vector3.up * 0.5f, new Vector3(0.15f, 1f, 0.15f), yellow);
            }
            Fence(new Vector3(-L * 0.5f, 0f, -Wd * 0.5f), new Vector3(L * 0.5f, 0f, -Wd * 0.5f));
            Fence(new Vector3(-L * 0.5f, 0f, -Wd * 0.5f), new Vector3(-L * 0.5f, 0f, Wd * 0.5f));
            Fence(new Vector3(L * 0.5f, 0f, -Wd * 0.5f), new Vector3(L * 0.5f, 0f, Wd * 0.5f));
            Fence(new Vector3(-L * 0.5f, 0f, Wd * 0.5f), new Vector3(-1.2f, 0f, Wd * 0.5f));
            Fence(new Vector3(1.2f, 0f, Wd * 0.5f), new Vector3(L * 0.5f, 0f, Wd * 0.5f));
            // the green steel frame, the tin roof, the electric net under it
            foreach (float x in new[] { -L * 0.5f, 0f, L * 0.5f })
                foreach (float z in new[] { -Wd * 0.5f, Wd * 0.5f }) mb.Box(new Vector3(x, H * 0.5f, z), new Vector3(0.2f, H, 0.2f), green);
            mb.Box(new Vector3(0f, H + 0.1f, 0f), new Vector3(L + 0.8f, 0.12f, Wd + 0.8f), tin);
            mb.Box(new Vector3(0f, H - 0.05f, Wd * 0.5f + 0.1f), new Vector3(L + 0.8f, 0.3f, 0.08f), green);
            for (float x = -L * 0.5f; x <= L * 0.5f; x += 1f) mb.Box(new Vector3(x, H - 0.4f, 0f), new Vector3(0.02f, 0.02f, Wd), dark);
            for (float z = -Wd * 0.5f; z <= Wd * 0.5f; z += 1f) mb.Box(new Vector3(0f, H - 0.4f, z), new Vector3(L, 0.02f, 0.02f), dark);
            // the cars, each with a rubber ring and a pole up to the net
            int[] cols = { Pal.JacketRed, Pal.BoatBlue, Pal.BoatYellow, Pal.BoatGreen, Pal.SareePink, Pal.JacketBlue };
            for (int k = 0; k < 6; k++)
            {
                Vector3 p = new Vector3(-4.5f + (k % 3) * 4.2f + (float)rng.NextDouble(), 0.16f, -2f + (k / 3) * 3.6f);
                var r = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                mb.Cylinder(p, 0.95f, 0.95f, 0.3f, 14, dark, true);                          // the rubber ring
                mb.Box(p + Vector3.up * 0.45f, new Vector3(1.2f, 0.45f, 1.5f), r, S(cols[k]));
                mb.Box(p + r * new Vector3(0f, 0.75f, -0.45f), new Vector3(1.1f, 0.5f, 0.2f), r, S(cols[k]));
                mb.Limb(p + r * new Vector3(0f, 0.9f, -0.55f), p + r * new Vector3(0f, H - 0.45f - 0.16f, -0.55f), 0.025f, 0.025f, 4, dark, false);
            }
            // bulbs round the roof's edge
            for (float x = -L * 0.5f; x <= L * 0.5f; x += 0.5f)
                foreach (float z in new[] { -Wd * 0.5f - 0.35f, Wd * 0.5f + 0.35f })
                    WorldBuilder.Bulb(mb.M.MultiplyPoint3x4(new Vector3(x, H - 0.1f, z)), ((int)(x * 2f) % 3) switch { 0 => WorldBuilder.LedRed, 1 => WorldBuilder.LedGold, _ => WorldBuilder.LedBlue }, 0.06f, 3f);
            WorldBuilder.Accent(mb.M.MultiplyPoint3x4(new Vector3(0f, H - 0.6f, 0f)), new Color(0.95f, 0.9f, 1f), 9f, 12f);
            mb.Box(new Vector3(0f, 4.3f, Wd * 0.5f + 0.4f), new Vector3(4.8f, 0.8f, 0.08f), S(Pal.JacketRed));
        }

        /// <summary>The picnic yard on the open grass (owner: "khaali khaali saare areas bhrne hain"; the videos' chai and
        /// Maggi carts, yellow, with plastic stools, benches under umbrellas, coin binoculars at the edge).</summary>
        static void FoodCourt(MeshBuilder mb, Site s, System.Random rng)
        {
            Vector2 stone = M(Atlas.MatStone), yellow = S(Pal.BoatYellow), red = S(Pal.TempleRed), dark = S(Pal.IronBlack), steel = S(Pal.StoneGrey), wood = S(Pal.WoodDark);
            Plinth(mb, new Vector3(20f, 0f, 14f), s.depth, stone);
            for (int k = 0; k < 6; k++)   // steps down all round the front where the yard stands above the slope
                mb.Box(new Vector3(0f, -0.1f - k * 0.17f, -7.15f - k * 0.3f), new Vector3(20f, 0.2f + k * 0.34f, 0.3f), stone);
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = new Vector3(-6f + k * 6f, 0f, 4.6f);
                mb.Box(c + Vector3.up * 0.75f, new Vector3(2.2f, 0.9f, 1.1f), k == 1 ? red : yellow);
                mb.Box(c + Vector3.up * 1.22f, new Vector3(2.3f, 0.05f, 1.2f), steel);
                foreach (float x in new[] { -1f, 1f }) mb.Cylinder(c + new Vector3(x, 0f, 0.5f), 0.04f, 0.04f, 2.3f, 5, steel, false);
                mb.Box(c + new Vector3(0f, 2.35f, 0.1f), new Vector3(2.6f, 0.08f, 1.6f), Quaternion.Euler(8f, 0f, 0f), k == 1 ? yellow : red);
                mb.Cylinder(c + new Vector3(-0.5f, 1.25f, 0f), 0.2f, 0.2f, 0.35f, 10, steel, true);
                foreach (float w in new[] { -0.9f, 0.9f }) mb.Cylinder(c + new Vector3(w, 0.3f, -0.56f), 0.3f, 0.3f, 0.08f, 10, dark, true, true);
                WorldBuilder.Bulb(mb.M.MultiplyPoint3x4(c + new Vector3(0f, 2.2f, -0.3f)), WorldBuilder.LedWarm, 0.2f, 2.2f);
                WorldBuilder.Accent(mb.M.MultiplyPoint3x4(c + new Vector3(0f, 2.1f, -1f)), new Color(1f, 0.8f, 0.5f), 5f, 6f);
            }
            int[] umb = { Pal.JacketRed, Pal.BoatBlue, Pal.BoatGreen, Pal.BoatYellow };
            for (int k = 0; k < 4; k++)
            {
                Vector3 t = new Vector3(-6f + k * 4f, 0f, -1.5f + (k % 2) * 1.2f);
                mb.Cylinder(t, 0.05f, 0.05f, 0.72f, 6, steel, false);
                mb.Cylinder(t + Vector3.up * 0.72f, 0.55f, 0.55f, 0.04f, 12, S(Pal.ShirtWhite), true, true);
                mb.Cylinder(t, 0.03f, 0.03f, 2.3f, 5, steel, false);
                mb.Cylinder(t + Vector3.up * 2.1f, 1.3f, 0.05f, 0.5f, 8, S(umb[k]), true, true);
                for (int j = 0; j < 4; j++)
                {
                    float a = j * Mathf.PI * 0.5f + 0.4f;
                    Vector3 st = t + new Vector3(Mathf.Cos(a) * 0.95f, 0f, Mathf.Sin(a) * 0.95f);
                    mb.Cylinder(st, 0.17f, 0.15f, 0.42f, 8, S(umb[(k + j) % 4]), true);
                }
            }
            foreach (float x in new[] { -7.5f, 7.5f })
            {
                mb.Box(new Vector3(x, 0.45f, -5.6f), new Vector3(2.2f, 0.08f, 0.45f), wood);
                foreach (float bx in new[] { -0.9f, 0.9f }) mb.Box(new Vector3(x + bx, 0.22f, -5.6f), new Vector3(0.08f, 0.44f, 0.4f), dark);
            }
            for (int k = 0; k < 2; k++)
            {
                Vector3 b = new Vector3(-2f + k * 4f, 0f, -6.3f);
                mb.Cylinder(b, 0.08f, 0.06f, 1.2f, 6, steel, true);
                mb.Box(b + new Vector3(0f, 1.3f, 0f), new Vector3(0.45f, 0.22f, 0.35f), S(Pal.JacketBlue));
                foreach (float x in new[] { -0.1f, 0.1f }) mb.Cylinder(b + new Vector3(x, 1.3f, -0.2f), 0.06f, 0.06f, 0.15f, 8, dark, true, true);
            }
            mb.Cylinder(new Vector3(9f, 0f, 2f), 0.3f, 0.3f, 0.9f, 10, S(Pal.BoatGreen), true);
            WorldBuilder.SagString(mb.M.MultiplyPoint3x4(new Vector3(-9.5f, 3f, 5.5f)), mb.M.MultiplyPoint3x4(new Vector3(9.5f, 3f, -5.5f)), 0.7f, WorldBuilder.LedWarm, 0.32f, 0.055f, 2.6f);
            WorldBuilder.SagString(mb.M.MultiplyPoint3x4(new Vector3(9.5f, 3f, 5.5f)), mb.M.MultiplyPoint3x4(new Vector3(-9.5f, 3f, -5.5f)), 0.7f, WorldBuilder.LedGold, 0.32f, 0.055f, 2.6f);
            foreach (var c in new[] { new Vector3(-9.5f, 0f, 5.5f), new Vector3(9.5f, 0f, -5.5f), new Vector3(9.5f, 0f, 5.5f), new Vector3(-9.5f, 0f, -5.5f) })
                mb.Cylinder(c, 0.06f, 0.05f, 3.1f, 6, dark, true);
        }

        /// <summary>The zip line (the board: "Zip Line"): a yellow steel tower with a deck and a ladder, a cable down over
        /// the trees to a lower post, a harness seat hanging on it.</summary>
        static void ZipLine(WorldCtx ctx, Transform parent)
        {
            var mb = new MeshBuilder();
            Vector2 yellow = S(Pal.BoatYellow), dark = S(Pal.IronBlack), wood = S(Pal.WoodDark), red = S(Pal.TempleRed);
            Vector3 a = new Vector3(SvZipA.x, ctx.H(SvZipA), SvZipA.y), b = new Vector3(SvZipB.x, ctx.H(SvZipB), SvZipB.y);
            const float Ta = 9f, Tb = 3.2f;
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                    mb.Limb(a + new Vector3(x * 1.3f, -0.3f, z * 1.3f), a + new Vector3(x * 1f, Ta, z * 1f), 0.09f, 0.08f, 5, yellow, false);
            for (float y = 2f; y < Ta; y += 2.2f) mb.Box(a + Vector3.up * y, new Vector3(2.4f, 0.08f, 2.4f), dark);
            mb.Box(a + Vector3.up * Ta, new Vector3(2.8f, 0.2f, 2.8f), wood);
            mb.Box(a + Vector3.up * (Ta + 0.55f), new Vector3(2.8f, 0.06f, 0.06f), red);
            for (int k = 0; k < 14; k++) mb.Box(a + new Vector3(1.2f, 0.4f + k * 0.62f, -1.4f), new Vector3(0.5f, 0.05f, 0.05f), dark);
            foreach (float x in new[] { -0.3f, 0.3f }) mb.Limb(b + new Vector3(x, 0f, 0f), b + new Vector3(x, Tb, 0f), 0.1f, 0.1f, 6, yellow, false);
            Vector3 ta = a + Vector3.up * (Ta + 1.6f), tb = b + Vector3.up * Tb;
            mb.Limb(a + Vector3.up * Ta, ta, 0.06f, 0.06f, 5, yellow, false);
            Vector3 last = ta;
            for (int k = 1; k <= 20; k++)
            {
                float t = k / 20f;
                Vector3 p = Vector3.Lerp(ta, tb, t) + Vector3.down * 1.4f * 4f * t * (1f - t);
                mb.Limb(last, p, 0.02f, 0.02f, 3, dark, false);
                last = p;
            }
            Vector3 seat = Vector3.Lerp(ta, tb, 0.15f) + Vector3.down * (1.4f * 4f * 0.15f * 0.85f);
            mb.Limb(seat, seat + Vector3.down * 1.2f, 0.015f, 0.015f, 3, dark, false);
            mb.Box(seat + Vector3.down * 1.25f, new Vector3(0.45f, 0.06f, 0.35f), red);
            ctx.MeshObject("Zip line (Snow View)", parent, mb, ctx.mats.atlas, null, true, true);
            WorldBuilder.Led(a + new Vector3(-1.4f, Ta + 0.12f, -1.4f), a + new Vector3(1.4f, Ta + 0.12f, -1.4f), WorldBuilder.LedWarm, 0.12f, 0.05f, 2.4f);
        }

        static void ClimbingWall(MeshBuilder mb, Site s, System.Random rng)
        {
            Vector2 grey = S(Pal.StoneGrey), steel = S(Pal.IronBlack), mat = S(Pal.BoatBlue);
            Plinth(mb, new Vector3(7f, 0f, 6f), s.depth, M(Atlas.MatConcrete));
            mb.Box(new Vector3(0f, 0.15f, 1.2f), new Vector3(4.5f, 0.3f, 2.4f), mat);                    // the crash mat
            // the wall: grey panels leaning back a little on a steel frame
            var lean = Quaternion.Euler(-6f, 0f, 0f);
            mb.Box(new Vector3(0f, 4f, -0.4f), new Vector3(4.2f, 8f, 0.25f), lean, grey);
            foreach (float x in new[] { -2.2f, 2.2f }) mb.Box(new Vector3(x, 4f, -1.4f), new Vector3(0.18f, 8f, 0.18f), steel);
            for (float y = 1.5f; y < 8f; y += 2f) mb.Box(new Vector3(0f, y, -1.4f), new Vector3(4.6f, 0.12f, 0.12f), steel);
            int[] holds = { Pal.JacketRed, Pal.BoatYellow, Pal.BoatGreen, Pal.BoatBlue, Pal.SareePink };
            for (int k = 0; k < 46; k++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * 3.8f, y = 0.4f + (float)rng.NextDouble() * 7.4f;
                Vector3 p = new Vector3(x, y, -0.25f + y * 0.105f);
                mb.Sphere(p, new Vector3(0.12f, 0.1f, 0.08f), 5, 3, S(holds[k % holds.Length]));
            }
        }

        static void MosaicArch(MeshBuilder mb, Site s)
        {
            int[] mosaic = { Pal.BoatBlue, Pal.JacketBlue, Pal.ShirtWhite, Pal.BoatBlue, Pal.StoneGrey };
            // two columns and a beam, covered in a pattern of little blue and white squares (bottle caps)
            foreach (float x in new[] { -2.4f, 2.4f })
            {
                mb.Box(new Vector3(x, 2.2f, 0f), new Vector3(0.9f, 4.4f, 0.9f), S(Pal.BoatBlue));
                for (int j = 0; j < 14; j++)
                    for (int i = 0; i < 3; i++)
                        foreach (float zz in new[] { 0.46f, -0.46f })
                            mb.Box(new Vector3(x - 0.3f + i * 0.3f, 0.3f + j * 0.3f, zz), new Vector3(0.24f, 0.24f, 0.02f), S(mosaic[(i + j * 2) % mosaic.Length]));
            }
            mb.Box(new Vector3(0f, 4.6f, 0f), new Vector3(6.2f, 1.4f, 0.9f), S(Pal.JacketBlue));
            mb.Box(new Vector3(0f, 4.6f, 0.46f), new Vector3(5.4f, 1.2f, 0.02f), S(Pal.IronBlack));      // the board's ground
            // its outline lit
            Vector3 W(Vector3 l) => mb.M.MultiplyPoint3x4(l);
            Color c = WorldBuilder.LedBlue;
            WorldBuilder.Led(W(new Vector3(-3.1f, 5.32f, 0.5f)), W(new Vector3(3.1f, 5.32f, 0.5f)), c, 0.1f, 0.05f, 2.6f);
            WorldBuilder.Led(W(new Vector3(-3.1f, 3.88f, 0.5f)), W(new Vector3(3.1f, 3.88f, 0.5f)), c, 0.1f, 0.05f, 2.6f);
            foreach (float x in new[] { -2.88f, 2.88f }) WorldBuilder.Led(W(new Vector3(x, 0.2f, 0.5f)), W(new Vector3(x, 3.9f, 0.5f)), WorldBuilder.LedWarm, 0.12f, 0.045f, 2.2f);
            WorldBuilder.Accent(W(new Vector3(0f, 3.6f, 2f)), new Color(0.5f, 0.65f, 1f), 7f, 8f);
        }

        /// <summary>The market lane on red-and-cream chequered tiles from the ropeway toward the Gol Market: shops on both
        /// sides with shawls and coats hung out, red paper lanterns, prayer flags across, the VIEW POINT board, warm strings.</summary>
        static void ChequeredLane(WorldCtx ctx, Transform parent, Transform words, Font font, System.Random rng)
        {
            Vector2 a = SvLaneA, b = SvLaneB, dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x);
            float len = Vector2.Distance(a, b);
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            Vector2 red = S(Pal.TempleRed), cream = S(Pal.ShirtWhite), gold = S(Pal.Gold), dark = S(Pal.IronBlack);
            const float tile = 0.6f, half = 2.1f;
            int[] cloth = { Pal.JacketRed, Pal.SareePink, Pal.JacketBlue, Pal.BoatYellow, Pal.JacketGreen, Pal.WoodDark };
            for (float t = 0f; t < len; t += tile)
                for (float u = -half; u < half; u += tile)
                {
                    Vector2 q = a + dir * (t + tile * 0.5f) + side * (u + tile * 0.5f);
                    if (ctx.InLake(q)) continue;
                    float y = ctx.H(q) + 0.04f;
                    int k = Mathf.FloorToInt(t / tile) + Mathf.FloorToInt((u + half) / tile);
                    var rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
                    mb.Box(new Vector3(q.x, y, q.y), new Vector3(tile, 0.08f, tile), rot, (k % 2 == 0) ? red : cream);
                }
            // shops on both sides every 4.5 m, their goods hung out; lanterns and flags overhead
            int shops = 0;
            for (float t = 2f; t < len - 2f; t += 4.5f)
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector2 q = a + dir * t + side * sgn * (half + 2.2f);
                    if (ctx.InLake(q) || WorldBuilder.NearAnyRoad(ctx, q)) continue;
                    float y = ctx.H(a + dir * t);
                    var face = -side * sgn;
                    float yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
                    mb.M = Matrix4x4.TRS(new Vector3(q.x, y, q.y), Quaternion.identity, Vector3.one);
                    HillShop(mb, Vector3.zero, yaw, 4f, rng);
                    var r = Quaternion.Euler(0f, yaw, 0f);
                    for (int k = 0; k < 5; k++)   // coats and shawls on a rail at the front
                        Cloth(mb).Box(r * new Vector3(-1.4f + k * 0.7f, 1.4f, 2.1f), new Vector3(0.55f, 1.1f, 0.06f), r, S(cloth[(shops + k) % cloth.Length]));
                    mb.M = Matrix4x4.identity;
                    shops++;
                }
            for (float t = 3f; t < len - 1f; t += 6f)
            {
                Vector2 q = a + dir * t;
                float y = ctx.H(q);
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector3 lan = new Vector3(q.x + side.x * sgn * (half - 0.3f), y + 3.1f, q.y + side.y * sgn * (half - 0.3f));
                    mb.Sphere(lan, new Vector3(0.32f, 0.38f, 0.32f), 8, 5, red);
                    mb.Cylinder(lan + Vector3.up * 0.3f, 0.14f, 0.14f, 0.08f, 8, gold, true);
                    mb.Cylinder(lan - Vector3.up * 0.42f, 0.14f, 0.14f, 0.08f, 8, gold, true);
                    WorldBuilder.Bulb(lan, new Color(1f, 0.35f, 0.2f), 0.45f, 1.4f);
                }
                Vector3 f0 = new Vector3(q.x + side.x * (half + 0.4f), y + 3.6f, q.y + side.y * (half + 0.4f));
                Vector3 f1 = new Vector3(q.x - side.x * (half + 0.4f), y + 3.6f, q.y - side.y * (half + 0.4f));
                for (int k = 0; k < 9; k++)
                {
                    Vector3 p = Vector3.Lerp(f0, f1, (k + 0.5f) / 9f) + Vector3.down * 0.35f * Mathf.Sin((k + 0.5f) / 9f * Mathf.PI);
                    Cloth(mb).Box(p + Vector3.down * 0.18f, new Vector3(0.3f, 0.36f, 0.01f), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)), S(new[] { Pal.BoatBlue, Pal.ShirtWhite, Pal.JacketRed, Pal.JacketGreen, Pal.BoatYellow }[k % 5]));
                }
                WorldBuilder.SagString(f0 + Vector3.down * 0.2f, f1 + Vector3.down * 0.2f, 0.5f, WorldBuilder.LedWarm, 0.3f, 0.055f, 2.6f);
                if ((int)(t / 6f) % 2 == 0) WorldBuilder.Accent(new Vector3(q.x, y + 2.6f, q.y), new Color(1f, 0.82f, 0.55f), 7f, 8f);
            }
            // the VIEW POINT board at the lane's end
            Vector2 vb = b + dir * 1.5f;
            float vy = ctx.H(vb);
            var vrot = Quaternion.LookRotation(new Vector3(-dir.x, 0f, -dir.y));
            mb.Box(new Vector3(vb.x, vy + 3.2f, vb.y), new Vector3(3.6f, 0.9f, 0.12f), vrot, S(Pal.BoatYellow));
            foreach (float sgn in new[] { -1f, 1f }) mb.Box(new Vector3(vb.x, vy + 1.4f, vb.y) + vrot * new Vector3(sgn * 1.6f, 0f, 0f), new Vector3(0.12f, 2.8f, 0.12f), dark);
            WorldBuilder.Label(words, font, "VIEW POINT", new Vector3(vb.x, vy + 3.2f, vb.y) + vrot * new Vector3(0f, 0f, 0.08f), vrot * Vector3.forward, 0.55f, new Color(0.75f, 0.1f, 0.05f));
            ctx.MeshObject("Snow View market lane", parent, mb, ctx.mats.atlas, col, true, true);
            Debug.Log($"[Nainital] Snow View market lane: {len:F0} m, {shops} shops");
        }
    }
}

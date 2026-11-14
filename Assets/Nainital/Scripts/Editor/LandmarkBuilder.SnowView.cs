using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Snow View hilltop (user, 2026-11-14: "snow view point ko bhi theek karo, wahan bahut games aur bahut kuch hai, google
    /// maps se dekh lo"). Laid out from Google Maps satellite (D:\Unity\ref\maps\snowview_sat.jpg) and OpenStreetMap, dressed
    /// from the ropeway vlog (D:\Unity\ref\yt10\09_*): the round "Gol Market" ringed by shops with a kiosk in the middle; the
    /// Panoramic View Point, a round deck with a railing, coin binoculars and prayer flags facing the snow range; the
    /// ropeway's upper station; the games yard (trampoline, merry-go-round, swings, balloon shooting, a small Ferris wheel);
    /// the Dinosaur Park behind its blue mosaic arch with the beasts under green-framed tin sheds. Positions are game metres.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        static readonly Vector2 SvGolMarket = new Vector2(649f, 528f), SvDeck = new Vector2(668f, 500f), SvRopeway = new Vector2(586f, 507f),
                                SvGames = new Vector2(618f, 548f), SvDinoPark = new Vector2(585f, 556f);
        const float SvSnowBearing = 25f;   // the snow range: Nanda Devi and Trishul to the north-north-east

        /// <summary>The hilltop's parts and their radii, for the planner to keep houses off them.</summary>
        internal static IEnumerable<(Vector2 c, float r)> SnowViewSpots()
        {
            yield return (SvGolMarket, 17f);
            yield return (SvDeck, 10f);
            yield return (SvRopeway, 9f);
            yield return (SvGames, 15f);
            yield return (SvDinoPark, 15f);
        }

        static void BuildSnowView(WorldCtx ctx, Transform parent, System.Random rng)
        {
            Vector2 north = SvDeck + new Vector2(Mathf.Sin(SvSnowBearing * Mathf.Deg2Rad), Mathf.Cos(SvSnowBearing * Mathf.Deg2Rad)) * 200f;
            Site Part(string name, Vector2 at, Vector2 face, float radius, System.Action<MeshBuilder, Site> build)
            {
                var site = MakeSite(ctx, at, face, radius, 0f);
                var mb = new MeshBuilder { M = Matrix4x4.TRS(site.origin, site.rot, Vector3.one) };
                build(mb, site);
                Emit(ctx, name, site, mb, parent);
                return site;
            }

            var gol = Part("Snow View Gol Market", SvGolMarket, SvDeck, 15f, (mb, s) => GolMarket(mb, s.depth, rng));
            var deck = Part("Snow View", SvDeck, north, 8f, (mb, s) => PanoramaDeck(mb, s.depth, rng));
            var rope = Part("Snow View ropeway station", SvRopeway, SvGolMarket, 7f, (mb, s) => RopewayTopStation(mb, s.depth));
            Part("Snow View games", SvGames, SvGolMarket, 13f, (mb, s) => GamesYard(mb, s.depth, rng));
            Part("Dinosaur Park (Snow View)", SvDinoPark, SvGames, 12f, (mb, s) => DinoPark(mb, s.depth, rng));

            ctx.landmarkSites["Snow View"] = deck.origin;
            ctx.landmarkSites["Snow View ropeway"] = rope.origin + Vector3.up * 1.5f;
            Sign(ctx, "Snow View", deck, new Vector3(-4f, 0f, -6f), parent);
            ctx.Zone("Snow View", gol.origin, 110f);
            var cam = new GameObject("ViewShot Snow View");   // PreviewCapture: the deck, and the snow range from it
            cam.transform.SetParent(parent, false);
            cam.transform.SetPositionAndRotation(deck.origin + Vector3.up * 1.7f, deck.rot);
        }

        /// <summary>Prayer flags on a line between two points, sagging a little.</summary>
        static void PrayerFlags(MeshBuilder mb, Vector3 a, Vector3 b)
        {
            int[] cols = { Pal.JacketBlue, Pal.ShirtWhite, Pal.JacketRed, Pal.BoatGreen, Pal.BoatYellow };
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.45f));
            Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * 0.01f;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (len * 0.04f * 4f * t * (1f - t));
                Vector3 r = (b - a).normalized * 0.16f;
                mb.QuadDouble(p - r + Vector3.down * 0.34f, p + r + Vector3.down * 0.34f, p + r, p - r, new Rect(S(cols[i % cols.Length]), Vector2.zero), side);
            }
        }

        static void GolMarket(MeshBuilder mb, float depth, System.Random rng)
        {
            Vector2 paver = M(Atlas.MatConcrete), red = S(Pal.TempleRed);
            mb.Cylinder(new Vector3(0f, -depth, 0f), 16f, 16f, depth, 24, paver, true);
            mb.Cylinder(new Vector3(0f, 0.005f, 0f), 6.5f, 6.5f, 0.01f, 24, red, true);   // the terracotta ring round the middle
            mb.Cylinder(new Vector3(0f, 0.01f, 0f), 5.7f, 5.7f, 0.01f, 24, paver, true);
            // the middle: a round raised bed with a small kiosk and benches round it
            mb.Cylinder(Vector3.zero, 3.2f, 3.2f, 0.5f, 16, M(Atlas.MatStone), true);
            mb.Cylinder(new Vector3(0f, 0.5f, 0f), 2.9f, 2.9f, 0.05f, 16, S(Pal.Grass), true);
            mb.Cylinder(new Vector3(0f, 0.5f, 0f), 0.9f, 0.9f, 2.4f, 8, S(Pal.ShirtWhite), true);
            mb.Cylinder(new Vector3(0f, 2.9f, 0f), 1.5f, 0.1f, 0.9f, 8, S(Pal.BoatGreen), true);
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                var q = Quaternion.Euler(0f, a, 0f);
                mb.Box(q * new Vector3(0f, 0.42f, 4.1f), new Vector3(1.6f, 0.08f, 0.45f), q, S(Pal.WoodLight));
                mb.Box(q * new Vector3(0f, 0.2f, 4.1f), new Vector3(0.12f, 0.4f, 0.4f), q, S(Pal.IronBlack));
            }
            // the ring of shops, open to the middle, with a gap to the deck (+z) and one to the ropeway (west)
            for (int k = 0; k < 18; k++)
            {
                float a = k * 20f;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 22f || Mathf.Abs(Mathf.DeltaAngle(a, 250f)) < 14f) continue;
                Vector3 at = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 12.6f);
                HillShop(mb, at, a + 180f, 3.9f, rng);
            }
            // light poles with prayer flags strung across
            for (int k = 0; k < 4; k++)
            {
                Vector3 p0 = Quaternion.Euler(0f, 45f + k * 90f, 0f) * new Vector3(0f, 0f, 9.5f);
                mb.Cylinder(p0, 0.08f, 0.06f, 5.5f, 6, S(Pal.IronGreen), true);
                PrayerFlags(mb, p0 + Vector3.up * 5.3f, new Vector3(0f, 3.6f, 0f));
            }
        }

        static void PanoramaDeck(MeshBuilder mb, float depth, System.Random rng)
        {
            Vector2 conc = M(Atlas.MatConcrete), paleBlue = S(Pal.JacketBlue), white = S(Pal.TempleWhite);
            mb.Cylinder(new Vector3(0f, -depth, 0f), 8.5f, 8.5f, depth, 20, conc, true);
            // a low pale-blue wall round the front half, a black iron railing on it
            var rail = new List<Vector3>();
            for (int k = -6; k <= 6; k++)
            {
                float a = k * 15f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 8.2f);
                rail.Add(p + Vector3.up * 0.7f);
                if (k < 6)
                {
                    Vector3 p1 = Quaternion.Euler(0f, a + 15f, 0f) * new Vector3(0f, 0f, 8.2f);
                    mb.Box((p + p1) * 0.5f + Vector3.up * 0.35f, new Vector3(0.35f, 0.7f, Vector3.Distance(p, p1) + 0.05f), Quaternion.LookRotation(p1 - p), paleBlue);
                    mb.Box((p + p1) * 0.5f + Vector3.up * 0.73f, new Vector3(0.45f, 0.06f, Vector3.Distance(p, p1) + 0.05f), Quaternion.LookRotation(p1 - p), white);
                }
            }
            PropKit.Railing(mb, new MeshBuilder(), rail);
            foreach (float a in new[] { -40f, -15f, 10f, 35f })
                Telescope(mb, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 6.9f));
            // flag poles at the back with lines of prayer flags fanning out to the rail
            Vector3 pole = new Vector3(0f, 0f, -5.5f);
            mb.Cylinder(pole, 0.1f, 0.07f, 7f, 6, S(Pal.WoodDark), true);
            foreach (float a in new[] { -50f, -20f, 20f, 50f })
                PrayerFlags(mb, pole + Vector3.up * 6.8f, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 1.6f, 8f));
            Flag(mb, pole + Vector3.up * 6.6f, 2.2f, Pal.SareeOrange);
            // the green notice board ("kooda neeche daalkar apni bewakoofi ka parichay na dein")
            mb.Box(new Vector3(-6f, 1.4f, -1f), new Vector3(0.08f, 1.1f, 1.6f), S(Pal.BoatGreen));
            mb.Box(new Vector3(-6f, 0.45f, -1f), new Vector3(0.08f, 0.9f, 0.1f), S(Pal.IronBlack));
            for (int k = 0; k < 3; k++) mb.Box(new Vector3(-3f + k * 2.2f, 0.42f, -3.8f), new Vector3(1.6f, 0.08f, 0.45f), S(Pal.WoodLight));
        }

        static void RopewayTopStation(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(14f, 0f, 12f), depth, M(Atlas.MatConcrete));
            Vector2 wall = Atlas.WallSolid(1), red = S(Pal.TempleRed), steel = S(Pal.StoneGrey);
            mb.Box(new Vector3(0f, 2.5f, -2f), new Vector3(12f, 5f, 7f), wall);
            mb.Box(new Vector3(0f, 5.1f, -2f), new Vector3(12.6f, 0.2f, 7.6f), red);
            mb.Box(new Vector3(0f, 4.2f, 1.55f), new Vector3(7f, 0.9f, 0.08f), red);                     // the KMVN board
            mb.Box(new Vector3(0f, 1.3f, 1.53f), new Vector3(2.4f, 2.6f, 0.05f), S(Pal.IronBlack));      // the entrance
            // the open platform where the cabins come in, under a steel frame painted red
            foreach (float x in new[] { -5.5f, 5.5f })
                foreach (float z in new[] { 2.5f, 5.5f }) mb.Box(new Vector3(x, 3f, z), new Vector3(0.25f, 6f, 0.25f), red);
            mb.Box(new Vector3(0f, 6.05f, 4f), new Vector3(11.5f, 0.15f, 3.6f), steel);
            mb.Box(new Vector3(0f, 0.5f, 4f), new Vector3(11f, 1f, 3f), M(Atlas.MatConcrete));
        }

        static void GamesYard(MeshBuilder mb, float depth, System.Random rng)
        {
            Plinth(mb, new Vector3(26f, 0f, 22f), depth, S(Pal.ShoeBrown));
            Vector2 red = S(Pal.TempleRed), blue = S(Pal.BoatBlue), yellow = S(Pal.BoatYellow), green = S(Pal.BoatGreen), white = S(Pal.ShirtWhite), black = S(Pal.IronBlack);
            // trampoline in a net cage
            Vector3 tr = new Vector3(-8f, 0f, 5f);
            mb.Cylinder(tr, 2.4f, 2.4f, 0.6f, 16, blue, true);
            mb.Cylinder(tr + Vector3.up * 0.6f, 2.1f, 2.1f, 0.02f, 16, black, true);
            for (int k = 0; k < 8; k++)
            {
                Vector3 p = tr + Quaternion.Euler(0f, k * 45f, 0f) * new Vector3(0f, 0f, 2.35f);
                mb.Cylinder(p, 0.05f, 0.05f, 2.8f, 5, yellow, true);
            }
            mb.Cylinder(tr + Vector3.up * 2.75f, 2.36f, 2.36f, 0.06f, 16, yellow, false);
            // merry-go-round: a striped canopy over seats in a ring
            Vector3 mg = new Vector3(0f, 0f, 6f);
            mb.Cylinder(mg, 2.8f, 2.8f, 0.3f, 16, M(Atlas.MatConcrete), true);
            mb.Cylinder(mg + Vector3.up * 0.3f, 0.2f, 0.2f, 3.3f, 8, white, false);
            for (int k = 0; k < 12; k++)
            {
                var q = Quaternion.Euler(0f, k * 30f, 0f);
                mb.Tri(mg + Vector3.up * 4.4f, mg + q * new Vector3(-0.78f, 3.5f, 2.95f), mg + q * new Vector3(0.78f, 3.5f, 2.95f), k % 2 == 0 ? red : white, q * new Vector3(0f, 1f, 1f));
                if (k % 2 == 0)
                {
                    Vector3 seat = mg + q * new Vector3(0f, 0.9f, 2.2f);
                    mb.Limb(seat + Vector3.up * 0.4f, mg + q * new Vector3(0f, 3.5f, 2.2f), 0.025f, 0.025f, 4, yellow, false);
                    mb.Box(seat, new Vector3(0.6f, 0.5f, 0.8f), q, k % 4 == 0 ? blue : green);
                }
            }
            // swings
            Vector3 sw = new Vector3(8f, 0f, 6f);
            foreach (float x in new[] { -2.2f, 2.2f })
            {
                mb.Limb(sw + new Vector3(x, 0f, -0.8f), sw + new Vector3(x, 2.6f, 0f), 0.06f, 0.06f, 6, red);
                mb.Limb(sw + new Vector3(x, 0f, 0.8f), sw + new Vector3(x, 2.6f, 0f), 0.06f, 0.06f, 6, red);
            }
            mb.Limb(sw + new Vector3(-2.2f, 2.6f, 0f), sw + new Vector3(2.2f, 2.6f, 0f), 0.06f, 0.06f, 6, red);
            foreach (float x in new[] { -1.1f, 0f, 1.1f })
            {
                foreach (float z in new[] { -0.2f, 0.2f }) mb.Limb(sw + new Vector3(x, 2.6f, z * 0.2f), sw + new Vector3(x, 0.55f, z), 0.01f, 0.01f, 3, black, false);
                mb.Box(sw + new Vector3(x, 0.55f, 0f), new Vector3(0.5f, 0.05f, 0.45f), yellow);
            }
            // a small Ferris wheel
            Vector3 fw = new Vector3(8f, 0f, -5f);
            foreach (float z in new[] { -0.7f, 0.7f })
                foreach (float x in new[] { -1.6f, 1.6f }) mb.Limb(fw + new Vector3(x, 0f, z), fw + new Vector3(0f, 4.2f, z), 0.08f, 0.08f, 6, white);
            for (int k = 0; k < 16; k++)
            {
                float a0 = k * Mathf.PI / 8f, a1 = (k + 1) * Mathf.PI / 8f;
                foreach (float z in new[] { -0.6f, 0.6f })
                    mb.Limb(fw + new Vector3(Mathf.Cos(a0) * 3.4f, 4.2f + Mathf.Sin(a0) * 3.4f, z), fw + new Vector3(Mathf.Cos(a1) * 3.4f, 4.2f + Mathf.Sin(a1) * 3.4f, z), 0.05f, 0.05f, 4, blue, false);
                if (k % 2 == 0)
                {
                    Vector3 rim = fw + new Vector3(Mathf.Cos(a0) * 3.4f, 4.2f + Mathf.Sin(a0) * 3.4f, 0f);
                    mb.Limb(fw + new Vector3(0f, 4.2f, 0f), rim, 0.03f, 0.03f, 4, white, false);
                    mb.Box(rim + Vector3.down * 0.55f, new Vector3(0.8f, 0.7f, 0.9f), (k / 2) % 3 == 0 ? red : (k / 2) % 3 == 1 ? yellow : green);
                }
            }
            // balloon shooting stall: a board of balloons behind a counter with air guns
            Vector3 bs = new Vector3(-8f, 0f, -6f);
            mb.Box(bs + new Vector3(0f, 1.6f, -1f), new Vector3(3.6f, 2.4f, 0.1f), S(Pal.JacketBlue));
            int[] bal = { Pal.JacketRed, Pal.BoatYellow, Pal.BoatGreen, Pal.SareePink, Pal.ShirtWhite, Pal.SareeOrange };
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 8; x++)
                    mb.Sphere(bs + new Vector3(-1.5f + x * 0.43f, 0.7f + y * 0.42f, -0.9f), new Vector3(0.16f, 0.19f, 0.12f), 6, 3, S(bal[rng.Next(bal.Length)]));
            mb.Box(bs + new Vector3(0f, 0.5f, 0.6f), new Vector3(3.4f, 1f, 0.6f), S(Pal.WoodLight));
            foreach (float x in new[] { -1f, 0f, 1f }) mb.Box(bs + new Vector3(x, 1.06f, 0.6f), new Vector3(0.08f, 0.08f, 0.8f), black);
            mb.QuadDouble(bs + new Vector3(-2f, 3.2f, -1.2f), bs + new Vector3(2f, 3.2f, -1.2f), bs + new Vector3(2f, 2.8f, 1.2f), bs + new Vector3(-2f, 2.8f, 1.2f), new Rect(red, Vector2.zero), Vector3.up);
            foreach (float x in new[] { -1.9f, 1.9f }) mb.Box(bs + new Vector3(x, 1.5f, 1.1f), new Vector3(0.07f, 3f, 0.07f), WoodBrown);
            // ticket booth and a tea stall
            mb.Box(new Vector3(0f, 1.2f, -8.5f), new Vector3(2f, 2.4f, 1.8f), yellow);
            mb.Box(new Vector3(0f, 2.5f, -8.5f), new Vector3(2.4f, 0.15f, 2.2f), red);
            TarpStall(mb, new Vector3(3.5f, 0f, -8.5f), 180f, Pal.BoatBlue, rng);
            if (WorldBuilder.LowPolyRidesAndVehicles)
            {
                ToyJeep(mb, new Vector3(-3f, 0f, -1f), 20f, Pal.ShirtWhite);
                ToyJeep(mb, new Vector3(-1.4f, 0f, -0.2f), -10f, Pal.JacketRed);
                ToyJeep(mb, new Vector3(0.4f, 0f, -1.3f), 35f, Pal.BoatBlue);
            }
        }

        /// <summary>A low-poly dinosaur: body, neck, head, tail and legs (the park's life-size figures).</summary>
        static void Dino(MeshBuilder mb, Vector3 p, float yaw, float s, bool longNeck, int skin)
        {
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * s);
            Vector2 c = S(skin), belly = S(Pal.PantsKhaki);
            mb.Sphere(new Vector3(0f, 1.5f, 0f), new Vector3(0.7f, 0.65f, 1.3f), 10, 6, c);
            mb.Sphere(new Vector3(0f, 1.25f, 0.1f), new Vector3(0.55f, 0.4f, 1.0f), 8, 4, belly);
            Vector3 neckTop = longNeck ? new Vector3(0f, 3.6f, 1.9f) : new Vector3(0f, 2.3f, 1.5f);
            mb.Limb(new Vector3(0f, 1.8f, 0.9f), neckTop, longNeck ? 0.38f : 0.42f, 0.22f, 8, c);
            mb.Sphere(neckTop + new Vector3(0f, 0.05f, 0.25f), new Vector3(0.28f, 0.26f, longNeck ? 0.45f : 0.6f), 8, 4, c);
            mb.Limb(new Vector3(0f, 1.5f, -1.1f), new Vector3(0f, longNeck ? 0.9f : 1.3f, -3.2f), 0.45f, 0.05f, 8, c);
            foreach (float x in new[] { -0.45f, 0.45f })
                foreach (float z in new[] { -0.6f, 0.6f })
                {
                    bool fore = z > 0f;
                    if (!longNeck && fore) mb.Limb(new Vector3(x, 1.5f, 0.8f), new Vector3(x * 1.2f, 1.1f, 1.2f), 0.1f, 0.07f, 5, c);   // the small arms of a raptor
                    else mb.Limb(new Vector3(x, 1.4f, z), new Vector3(x * 1.1f, 0f, z), 0.24f, 0.18f, 6, c);
                }
            mb.M = saved;
        }

        static void DinoPark(MeshBuilder mb, float depth, System.Random rng)
        {
            Plinth(mb, new Vector3(24f, 0f, 22f), depth, S(Pal.Grass));
            Vector2 mosaic = S(Pal.BoatBlue), green = S(Pal.BoatGreen), tin = S(Pal.Concrete), wood = WoodBrown;
            // the blue mosaic arch at the front, a claw-gripping beast's head on top, "ADVENTURE PARK" boards
            foreach (float x in new[] { -2.4f, 2.4f }) mb.Box(new Vector3(x, 1.7f, 10.5f), new Vector3(1.2f, 3.4f, 1.2f), mosaic);
            Vector3 last = default;
            for (int k = 0; k <= 10; k++)
            {
                float a = k / 10f * Mathf.PI;
                Vector3 q = new Vector3(Mathf.Cos(a) * 2.4f, 3.4f + Mathf.Sin(a) * 1.6f, 10.5f);
                if (k > 0) mb.Limb(last, q, 0.55f, 0.55f, 8, mosaic);
                last = q;
            }
            mb.Sphere(new Vector3(0f, 5.6f, 10.5f), new Vector3(1.0f, 0.8f, 1.1f), 8, 5, S(Pal.StoneGrey));
            foreach (float x in new[] { -0.7f, 0.7f }) mb.Limb(new Vector3(x, 5.4f, 11.2f), new Vector3(x * 1.3f, 4.7f, 11.6f), 0.12f, 0.04f, 5, S(Pal.ShirtWhite));
            mb.Box(new Vector3(0f, 3.2f, 11.05f), new Vector3(3.2f, 0.5f, 0.06f), S(Pal.SareeOrange));
            // three sheds: green steel frames, tin roofs, a forest backdrop, grass floor, a rope rail; a beast in each
            float[] shedX = { -7.5f, 0f, 7.5f };
            int[] skins = { Pal.ShoeBrown, Pal.StoneGrey, Pal.PantsKhaki };
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = new Vector3(shedX[k], 0f, -3f);
                foreach (float x in new[] { -3.2f, 3.2f })
                    foreach (float z in new[] { -3.5f, 3.5f }) mb.Box(c + new Vector3(x, 2.2f, z), new Vector3(0.18f, 4.4f, 0.18f), green);
                mb.Box(c + new Vector3(0f, 4.5f, 0f), new Vector3(7f, 0.08f, 7.6f), tin);
                mb.Box(c + new Vector3(0f, 2.2f, -3.6f), new Vector3(6.4f, 4.4f, 0.06f), S(Pal.Pine));
                mb.Box(c + new Vector3(0f, 0.02f, 0f), new Vector3(6.4f, 0.04f, 7f), S(Pal.Grass));
                foreach (float x in new[] { -3f, 3f }) mb.Box(c + new Vector3(x, 0.5f, 3.2f), new Vector3(0.06f, 1f, 0.06f), wood);
                mb.Box(c + new Vector3(0f, 0.85f, 3.2f), new Vector3(6f, 0.03f, 0.03f), S(Pal.ShirtWhite));
                for (int r = 0; r < 3; r++) Boulder(mb, c + new Vector3(-2.4f + r * 2.3f, 0f, -2.5f), 0.5f, rng);
                Dino(mb, c + new Vector3(0f, 0f, -0.5f), 160f + k * 25f, k == 1 ? 1.1f : 0.85f, k == 1, skins[k]);
            }
            // a pterosaur hung under the middle roof
            Vector2 wing = S(Pal.ShoeBrown);
            Vector3 pt = new Vector3(0f, 3.6f, -3f);
            mb.Sphere(pt, new Vector3(0.25f, 0.25f, 0.8f), 8, 4, wing);
            mb.QuadDouble(pt + new Vector3(-3f, 0.2f, -0.4f), pt + new Vector3(0f, 0f, -0.4f), pt + new Vector3(0f, 0f, 0.4f), pt + new Vector3(-2.6f, 0.3f, 0.3f), new Rect(wing, Vector2.zero), Vector3.up);
            mb.QuadDouble(pt + new Vector3(0f, 0f, -0.4f), pt + new Vector3(3f, 0.2f, -0.4f), pt + new Vector3(2.6f, 0.3f, 0.3f), pt + new Vector3(0f, 0f, 0.4f), new Rect(wing, Vector2.zero), Vector3.up);
            // a raptor out in the open by the path
            Dino(mb, new Vector3(-8f, 0f, 6f), 30f, 0.7f, false, Pal.JacketGreen);
        }
    }
}

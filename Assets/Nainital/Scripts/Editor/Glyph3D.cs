using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Extruded 3D lettering (user, 2026-11-18: "saare jahan jahan text hai, bahut basic hai - usko tagda 3D name kardo,
    /// different stylish font based on their category"). Reads Data/Fonts3D/*.json (Tools/fonts3d/make_fonts3d.py: the
    /// outlines of eight OFL display fonts, triangulated) and writes words as solid letters into a MeshBuilder: a face
    /// in one atlas colour standing `depth` proud of the board, sides in another. Being plain atlas geometry, the
    /// letters batch with the rest of the town (the old TextMesh signs were one renderer each). Each shop kind has its
    /// own style (font, board, letters): <see cref="StyleFor"/>.
    /// </summary>
    public static class Glyph3D
    {
        public enum Face { Script, Round, Royal, Desi, Serif, Tall, Block, Deco }

        [System.Serializable] class ContourJ { public float[] p; }
        [System.Serializable] class GlyphJ { public int c; public float adv; public float[] pts; public int[] tris; public ContourJ[] contours; }
        [System.Serializable] class FontJ { public string key; public float space; public GlyphJ[] glyphs; }

        sealed class Font3D
        {
            public float space;
            public readonly Dictionary<char, GlyphJ> glyphs = new Dictionary<char, GlyphJ>();
        }

        static readonly Dictionary<Face, Font3D> fonts = new Dictionary<Face, Font3D>();

        static Font3D Load(Face face)
        {
            if (fonts.TryGetValue(face, out var f)) return f;
            string path = "Assets/Nainital/Data/Fonts3D/" + face.ToString().ToLowerInvariant() + ".json";
            f = new Font3D();
            if (System.IO.File.Exists(path))
            {
                var j = JsonUtility.FromJson<FontJ>(System.IO.File.ReadAllText(path));
                f.space = j.space;
                foreach (var g in j.glyphs) f.glyphs[(char)g.c] = g;
            }
            else Debug.LogWarning("[Nainital] no 3D font " + path + " (run Tools/fonts3d/make_fonts3d.py)");
            fonts[face] = f;
            return f;
        }

        /// <summary>Width of `text` in cap heights.</summary>
        public static float Measure(string text, Face face)
        {
            var f = Load(face);
            float w = 0f;
            foreach (char ch in text)
                w += ch == ' ' ? f.space : f.glyphs.TryGetValue(ch, out var g) ? g.adv : f.space;
            return w;
        }

        /// <summary>
        /// One line of solid letters, readable from the `outward` side, its middle (half the cap height) at `center` on
        /// the board's face, letters `capHeight` tall (shrunk to fit `maxWidth`), standing `depth` out. Returns the cap
        /// height used.
        /// </summary>
        public static float Write(MeshBuilder mb, string text, Face face, Vector3 center, Vector3 outward, float capHeight, float depth,
                                  float maxWidth, Vector2 frontUV, Vector2 sideUV)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var f = Load(face);
            if (f.glyphs.Count == 0) return 0f;
            outward = outward.normalized;
            Vector3 right = Vector3.Cross(outward, Vector3.up);
            if (right.sqrMagnitude < 1e-6f) return 0f;
            right.Normalize();
            Vector3 up = Vector3.Cross(right, outward).normalized;   // = world up for a vertical board
            if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;
            float units = Measure(text, face);
            float s = capHeight;
            if (units * s > maxWidth && units > 0f) s = maxWidth / units;
            Vector3 origin = center - right * (units * s * 0.5f) - up * (s * 0.5f);
            Vector3 lift = outward * depth;
            float x = 0f;
            foreach (char ch in text)
            {
                if (ch == ' ' || !f.glyphs.TryGetValue(ch, out var g)) { x += f.space; continue; }
                Vector3 at = origin + right * (x * s);
                var p = g.pts;
                Vector3 P(int i) => at + right * (p[2 * i] * s) + up * (p[2 * i + 1] * s);
                for (int t = 0; t + 2 < g.tris.Length; t += 3)
                    mb.Tri(P(g.tris[t]) + lift, P(g.tris[t + 1]) + lift, P(g.tris[t + 2]) + lift, frontUV, outward);
                foreach (var c in g.contours)
                {
                    var q = c.p;
                    int n = q.Length / 2;
                    for (int i = 0; i < n; i++)
                    {
                        int k = (i + 1) % n;
                        Vector3 a = at + right * (q[2 * i] * s) + up * (q[2 * i + 1] * s);
                        Vector3 b = at + right * (q[2 * k] * s) + up * (q[2 * k + 1] * s);
                        // outer contours run counter-clockwise, holes clockwise: the solid is on the left, so out is right
                        float dx = q[2 * k] - q[2 * i], dy = q[2 * k + 1] - q[2 * i + 1];
                        Vector3 side = (right * dy - up * dx).normalized;
                        mb.Quad(a, b, b + lift, a + lift, sideUV, side);
                    }
                }
                x += g.adv;
            }
            return s;
        }

        // ------------------------------------------------------------------ styles by shop kind
        public struct Style
        {
            public Face face;
            public int[] boards, letters;   // palette choices (Pal.*); letters[i] goes with any board
            public int side;                // the letters' sides
            public float glowChance;        // letters in lamp-bulb colour, lit at night
        }

        static readonly Style Eatery = new Style { face = Face.Script, boards = new[] { Pal.TempleRed, Pal.IronBlack, Pal.WoodDark, Pal.BoatGreen }, letters = new[] { Pal.BoatYellow, Pal.ShirtWhite, Pal.Gold }, side = Pal.WoodDark, glowChance = 0.45f };
        static readonly Style Sweet = new Style { face = Face.Round, boards = new[] { Pal.SareePink, Pal.TempleWhite, Pal.BoatBlue, Pal.BoatYellow }, letters = new[] { Pal.TempleRed, Pal.BoatBlue, Pal.ShirtWhite }, side = Pal.ShirtWhite, glowChance = 0.25f };
        static readonly Style Hotel = new Style { face = Face.Royal, boards = new[] { Pal.TempleWhite, Pal.IronGreen, Pal.IronBlack, Pal.TempleRed }, letters = new[] { Pal.Gold, Pal.TempleRed, Pal.Gold }, side = Pal.Gold, glowChance = 0.5f };
        static readonly Style Craft = new Style { face = Face.Desi, boards = new[] { Pal.Saffron, Pal.BoatYellow, Pal.TempleRed, Pal.BoatBlue }, letters = new[] { Pal.TempleRed, Pal.IronBlack, Pal.ShirtWhite }, side = Pal.WoodDark, glowChance = 0.15f };
        static readonly Style Wool = new Style { face = Face.Serif, boards = new[] { Pal.IronGreen, Pal.TempleRed, Pal.WoodDark, Pal.TempleWhite }, letters = new[] { Pal.Gold, Pal.ShirtWhite, Pal.TempleRed }, side = Pal.Gold, glowChance = 0.2f };
        static readonly Style Wear = new Style { face = Face.Tall, boards = new[] { Pal.IronBlack, Pal.BoatBlue, Pal.JacketRed, Pal.ShirtWhite }, letters = new[] { Pal.ShirtWhite, Pal.BoatYellow, Pal.IronBlack }, side = Pal.StoneGrey, glowChance = 0.3f };
        static readonly Style Service = new Style { face = Face.Block, boards = new[] { Pal.BoatBlue, Pal.BoatGreen, Pal.ShirtWhite, Pal.JacketRed }, letters = new[] { Pal.ShirtWhite, Pal.ShirtWhite, Pal.BoatBlue }, side = Pal.StoneGrey, glowChance = 0.3f };

        public static Style StyleFor(int room)
        {
            if (room == Atlas.RoomBakery || room == Atlas.RoomIceCream || room == Atlas.RoomSweets) return Sweet;
            if (Atlas.RoomIsEatery(room)) return Eatery;
            if (Atlas.RoomIsHotel(room)) return Hotel;
            if (room == Atlas.RoomShawls || room == Atlas.RoomWoollens) return Wool;
            if (room == Atlas.RoomFashion || room == Atlas.RoomEthnic || room == Atlas.RoomBags || room == Atlas.RoomFootwear) return Wear;
            if (room == Atlas.RoomOptical || room == Atlas.RoomBank || room == Atlas.RoomTravel || room == Atlas.RoomPharmacy || room == Atlas.RoomMobile || room == Atlas.RoomBooks) return Service;
            return Craft;   // gifts, souvenirs, Tibetan crafts, candles, local produce, and shut shops
        }

        /// <summary>A style for a shop known only by its name (the bazaars' signs).</summary>
        public static Style StyleForName(string name)
        {
            string n = name.ToLowerInvariant();
            bool Has(params string[] w) { foreach (var s in w) if (n.Contains(s)) return true; return false; }
            if (Has("bakery", "sweet", "mithai", "ice cream", "dairy", "cake")) return Sweet;
            if (Has("restaurant", "dhaba", "cafe", "momo", "food", "chai", "tea", "bhojanalaya", "dosa", "pizza")) return Eatery;
            if (Has("hotel", "lodge", "resort", "inn", "guest house")) return Hotel;
            if (Has("shawl", "woollen", "woolen", "pashmina", "knit")) return Wool;
            if (Has("fashion", "garment", "cloth", "saree", "sari", "shoe", "footwear", "bag", "boutique", "tailor")) return Wear;
            if (Has("medical", "chemist", "pharma", "bank", "optic", "travel", "tour", "mobile", "electronic", "book", "photo", "store", "kirana", "general", "jewel", "hardware")) return Service;
            return Craft;
        }

        // ------------------------------------------------------------------ names by shop kind
        static readonly Dictionary<int, string[]> Names = new Dictionary<int, string[]>
        {
            { Atlas.RoomRestFamily, new[] { "Naina Restaurant", "Kumaon Rasoi", "Lake View Restaurant", "Pahadi Zaika" } },
            { Atlas.RoomRestPunjabi, new[] { "Punjab Da Dhaba", "Sher-e-Punjab", "Amritsari Rasoi", "Mall Road Dhaba" } },
            { Atlas.RoomRestMulti, new[] { "Moonlit Restaurant", "The Lake Palace", "Royal Kitchen", "Hill Top Restaurant" } },
            { Atlas.RoomRestTibetan, new[] { "Tashi Delek Momos", "Lhasa Kitchen", "Yak & Yeti", "Little Tibet" } },
            { Atlas.RoomCafe, new[] { "Cafe Lakeview", "Mountain Brew Cafe", "The Pine Cafe", "Cafe Chinar" } },
            { Atlas.RoomBakery, new[] { "Hill Queen Bakery", "Naini Bakers", "Sweet Crumbs", "Pine Cone Bakery" } },
            { Atlas.RoomIceCream, new[] { "Lakeside Ice Cream", "Snowdrop Ice Cream", "Frosty Treats", "Scoops" } },
            { Atlas.RoomSweets, new[] { "Naini Sweets", "Kumaon Mishthan", "Bal Mithai House", "Annapurna Sweets" } },
            { Atlas.RoomFastFood, new[] { "Mountain Bites", "Momo Corner", "Quick Bite", "Hungry Hill" } },
            { Atlas.RoomGiftGallery, new[] { "Himalayan Gift Gallery", "Naina Gift House", "Kumaon Arts", "Pahadi Crafts" } },
            { Atlas.RoomSouvenir, new[] { "Souvenir Corner", "Naini Memories", "Lake City Gifts", "Hill Souvenirs" } },
            { Atlas.RoomTibetanCraft, new[] { "Tibetan Handicrafts", "Shangri-La Crafts", "Om Mani Arts", "Prayer Wheel" } },
            { Atlas.RoomCandles, new[] { "Naini Candle House", "Candle Bazaar", "Wax Wonders", "Mom Batti Ghar" } },
            { Atlas.RoomShawls, new[] { "Kumaon Shawl House", "Pashmina Palace", "Himalayan Shawls", "Kashmiri Shawls" } },
            { Atlas.RoomWoollens, new[] { "Himalayan Woollens", "Woollen Emporium", "Warm Wear", "Knit & Wool" } },
            { Atlas.RoomFashion, new[] { "Mall Road Fashion", "Style Point", "Trendz", "The Jeans Shop" } },
            { Atlas.RoomEthnic, new[] { "Sari Sadan", "Kurti Corner", "Ethnic Threads", "Rang Mahal" } },
            { Atlas.RoomBags, new[] { "Bag Bazaar", "Travel Bags", "Bag House", "Leather World" } },
            { Atlas.RoomFootwear, new[] { "Shoe Palace", "Footwear Point", "Step In", "Hill Walkers" } },
            { Atlas.RoomLocalProduce, new[] { "Pahadi Organics", "Kumaon Honey", "Uttarakhand Store", "Pahadi Bazaar" } },
            { Atlas.RoomOptical, new[] { "Vision Opticals", "Clear Sight", "Eye Care", "Naini Optics" } },
            { Atlas.RoomBank, new[] { "Naini Co-op Bank", "Kumaon Bank", "Hill Bank", "Lake City Bank" } },
            { Atlas.RoomTravel, new[] { "Naini Tours & Travels", "Himalayan Treks", "Kumaon Travels", "Hill Holidays" } },
            { Atlas.RoomPharmacy, new[] { "Joshi Medical Hall", "Naini Medicos", "Life Care Chemist", "City Medical" } },
            { Atlas.RoomMobile, new[] { "Mobile Point", "Connect Mobiles", "Phone Zone", "Hello Mobiles" } },
            { Atlas.RoomHotelDesk, new[] { "Hotel Pine Crest", "Hotel Lake Inn", "Hotel Himalaya", "Hotel Snow View" } },
            { Atlas.RoomHotelHeritage, new[] { "The Grand Royal", "Hotel Belvedere", "Alka The Lake", "Hotel Metropole" } },
            { Atlas.RoomBooks, new[] { "Naini Book Depot", "Snap Photo Studio", "Hill Book House", "Lake Studio" } },
        };
        static readonly string[] ShutNames = { "Gupta General Store", "Bisht Electronics", "Negi Hardware", "Rawat Kirana", "Shah Jewellers", "Pant Tailors" };

        public static string NameFor(int room, System.Random rnd)
        {
            if (room >= 0 && Names.TryGetValue(room, out var list)) return list[rnd.Next(list.Length)];
            return ShutNames[rnd.Next(ShutNames.Length)];
        }

        /// <summary>Board, letters and glow for one sign of a style. Letters never in the board's own colour.</summary>
        public static void Pick(Style st, System.Random rnd, out int board, out Vector2 front, out Vector2 side)
        {
            board = st.boards[rnd.Next(st.boards.Length)];
            int letters = st.letters[rnd.Next(st.letters.Length)];
            if (letters == board) letters = board == Pal.ShirtWhite || board == Pal.TempleWhite ? Pal.TempleRed : Pal.ShirtWhite;
            bool glow = rnd.NextDouble() < st.glowChance;
            front = Atlas.SolidUV(glow ? Pal.LampBulb : letters);
            side = Atlas.SolidUV(st.side == board ? Pal.StoneGrey : st.side);
        }
    }
}

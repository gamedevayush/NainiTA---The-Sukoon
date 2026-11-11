using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Hand-shaped (code-built) Nainital landmarks placed at their real OpenStreetMap positions.</summary>
    public static class LandmarkBuilder
    {
        static Vector2 S(int pal) => Atlas.SolidUV(pal);
        static Vector2 M(int mat) => Atlas.MaterialUV(mat);

        public static readonly Dictionary<string, string> Info = new Dictionary<string, string>
        {
            ["Naini Jheel"] = "Kumaon ki sabse mashhoor jheel, lagbhag 1,938 m ki unchai par. Aankh (nain) jaise aakar ki - manyata hai ki Mata Sati ki aankh yahan giri thi. Lambai lagbhag 1.4 km. Uttari sira Mallital, dakshini sira Tallital.",
            ["Naina Devi Mandir"] = "51 Shaktipeethon mein se ek. Manyata hai ki yahan Mata Sati ki aankhein giri thin. Purana mandir 1880 ke bade bhooskhalan (landslide) mein nasht ho gaya tha, baad mein dobara banaya gaya. Jheel ke uttari kinare, Mallital mein.",
            ["Gurudwara Sri Guru Singh Sabha"] = "Mallital mein Naina Devi mandir ke bilkul paas. Shaant mahaul, langar aur jheel ka sundar nazara.",
            ["St. Francis Church"] = "Mallital ka purana Catholic church - British daur ki pathar ki imaarat.",
            ["Raj Bhavan"] = "Uttarakhand ke Rajyapal ka nivas. 1897-1900 ke beech Buckingham Palace ki tarz par Gothic shaili mein bana. Saath mein golf course bhi hai.",
            ["Bada Bazar"] = "Mallital ka purana bazaar - woolen kapde, Nainital ki mashhoor rang-birangi candles aur khaane-peene ki dukaanein.",
            ["The Flats"] = "1880 ke bade landslide ke malbe se bana khula maidan. Aaj yahan khel, mele aur shaam ki raunak hoti hai.",
            ["Snow View"] = "Lagbhag 2,270 m unchai. Mallital se ropeway (cable car) se pahunchte hain. Saaf mausam mein Nanda Devi samet Himalaya ki chotiyan dikhti hain.",
            ["Naini Peak (China Peak)"] = "Nainital ki sabse oonchi choti, lagbhag 2,615 m. Upar se poora shehar, jheel aur door Himalaya dikhte hain.",
            ["Tiffin Top (Dorothy's Seat)"] = "Ayarpatta pahadi par. Angrez painter Dorothy Kellett ki yaad mein unke pati ne yahan seat banwayi thi. Picnic ke liye mashhoor.",
            ["Land's End"] = "Yahan se Khurpatal jheel aur gehri ghaati ka shaandaar nazara milta hai.",
            ["Lover's Point"] = "Barapathar ke paas ek khula kinara - neeche gehri ghaati aur door Khurpatal ki taraf ka nazara.",
            ["Barapathar (pony stand)"] = "Yahan se ghode Tiffin Top aur Land's End tak le jaate hain. Kaladhungi road par, Nainital se lagbhag 3 km.",
            ["Khurpatal Lake View Point"] = "Kaladhungi road se neeche Khurpatal jheel aur uske kheton ka nazara.",
            ["Khurpatal View Point"] = "Khurpatal jheel ke theek upar ka view point - hare paani ki chhoti jheel aur seedhidaar khet.",
            ["Sariyatal"] = "Nainital se ~5 km, Kaladhungi road par ek chhoti si jheel. Paas mein Woodland jharna aur chai-maggi ki dukaanein.",
            ["Khurpatal"] = "Kaladhungi road par Nainital se ~10 km, 1,635 m par ek hari jheel, khet aur chhote homestays se ghiri hui.",
            ["Himalaya Darshan"] = "Kilbury road par Nainital ka mashhoor view point. Saaf mausam mein yahan se Himalaya ki barfeeli chotiyan - Nanda Devi, Trishul aur Nanda Kot - dikhti hain.",
            ["Eco Cave Garden"] = "Aapas mein judi pathreeli gufaon ka bagicha - gufaon ke naam jaanwaron par hain, jaise Tiger Cave aur Panther Cave.",
            ["Nainital Zoo"] = "Pt. G.B. Pant High Altitude Zoo - Sher ka Danda pahadi par, lagbhag 2,100 m unchai par, pahadi jaanwaron aur pakshiyon ka ghar.",
            ["Boat House Club"] = "Nainital Boat Club - British daur ka purana yacht club. Jheel mein tairti safed paal wali yachts isi ki pehchaan hain.",
            ["Mall Road"] = "Mallital se Tallital tak jheel ke kinare ki sadak, sarkari naam Govind Ballabh Pant Marg. Shaam ko lights aur jheel mein unka pratibimb dekhne laayak hai.",
            ["Tallital"] = "Jheel ka dakshini sira - bus adda, boat stand aur Mall Road ka doosra chhor.",
            ["Mallital"] = "Jheel ka uttari sira - Naina Devi mandir, The Flats, Bada Bazar, Boat Club aur ropeway yahin hain.",
        };

        // ------------------------------------------------------------------ helpers
        struct Site { public Vector3 origin; public Quaternion rot; public float depth; }

        static Site MakeSite(WorldCtx ctx, Vector2 p, Vector2 faceTowards, float radius, float minLakeDist = 14f)
        {
            p = ctx.PushFromLake(p, minLakeDist + radius * 0.5f);
            float gMin = float.MaxValue, gMax = float.MinValue;
            for (int i = 0; i <= 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                foreach (float rr in new[] { 0f, radius * 0.5f, radius })
                {
                    float g = ctx.H(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
                    gMin = Mathf.Min(gMin, g);
                    gMax = Mathf.Max(gMax, g);
                }
            }
            Vector2 f = faceTowards - p;
            float yaw = f.sqrMagnitude > 0.01f ? Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg : 0f;
            float top = Mathf.Max(gMax + 0.15f, ctx.lakeY + 1.6f);
            return new Site { origin = new Vector3(p.x, top, p.y), rot = Quaternion.Euler(0, yaw, 0), depth = top - gMin + 1.2f };
        }

        static GameObject Emit(WorldCtx ctx, string name, Site s, MeshBuilder mb, Transform parent, bool collider = true)
        {
            var go = ctx.MeshObject(name, parent, mb, ctx.mats.atlas, null, true, collider);
            return go;
        }

        static void Sign(WorldCtx ctx, string title, Site s, Vector3 local, Transform parent)
        {
            Vector3 pos = s.origin + s.rot * local;
            pos.y = Mathf.Max(ctx.H(new Vector2(pos.x, pos.z)), s.origin.y - 0.2f);
            ctx.InfoSign(title, Info.TryGetValue(title, out var body) ? body : title, pos, s.rot, parent);
        }

        static void Plinth(MeshBuilder mb, Vector3 size, float depth, Vector2 uv)
        {
            mb.Box(new Vector3(0, -depth * 0.5f, 0), new Vector3(size.x, depth, size.z), uv);
        }

        static void Flag(MeshBuilder mb, Vector3 poleBase, float height, int color)
        {
            mb.Limb(poleBase, poleBase + Vector3.up * height, 0.07f, 0.04f, 5, S(Pal.Gold));
            Vector3 t = poleBase + Vector3.up * (height - 0.1f);
            Vector3 b = poleBase + Vector3.up * (height - 1.3f);
            Vector3 tip = poleBase + Vector3.up * (height - 0.7f) + Vector3.right * 2.2f;
            mb.Tri(t, b, tip, S(color), Vector3.forward);
            mb.Tri(t, b, tip, S(color), Vector3.back);
        }

        // ------------------------------------------------------------------ landmarks
        static void NainaDevi(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(22, 0, 18), depth, M(Atlas.MatMarble));
            Vector2 white = S(Pal.TempleWhite), red = S(Pal.TempleRed), orange = M(Atlas.MatTempleOrange), gold = M(Atlas.MatGold);
            // sanctum with Kumaoni pagoda roofs: three red tiers with white trim, a gold finial and red flags (photo sphere, 2022)
            mb.Box(new Vector3(0, 2.3f, -3), new Vector3(6, 4.6f, 6), white);
            float y = 4.6f;
            float[] tier = { 8.2f, 6.0f, 3.9f };
            for (int i = 0; i < tier.Length; i++)
            {
                mb.Box(new Vector3(0, y + 0.08f, -3), new Vector3(tier[i] + 0.2f, 0.16f, tier[i] + 0.2f), white);
                mb.Pyramid(new Vector3(0, y + 0.16f, -3), tier[i], tier[i], 1.5f - i * 0.15f, red);
                y += 1.5f - i * 0.15f;
                if (i < tier.Length - 1)
                {
                    float drum = tier[i + 1] * 0.55f;
                    mb.Box(new Vector3(0, y + 0.45f, -3), new Vector3(drum, 0.9f, drum), white);
                    y += 0.9f;
                }
            }
            mb.Cylinder(new Vector3(0, y - 0.1f, -3), 0.35f, 0.25f, 0.5f, 10, gold, true);
            mb.Sphere(new Vector3(0, y + 0.6f, -3), Vector3.one * 0.32f, 8, 5, gold);
            mb.Cylinder(new Vector3(0, y + 0.9f, -3), 0.1f, 0f, 0.8f, 6, gold, false);
            Flag(mb, new Vector3(0.3f, y + 1.4f, -3), 3.2f, Pal.TempleRed);
            // mandapa (porch) under a red pitched roof
            foreach (float x in new[] { -2.4f, 2.4f })
                foreach (float z in new[] { 1.0f, 4.0f })
                    mb.Cylinder(new Vector3(x, 0, z), 0.24f, 0.2f, 3.3f, 8, white, true);
            mb.Box(new Vector3(0, 3.42f, 2.3f), new Vector3(6.2f, 0.2f, 4.8f), white);
            mb.Pyramid(new Vector3(0, 3.52f, 2.3f), 6.6f, 5.2f, 1.7f, red);
            for (int i = -2; i <= 2; i++) mb.Sphere(new Vector3(i * 0.9f, 2.9f, 4.4f), Vector3.one * 0.13f, 6, 3, gold);   // bells
            // side shrines, each with two red tiers
            foreach (float x in new[] { -6.5f, 6.5f })
            {
                mb.Box(new Vector3(x, 1.5f, -2.5f), new Vector3(3.0f, 3.0f, 3.0f), white);
                mb.Box(new Vector3(x, 3.04f, -2.5f), new Vector3(4.3f, 0.12f, 4.3f), white);
                mb.Pyramid(new Vector3(x, 3.1f, -2.5f), 4.2f, 4.2f, 1.1f, red);
                mb.Box(new Vector3(x, 4.5f, -2.5f), new Vector3(1.4f, 0.6f, 1.4f), white);
                mb.Pyramid(new Vector3(x, 4.8f, -2.5f), 2.2f, 2.2f, 0.9f, red);
                mb.Sphere(new Vector3(x, 5.85f, -2.5f), Vector3.one * 0.18f, 6, 3, gold);
            }
            // tall poles with red flags at the corners of the courtyard
            foreach (var fp in new[] { new Vector3(-9.5f, 0, 6.5f), new Vector3(9.5f, 0, 6.5f), new Vector3(-9.5f, 0, -7.5f) })
            {
                mb.Cylinder(fp, 0.07f, 0.05f, 9f, 6, S(Pal.WoodDark), true);
                Flag(mb, fp + new Vector3(0.1f, 8.6f, 0), 2.6f, Pal.TempleRed);
            }
            // courtyard wall + gate
            mb.Box(new Vector3(0, 0.5f, -8.7f), new Vector3(22, 1f, 0.4f), white);
            mb.Box(new Vector3(-10.8f, 0.5f, 0), new Vector3(0.4f, 1f, 17.4f), white);
            mb.Box(new Vector3(10.8f, 0.5f, 0), new Vector3(0.4f, 1f, 17.4f), white);
            foreach (float x in new[] { -2.2f, 2.2f })
            {
                mb.Box(new Vector3(x, 2.2f, 8.6f), new Vector3(0.7f, 4.4f, 0.7f), S(Pal.SareePink));
                mb.Box(new Vector3(x, 4.6f, 8.6f), new Vector3(0.9f, 0.4f, 0.9f), red);
            }
            mb.Box(new Vector3(0, 4.3f, 8.6f), new Vector3(5.2f, 0.7f, 0.6f), red);
            mb.Box(new Vector3(0, 4.85f, 8.6f), new Vector3(3f, 0.5f, 0.5f), gold);
            // sacred peepal tree
            mb.Cylinder(new Vector3(-7.5f, 0, 5), 0.9f, 0.55f, 5.5f, 8, S(Pal.Trunk), false);
            mb.Sphere(new Vector3(-7.5f, 8.5f, 5), new Vector3(5f, 3.6f, 4.8f), 9, 5, S(Pal.Oak));
            mb.Sphere(new Vector3(-5.2f, 7.4f, 6.5f), new Vector3(3.2f, 2.4f, 3f), 8, 4, S(Pal.OakLight));
            mb.Sphere(new Vector3(-9.6f, 7.8f, 3.6f), new Vector3(3.1f, 2.5f, 3.2f), 8, 4, S(Pal.OakLight));
            mb.Cylinder(new Vector3(-7.5f, 0, 5), 1.8f, 1.8f, 0.6f, 10, white, true);   // chabutra
        }

        static void Gurudwara(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(18, 0, 14), depth, M(Atlas.MatMarble));
            Vector2 white = S(Pal.TempleWhite), gold = M(Atlas.MatGold), glass = M(Atlas.MatGlass);
            mb.Box(new Vector3(0, 3.2f, -1), new Vector3(14, 6.4f, 10), white);
            mb.Box(new Vector3(0, 6.55f, -1), new Vector3(14.6f, 0.3f, 10.6f), gold);
            for (int i = -2; i <= 2; i++)   // arched openings
                mb.Quad(new Vector3(i * 2.6f - 0.8f, 0.4f, 4.02f), new Vector3(i * 2.6f + 0.8f, 0.4f, 4.02f), new Vector3(i * 2.6f + 0.8f, 3.4f, 4.02f), new Vector3(i * 2.6f - 0.8f, 3.4f, 4.02f), glass, Vector3.forward);
            mb.Cylinder(new Vector3(0, 6.7f, -1), 2.5f, 2.5f, 1.3f, 12, white, true);
            mb.Sphere(new Vector3(0, 8.0f, -1), new Vector3(2.7f, 2.9f, 2.7f), 12, 5, gold, 0f, 90f);
            mb.Cylinder(new Vector3(0, 10.8f, -1), 0.25f, 0f, 1.6f, 6, gold, false);
            foreach (float x in new[] { -6f, 6f })
                foreach (float z in new[] { -5f, 3f })
                {
                    foreach (float dx in new[] { -0.5f, 0.5f })
                        foreach (float dz in new[] { -0.5f, 0.5f })
                            mb.Cylinder(new Vector3(x + dx, 6.7f, z + dz), 0.07f, 0.07f, 1.1f, 4, white, false);
                    mb.Sphere(new Vector3(x, 7.8f, z), new Vector3(0.75f, 0.8f, 0.75f), 8, 3, gold, 0f, 90f);
                }
            Flag(mb, new Vector3(7.5f, 0, 5.5f), 15f, Pal.Saffron);
        }

        static void Church(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(14, 0, 28), depth, M(Atlas.MatDarkStone));
            Vector2 stone = M(Atlas.MatDarkStone), glass = M(Atlas.MatGlass), wood = S(Pal.WoodDark);
            mb.Box(new Vector3(0, 3.6f, -2), new Vector3(9, 7.2f, 18), stone);
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(new Vector3(0, 7.2f, -2), Quaternion.Euler(0, 90, 0), Vector3.one);
            mb.GableRoof(Vector3.zero, 18f, 9f, 4.2f, Atlas.Roof(6), stone, 0.5f);
            mb.M = saved;
            for (int i = 0; i < 5; i++)
                foreach (float x in new[] { -4.52f, 4.52f })
                {
                    float z = -9f + i * 3.6f;
                    Vector3 n = x > 0 ? Vector3.right : Vector3.left;
                    mb.Quad(new Vector3(x, 2f, z - 0.5f), new Vector3(x, 2f, z + 0.5f), new Vector3(x, 5f, z + 0.5f), new Vector3(x, 5f, z - 0.5f), glass, n);
                    mb.Tri(new Vector3(x, 5f, z - 0.5f), new Vector3(x, 5f, z + 0.5f), new Vector3(x, 5.8f, z), glass, n);
                }
            mb.Box(new Vector3(0, 6.5f, 9.2f), new Vector3(4.6f, 13f, 4.6f), stone);
            mb.Pyramid(new Vector3(0, 13f, 9.2f), 4.8f, 4.8f, 7f, S(Pal.StoneGrey));
            mb.Box(new Vector3(0, 20.6f, 9.2f), new Vector3(0.12f, 1.4f, 0.12f), S(Pal.Gold));
            mb.Box(new Vector3(0, 20.9f, 9.2f), new Vector3(0.7f, 0.12f, 0.12f), S(Pal.Gold));
            mb.Quad(new Vector3(-0.9f, 0, 11.52f), new Vector3(0.9f, 0, 11.52f), new Vector3(0.9f, 3f, 11.52f), new Vector3(-0.9f, 3f, 11.52f), wood, Vector3.forward);
        }

        static void RajBhavan(MeshBuilder mb, float depth, System.Random rng)
        {
            Plinth(mb, new Vector3(56, 0, 26), depth, M(Atlas.MatStone));
            var st = new BuildingKit.Style { wall = 7, roof = 4, flatRoof = false, shop = false, litChance = 0.6f };
            float y0 = 0f, y1 = 10f;
            void Block(float cx, float cz, float w, float d, float h)
            {
                Vector2 c = new Vector2(cx, cz);
                Vector2 fl = c + new Vector2(-w / 2, d / 2), fr = c + new Vector2(w / 2, d / 2), br = c + new Vector2(w / 2, -d / 2), bl = c + new Vector2(-w / 2, -d / 2);
                BuildingKit.FacadeWall(mb, fl, fr, y0, h, y0, Vector3.forward, st, rng, true);
                BuildingKit.FacadeWall(mb, fr, br, y0, h, y0, Vector3.right, st, rng, false);
                BuildingKit.FacadeWall(mb, br, bl, y0, h, y0, Vector3.back, st, rng, false);
                BuildingKit.FacadeWall(mb, bl, fl, y0, h, y0, Vector3.left, st, rng, false);
            }
            Block(0, 0, 40, 14, y1);
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(new Vector3(0, y1, 0), Quaternion.identity, Vector3.one);
            mb.GableRoof(Vector3.zero, 40f, 14f, 4.5f, Atlas.Roof(4), Atlas.WallSolid(7), 0.6f);
            mb.M = saved;
            foreach (float x in new[] { -22f, 22f })
            {
                Block(x, 0, 8, 8, 16);
                mb.Pyramid(new Vector3(x, 16f, 0), 8.6f, 8.6f, 5.5f, Atlas.SolidUV(Pal.Deodar));
            }
            for (int g = -1; g <= 1; g++)   // front gables
            {
                mb.M = saved * Matrix4x4.TRS(new Vector3(g * 12f, y1, 7.6f), Quaternion.Euler(0, 90, 0), Vector3.one);
                mb.GableRoof(Vector3.zero, 4f, 5f, 3.2f, Atlas.Roof(4), Atlas.WallSolid(7), 0.3f);
                mb.M = saved;
            }
            for (int i = 0; i < 4; i++) mb.Cylinder(new Vector3(-4.5f + i * 3f, 0, 9.6f), 0.35f, 0.3f, 4.6f, 8, S(Pal.TempleWhite), true);
            mb.Box(new Vector3(0, 4.8f, 9.2f), new Vector3(11f, 0.4f, 3.6f), M(Atlas.MatStone));
        }

        static void BoatClub(MeshBuilder mb, float depth, System.Random rng)
        {
            Plinth(mb, new Vector3(20, 0, 14), depth, M(Atlas.MatStone));
            var st = new BuildingKit.Style { wall = 0, roof = 3, flatRoof = false, litChance = 0.7f };
            Vector2 fl = new Vector2(-8, 5), fr = new Vector2(8, 5), br = new Vector2(8, -5), bl = new Vector2(-8, -5);
            BuildingKit.FacadeWall(mb, fl, fr, 0, 7, 0, Vector3.forward, st, rng, true);
            BuildingKit.FacadeWall(mb, fr, br, 0, 7, 0, Vector3.right, st, rng, false);
            BuildingKit.FacadeWall(mb, br, bl, 0, 7, 0, Vector3.back, st, rng, false);
            BuildingKit.FacadeWall(mb, bl, fl, 0, 7, 0, Vector3.left, st, rng, false);
            Vector2 beam = S(Pal.WoodDark);
            for (int i = 0; i <= 8; i++) mb.Box(new Vector3(-8 + i * 2f, 3.5f, 5.06f), new Vector3(0.18f, 7f, 0.08f), beam);   // Tudor timbering
            mb.Box(new Vector3(0, 3.5f, 5.06f), new Vector3(16f, 0.18f, 0.08f), beam);
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(new Vector3(0, 7f, 0), Quaternion.identity, Vector3.one);
            mb.GableRoof(Vector3.zero, 16f, 10f, 5f, Atlas.Roof(3), Atlas.WallSolid(0), 0.6f);
            mb.M = saved;
            mb.Box(new Vector3(0, 0.1f, 8f), new Vector3(16f, 0.2f, 6f), M(Atlas.MatWood));      // veranda deck
            for (int i = 0; i < 5; i++) mb.Cylinder(new Vector3(-7f + i * 3.5f, 0.2f, 10.6f), 0.12f, 0.12f, 1f, 5, S(Pal.SailWhite), true);
            Flag(mb, new Vector3(9f, 0, 9f), 9f, Pal.BoatBlue);
        }

        static void Bazaar(MeshBuilder mb, float depth, System.Random rng)
        {
            Plinth(mb, new Vector3(16, 0, 26), depth, M(Atlas.MatStone));
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 6; i++)
                {
                    float z = -10f + i * 4f, x = side * 5f;
                    int wall = rng.Next(Atlas.WallColors);
                    Vector2 wc = Atlas.WallSolid(wall), aw = S(new[] { Pal.Awning, Pal.Canopy, Pal.BoatYellow, Pal.BoatGreen, Pal.SareePink }[rng.Next(5)]);
                    mb.Box(new Vector3(x + side * 1.7f, 1.6f, z), new Vector3(0.3f, 3.2f, 3.8f), wc);   // back wall
                    mb.Box(new Vector3(x, 1.6f, z - 1.9f), new Vector3(3.6f, 3.2f, 0.2f), wc);
                    mb.Box(new Vector3(x - side * 0.6f, 0.5f, z), new Vector3(1.2f, 1f, 3.2f), S(Pal.WoodLight));   // counter
                    for (int k = 0; k < 4; k++)
                        mb.Box(new Vector3(x - side * 0.6f, 1.15f, z - 1.2f + k * 0.8f), new Vector3(0.5f, 0.3f, 0.5f),
                               S(new[] { Pal.SareeOrange, Pal.JacketRed, Pal.JacketBlue, Pal.CapWool, Pal.LampBulb }[rng.Next(5)]));
                    mb.QuadDouble(new Vector3(x + side * 1.8f, 3.2f, z - 2f), new Vector3(x + side * 1.8f, 3.2f, z + 2f),
                                  new Vector3(x - side * 1.6f, 2.6f, z + 2f), new Vector3(x - side * 1.6f, 2.6f, z - 2f), new Rect(aw.x, aw.y, 0, 0), Vector3.up);
                    mb.Box(new Vector3(x + side * 1.7f, 3.5f, z), new Vector3(0.2f, 0.5f, 3.4f), Atlas.WallSolid(rng.Next(Atlas.WallColors)));
                }
            for (int i = 0; i < 14; i++)   // festive string lights over the lane
                mb.Sphere(new Vector3(Mathf.Sin(i) * 0.3f, 3.4f - Mathf.Abs(Mathf.Sin(i * 0.45f)) * 0.3f, -12f + i * 1.8f), Vector3.one * 0.09f, 5, 3, S(Pal.LampBulb));
        }

        static void Viewpoint(MeshBuilder mb, float depth, float w, float d)
        {
            Plinth(mb, new Vector3(w, 0, d), depth, M(Atlas.MatStone));
            var pts = new List<Vector3>();
            for (float x = -w / 2; x <= w / 2 + 0.01f; x += 2f) pts.Add(new Vector3(x, 0, d / 2));
            var col = new MeshBuilder();
            PropKit.Railing(mb, col, pts);
            pts.Clear();
            for (float z = d / 2; z >= -d / 2 - 0.01f; z -= 2f) pts.Add(new Vector3(w / 2, 0, z));
            PropKit.Railing(mb, col, pts);
            pts.Clear();
            for (float z = d / 2; z >= -d / 2 - 0.01f; z -= 2f) pts.Add(new Vector3(-w / 2, 0, z));
            PropKit.Railing(mb, col, pts);
        }

        static void SnowViewStation(MeshBuilder mb, float depth)
        {
            Viewpoint(mb, depth, 18, 12);
            var st = new BuildingKit.Style { wall = 1, roof = 0, flatRoof = true, litChance = 0.8f };
            var col = new MeshBuilder();
            var saved = mb.M;
            BuildingKit.House(mb, col, new Vector2(-4f, -2.5f), 8f, 5f, 0f, 2, 0f, 0f, st, new System.Random(5));
            mb.M = saved;
            mb.Box(new Vector3(-4f, 6.6f, 0.1f), new Vector3(5f, 0.9f, 0.15f), S(Pal.Saffron));      // "ROPEWAY" board
            mb.Limb(new Vector3(5f, 0, 3f), new Vector3(5f, 1.3f, 3f), 0.06f, 0.05f, 5, S(Pal.IronBlack));   // telescope
            mb.Limb(new Vector3(4.7f, 1.35f, 2.7f), new Vector3(5.4f, 1.6f, 3.6f), 0.09f, 0.06f, 6, S(Pal.IronBlack));
        }

        static void EcoCaves(MeshBuilder mb, float depth, System.Random rng)
        {
            Plinth(mb, new Vector3(24, 0, 18), Mathf.Min(depth, 2f), M(Atlas.MatDarkStone));
            for (int i = 0; i < 7; i++)
            {
                float x = -9f + i * 3f + (float)rng.NextDouble(), z = (float)(rng.NextDouble() - 0.5) * 8f;
                mb.Sphere(new Vector3(x, 0.4f, z), new Vector3(2.8f, 2.2f, 2.4f) * (0.8f + (float)rng.NextDouble() * 0.5f), 7, 4, M(Atlas.MatDarkStone), 0f, 90f);
                mb.Quad(new Vector3(x - 0.6f, 0.2f, z + 2.05f), new Vector3(x + 0.6f, 0.2f, z + 2.05f), new Vector3(x + 0.6f, 1.6f, z + 2.05f), new Vector3(x - 0.6f, 1.6f, z + 2.05f),
                        S(Pal.IronBlack), Vector3.forward);
            }
        }

        static void ZooGate(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(12, 0, 6), Mathf.Min(depth, 2f), M(Atlas.MatStone));
            foreach (float x in new[] { -3f, 3f }) mb.Box(new Vector3(x, 2.5f, 0), new Vector3(1f, 5f, 1f), M(Atlas.MatStone));
            mb.Box(new Vector3(0, 5.2f, 0), new Vector3(8f, 1f, 0.8f), S(Pal.JacketGreen));
            mb.Box(new Vector3(0, 5.2f, 0.42f), new Vector3(6f, 0.6f, 0.05f), S(Pal.BoatYellow));
            for (float x = -6f; x <= 6f; x += 1.5f)
                if (Mathf.Abs(x) > 3.5f) mb.Box(new Vector3(x, 1f, 0), new Vector3(0.08f, 2f, 0.08f), S(Pal.IronGreen));
        }

        static void FlatsPavilion(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(30, 0, 9), Mathf.Min(depth, 2.5f), M(Atlas.MatConcrete));
            for (int t = 0; t < 4; t++) mb.Box(new Vector3(0, 0.25f + t * 0.5f, -t * 1.2f), new Vector3(28f, 0.5f, 1.2f), M(Atlas.MatConcrete));
            for (int i = 0; i < 6; i++) mb.Cylinder(new Vector3(-12.5f + i * 5f, 0, 2.5f), 0.15f, 0.15f, 5f, 6, S(Pal.IronGreen), true);
            mb.Box(new Vector3(0, 5.1f, -1f), new Vector3(29f, 0.2f, 8f), Atlas.SolidUV(Pal.BoatGreen));
            Flag(mb, new Vector3(-15f, 0, 4f), 10f, Pal.SareeOrange);
            Flag(mb, new Vector3(15f, 0, 4f), 10f, Pal.JacketGreen);
        }

        static void DorothySeat(MeshBuilder mb, float depth)
        {
            Viewpoint(mb, depth, 10, 8);
            mb.Box(new Vector3(0, 0.25f, -2.5f), new Vector3(3f, 0.5f, 0.9f), M(Atlas.MatStone));
            mb.Box(new Vector3(0, 0.8f, -2.95f), new Vector3(3f, 1.1f, 0.25f), M(Atlas.MatStone));
            mb.Box(new Vector3(0, 0.9f, -2.8f), new Vector3(0.8f, 0.5f, 0.05f), S(Pal.Gold));   // plaque
        }


        // ------------------------------------------------------------------ viewpoints (2025-26 walking videos)
        static Vector2 WoodBrown => S(Pal.WoodDark);

        /// <summary>Brown post-and-rail fence, as along the Land's End and Tiffin Top edges.</summary>
        static void WoodFence(MeshBuilder mb, Vector3 a, Vector3 b, float h = 1.1f)
        {
            float len = Vector3.Distance(a, b);
            if (len < 0.1f) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
            var rot = Quaternion.LookRotation(b - a);
            for (int i = 0; i <= n; i++)
                mb.Box(Vector3.Lerp(a, b, i / (float)n) + Vector3.up * h * 0.5f, new Vector3(0.12f, h, 0.12f), rot, WoodBrown);
            foreach (float y in new[] { h * 0.45f, h * 0.92f })
                mb.Box((a + b) * 0.5f + Vector3.up * y, new Vector3(0.07f, 0.1f, len), rot, S(Pal.WoodLight));
        }

        /// <summary>Coin binoculars on a post, aimed at the view.</summary>
        static void Telescope(MeshBuilder mb, Vector3 p)
        {
            Vector2 metal = S(Pal.IronGreen), dark = S(Pal.IronBlack);
            mb.Cylinder(p, 0.09f, 0.07f, 1.05f, 6, metal, true);
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(p + Vector3.up * 1.2f, Quaternion.Euler(-8f, 0f, 0f), Vector3.one);
            mb.Box(Vector3.zero, new Vector3(0.42f, 0.26f, 0.32f), metal);
            foreach (float x in new[] { -0.1f, 0.1f }) mb.Limb(new Vector3(x, 0.02f, 0.12f), new Vector3(x, 0.03f, 0.4f), 0.055f, 0.06f, 8, dark);
            mb.Box(new Vector3(0f, -0.02f, -0.2f), new Vector3(0.3f, 0.12f, 0.08f), dark);
            mb.M = saved;
        }

        static void PlasticChair(MeshBuilder mb, Vector3 p, float yaw, int pal)
        {
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            Vector2 c = S(pal);
            mb.Box(new Vector3(0f, 0.45f, 0f), new Vector3(0.44f, 0.05f, 0.42f), c);
            mb.Box(new Vector3(0f, 0.72f, -0.2f), new Vector3(0.44f, 0.5f, 0.04f), c);
            foreach (float x in new[] { -0.19f, 0.19f })
                foreach (float z in new[] { -0.18f, 0.18f }) mb.Box(new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.45f, 0.04f), c);
            mb.M = saved;
        }

        static void CafeTable(MeshBuilder mb, Vector3 p, int chairPal, System.Random rng)
        {
            mb.Box(p + Vector3.up * 0.74f, new Vector3(0.9f, 0.04f, 0.9f), S(Pal.ShirtWhite));
            mb.Box(p + Vector3.up * 0.37f, new Vector3(0.08f, 0.74f, 0.08f), S(Pal.Concrete));
            for (int k = 0; k < 4; k++)
            {
                float a = k * 90f + (float)rng.NextDouble() * 20f;
                Vector3 off = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, -0.75f);
                PlasticChair(mb, p + off, a, chairPal);
            }
        }

        /// <summary>A tea and Maggi stall: poles, a sloping tarp, a counter of packets and a kettle.</summary>
        static void TarpStall(MeshBuilder mb, Vector3 p, float yaw, int tarp, System.Random rng)
        {
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            Vector2 pole = WoodBrown, tp = S(tarp);
            foreach (float x in new[] { -1.6f, 1.6f })
                foreach (float z in new[] { -1.2f, 1.2f })
                    mb.Box(new Vector3(x, z > 0 ? 1.1f : 1.3f, z), new Vector3(0.07f, z > 0 ? 2.2f : 2.6f, 0.07f), pole);
            mb.QuadDouble(new Vector3(-1.8f, 2.2f, 1.45f), new Vector3(1.8f, 2.2f, 1.45f), new Vector3(1.8f, 2.6f, -1.3f), new Vector3(-1.8f, 2.6f, -1.3f), new Rect(tp.x, tp.y, 0f, 0f), Vector3.up);
            mb.Box(new Vector3(0f, 0.45f, -0.6f), new Vector3(2.6f, 0.9f, 0.7f), S(Pal.WoodLight));
            mb.Cylinder(new Vector3(-0.8f, 0.9f, -0.6f), 0.17f, 0.15f, 0.25f, 8, S(Pal.Concrete), true);
            int[] packets = { Pal.JacketRed, Pal.BoatYellow, Pal.JacketBlue, Pal.SareeOrange, Pal.JacketGreen };
            for (int k = 0; k < 7; k++)
                mb.Box(new Vector3(-0.35f + k * 0.2f, 1.0f, -0.75f), new Vector3(0.15f, 0.2f, 0.1f), S(packets[rng.Next(packets.Length)]));
            for (int k = 0; k < 5; k++)   // snack strips hanging from the front pole line
                mb.Box(new Vector3(-1.2f + k * 0.6f, 1.75f, 1.2f), new Vector3(0.12f, 0.6f, 0.02f), S(packets[rng.Next(packets.Length)]));
            mb.M = saved;
        }

        /// <summary>A child's electric jeep for hire, as parked round the Snow View plaza.</summary>
        static void ToyJeep(MeshBuilder mb, Vector3 p, float yaw, int pal)
        {
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            Vector2 body = S(pal), black = S(Pal.IronBlack);
            mb.Box(new Vector3(0f, 0.38f, 0f), new Vector3(0.72f, 0.32f, 1.15f), body);
            mb.Box(new Vector3(0f, 0.6f, 0.38f), new Vector3(0.7f, 0.12f, 0.36f), body);           // bonnet
            mb.Box(new Vector3(0f, 0.6f, -0.2f), new Vector3(0.4f, 0.14f, 0.3f), S(Pal.JacketRed)); // seat
            foreach (float x in new[] { -0.33f, 0.33f })
            {
                mb.Box(new Vector3(x, 0.85f, -0.05f), new Vector3(0.04f, 0.55f, 0.04f), black);       // roll bar
                foreach (float z in new[] { -0.4f, 0.4f })
                    mb.Limb(new Vector3(x * 1.2f, 0.2f, z), new Vector3(x * 1.45f, 0.2f, z), 0.19f, 0.19f, 10, black);
            }
            mb.Box(new Vector3(0f, 1.12f, -0.05f), new Vector3(0.7f, 0.04f, 0.04f), black);
            mb.M = saved;
        }

        static void Boulder(MeshBuilder mb, Vector3 p, float r, System.Random rng)
        {
            mb.Sphere(p, new Vector3(r * (1f + (float)rng.NextDouble() * 0.4f), r * (0.55f + (float)rng.NextDouble() * 0.25f), r), 7, 4, M(Atlas.MatDarkStone));
        }

        /// <summary>A hilltop shop: open front with a counter, goods hanging on display, a tin roof and an awning.</summary>
        static void HillShop(MeshBuilder mb, Vector3 p, float yaw, float w, System.Random rng)
        {
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            Vector2 wc = Atlas.WallSolid(rng.Next(Atlas.WallColors));
            int[] tins = { Pal.TempleRed, Pal.BoatGreen, Pal.BoatBlue, Pal.TempleRed };
            int[] goods = { Pal.JacketRed, Pal.SareePink, Pal.CapWool, Pal.JacketBlue, Pal.SareeOrange, Pal.Shawl, Pal.BoatYellow, Pal.ShirtWhite };
            mb.Box(new Vector3(0f, 1.6f, -2.1f), new Vector3(w, 3.2f, 0.25f), wc);
            foreach (float sd in new[] { -1f, 1f }) mb.Box(new Vector3(sd * w * 0.5f, 1.6f, -0.7f), new Vector3(0.2f, 3.2f, 3f), wc);
            mb.Box(new Vector3(0f, 3.28f, -0.6f), new Vector3(w + 0.3f, 0.1f, 3.6f), S(tins[rng.Next(tins.Length)]));
            mb.Box(new Vector3(0f, 2.95f, 0.82f), new Vector3(w - 0.2f, 0.5f, 0.06f), S(goods[rng.Next(goods.Length)]));     // the board
            mb.QuadDouble(new Vector3(-w * 0.5f, 2.35f, 1.7f), new Vector3(w * 0.5f, 2.35f, 1.7f), new Vector3(w * 0.5f, 2.7f, 0.85f), new Vector3(-w * 0.5f, 2.7f, 0.85f),
                          new Rect(S(new[] { Pal.Awning, Pal.Canopy, Pal.BoatBlue }[rng.Next(3)]), Vector2.zero), Vector3.up);
            mb.Box(new Vector3(0f, 0.45f, 0.4f), new Vector3(w - 0.6f, 0.9f, 0.5f), S(Pal.WoodLight));                          // counter
            for (float x = -w * 0.5f + 0.4f; x < w * 0.5f - 0.3f; x += 0.42f)                                                // shawls and jackets on show
                mb.Box(new Vector3(x, 1.55f + (float)rng.NextDouble() * 0.3f, 0.75f), new Vector3(0.36f, 0.75f, 0.04f), S(goods[rng.Next(goods.Length)]));
            for (float x = -w * 0.5f + 0.4f; x < w * 0.5f - 0.3f; x += 0.5f)
                mb.Box(new Vector3(x, 1.3f, -1.9f), new Vector3(0.42f, 1.6f, 0.2f), S(goods[rng.Next(goods.Length)]));      // shelves at the back
            mb.M = saved;
        }

        /// <summary>Snow View top (2025 video): a paved plaza ringed by woollen and souvenir shops and a cafe, kids' jeeps for hire,
        /// the ropeway's upper station to one side, and a gallery wall painted pale blue with coin binoculars facing the snow range.</summary>
        static void SnowViewTop(MeshBuilder mb, Site s, System.Random rng)
        {
            Plinth(mb, new Vector3(34f, 0f, 26f), s.depth, M(Atlas.MatConcrete));
            Vector2 terracotta = S(Pal.TempleRed);   // grey pavers inside a terracotta border, as in the video
            mb.Box(new Vector3(0f, 0.01f, -12.6f), new Vector3(34f, 0.02f, 0.8f), terracotta);
            mb.Box(new Vector3(0f, 0.01f, 12.1f), new Vector3(34f, 0.02f, 0.8f), terracotta);
            foreach (float bx in new[] { -16.6f, 16.6f }) mb.Box(new Vector3(bx, 0.01f, 0f), new Vector3(0.8f, 0.02f, 26f), terracotta);
            for (int k = 0; k < 40; k++)   // a few odd-coloured pavers
                mb.Box(new Vector3(-15f + (float)rng.NextDouble() * 30f, 0.008f, -11f + (float)rng.NextDouble() * 22f), new Vector3(0.6f, 0.016f, 0.6f), S(Pal.StoneGrey));
            // the gallery along the view side
            Vector2 paleBlue = S(Pal.JacketBlue), white = S(Pal.TempleWhite);
            mb.Box(new Vector3(0f, 0.45f, 12.7f), new Vector3(34f, 0.9f, 0.4f), paleBlue);
            mb.Box(new Vector3(0f, 0.93f, 12.7f), new Vector3(34.2f, 0.08f, 0.5f), white);
            for (float x = -16.5f; x <= 16.6f; x += 1.5f)   // the white stone pattern painted on it
                mb.Box(new Vector3(x + (float)rng.NextDouble() * 0.3f, 0.3f + (float)rng.NextDouble() * 0.4f, 12.49f), new Vector3(0.5f, 0.06f, 0.02f), white);
            var rail = new List<Vector3>();
            for (float x = -17f; x <= 17.1f; x += 2f) rail.Add(new Vector3(x, 0.95f, 12.7f));
            PropKit.Railing(mb, new MeshBuilder(), rail);
            foreach (float x in new[] { -9f, -3f, 3f, 9f }) Telescope(mb, new Vector3(x, 0f, 11.6f));
            // shops along the back, cafe on the left
            float sx = -15f;
            while (sx < 8f)
            {
                float w = 3.2f + (float)rng.NextDouble() * 1.2f;
                HillShop(mb, new Vector3(sx + w * 0.5f, 0f, -10.6f), 0f, w, rng);
                sx += w + 0.05f;
            }
            HillShop(mb, new Vector3(-14.8f, 0f, 1f), 90f, 6f, rng);
            for (int k = 0; k < 4; k++) CafeTable(mb, new Vector3(-10.5f + (k % 2) * 2.4f, 0f, -1.5f + (k / 2) * 2.6f), k % 2 == 0 ? Pal.JacketBlue : Pal.JacketRed, rng);
            if (WorldBuilder.LowPolyRidesAndVehicles)   // kids' jeeps for hire (off until the user's own models arrive)
            {
                ToyJeep(mb, new Vector3(-2.5f, 0f, 3.5f), 20f, Pal.ShirtWhite);
                ToyJeep(mb, new Vector3(-0.6f, 0f, 4.2f), -10f, Pal.JacketRed);
                ToyJeep(mb, new Vector3(1.4f, 0f, 3.1f), 35f, Pal.BoatBlue);
            }
            // the ropeway's upper station, set back on the right
            var st = new BuildingKit.Style { wall = 1, roof = 0, flatRoof = true, litChance = 0.8f };
            var col = new MeshBuilder();
            var saved = mb.M;
            BuildingKit.House(mb, col, new Vector2(13f, -4f), 7f, 9f, -90f, 2, 0f, 0f, st, new System.Random(5));
            mb.M = saved;
            mb.Box(new Vector3(9.4f, 6.4f, -4f), new Vector3(0.15f, 0.9f, 5f), S(Pal.Saffron));   // "ROPEWAY" board facing the plaza
        }

        /// <summary>Naina Peak (2026 video): a bare rocky top in dry grass among deodars; people sit on the rocks at the edge over
        /// the lake. A short fence, a tarp tea stall, boulders - nothing built.</summary>
        static void NainaPeakTop(MeshBuilder mb, Site s, System.Random rng)
        {
            for (int k = 0; k < 9; k++)
            {
                float a = (k / 9f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f, r = 5f + (float)rng.NextDouble() * 3f;
                Boulder(mb, new Vector3(Mathf.Sin(a) * r, -0.1f, Mathf.Cos(a) * r), 0.5f + (float)rng.NextDouble() * 0.6f, rng);
            }
            WoodFence(mb, new Vector3(-4f, 0f, 6.2f), new Vector3(3.5f, 0f, 6.6f));
            TarpStall(mb, new Vector3(-1f, 0f, -5.5f), 0f, Pal.BoatBlue, rng);
            PlasticChair(mb, new Vector3(1.5f, 0f, -3.2f), 10f, Pal.JacketRed);
            PlasticChair(mb, new Vector3(2.3f, 0f, -3.4f), -15f, Pal.JacketRed);
        }

        /// <summary>Tiffin Top (2025 video): the stone-paved trail ends at a dhaba under a tin roof with red plastic chairs, a yellow
        /// umbrella at the edge, brown wooden railings, and Dorothy's Seat - a stone bench with a plaque - looking over the lake.</summary>
        static void TiffinTop(MeshBuilder mb, Site s, System.Random rng)
        {
            // Dorothy's Seat at the front
            mb.Box(new Vector3(0f, 0.25f, 3.2f), new Vector3(3f, 0.5f, 0.9f), M(Atlas.MatStone));
            mb.Box(new Vector3(0f, 0.8f, 2.75f), new Vector3(3.2f, 1.1f, 0.3f), M(Atlas.MatStone));
            mb.Box(new Vector3(0f, 0.95f, 2.92f), new Vector3(0.9f, 0.5f, 0.05f), S(Pal.Gold));
            WoodFence(mb, new Vector3(-6f, 0f, 5.4f), new Vector3(6f, 0f, 5.4f));
            WoodFence(mb, new Vector3(6f, 0f, 5.4f), new Vector3(6.5f, 0f, 0f));
            WoodFence(mb, new Vector3(-6f, 0f, 5.4f), new Vector3(-6.5f, 0f, 0f));
            // the dhaba
            Vector2 tin = S(Pal.Concrete), post = WoodBrown;
            foreach (float x in new[] { -3f, 3f })
                foreach (float z in new[] { -6.5f, -2.5f }) mb.Box(new Vector3(x, 1.2f, z), new Vector3(0.12f, 2.4f, 0.12f), post);
            mb.Box(new Vector3(0f, 2.45f, -4.5f), new Vector3(6.8f, 0.08f, 4.8f), tin);
            mb.Box(new Vector3(0f, 1.2f, -6.6f), new Vector3(6f, 2.4f, 0.15f), S(Pal.WoodLight));
            mb.Box(new Vector3(-1.5f, 0.5f, -5.8f), new Vector3(2.5f, 1f, 0.7f), S(Pal.WoodLight));
            mb.Cylinder(new Vector3(-2.2f, 1f, -5.8f), 0.17f, 0.15f, 0.25f, 8, S(Pal.Concrete), true);
            CafeTable(mb, new Vector3(-1.2f, 0f, -3.6f), Pal.JacketRed, rng);
            CafeTable(mb, new Vector3(1.6f, 0f, -3.8f), Pal.JacketRed, rng);
            // the yellow umbrella table at the edge
            CafeTable(mb, new Vector3(4f, 0f, 3f), Pal.JacketRed, rng);
            mb.Cylinder(new Vector3(4f, 0.74f, 3f), 0.03f, 0.03f, 1.6f, 5, S(Pal.Concrete), true);
            mb.Pyramid(new Vector3(4f, 2.1f, 3f), 2.4f, 2.4f, 0.45f, S(Pal.BoatYellow));
            for (int k = 0; k < 4; k++) Boulder(mb, new Vector3(-5f + k * 3.2f, -0.1f, 1f + (float)rng.NextDouble() * 2f), 0.5f + (float)rng.NextDouble() * 0.4f, rng);
        }

        /// <summary>Land's End (2025 video): the forest trail opens on a cliff edge fenced with brown wooden rails, a bench and a
        /// tea stall, Khurpatal's valley below.</summary>
        static void LandsEnd(MeshBuilder mb, Site s, System.Random rng)
        {
            WoodFence(mb, new Vector3(-5.5f, 0f, 4.6f), new Vector3(5.5f, 0f, 4.6f));
            WoodFence(mb, new Vector3(5.5f, 0f, 4.6f), new Vector3(6.2f, 0f, -1.5f));
            WoodFence(mb, new Vector3(-5.5f, 0f, 4.6f), new Vector3(-6.2f, 0f, -1.5f));
            TarpStall(mb, new Vector3(2.5f, 0f, -4.2f), -10f, Pal.JacketGreen, rng);
            for (int k = 0; k < 5; k++) Boulder(mb, new Vector3(-6f + k * 3f, -0.1f, -1f - (float)rng.NextDouble() * 3f), 0.45f + (float)rng.NextDouble() * 0.5f, rng);
        }

        /// <summary>Himalaya Darshan (on the Kilbury road): a concrete lookout off the road with a green iron railing, coin
        /// binoculars aimed at the snow range, and tea and Maggi stalls under tarps.</summary>
        static void HimalayaDarshan(MeshBuilder mb, Site s, System.Random rng)
        {
            Plinth(mb, new Vector3(16f, 0f, 9f), s.depth, M(Atlas.MatConcrete));
            var rail = new List<Vector3>();
            for (float x = -8f; x <= 8.01f; x += 2f) rail.Add(new Vector3(x, 0f, 4.5f));
            PropKit.Railing(mb, new MeshBuilder(), rail);
            foreach (float x in new[] { -5f, -1.5f, 2f, 5.5f }) Telescope(mb, new Vector3(x, 0f, 3.6f));
            TarpStall(mb, new Vector3(-4.5f, 0f, -3f), 0f, Pal.BoatBlue, rng);
            TarpStall(mb, new Vector3(1f, 0f, -3f), 0f, Pal.BoatRed, rng);
            for (int k = 0; k < 4; k++) PlasticChair(mb, new Vector3(-6f + k * 1.1f, 0f, -0.6f), 180f + k * 12f, k % 2 == 0 ? Pal.JacketRed : Pal.JacketBlue);
        }


        /// <summary>Snow View's "360" tower (2025 videos): a white lattice mast with a crown, a ring gondola of bronze-coloured
        /// plate with a gold rail that rides up it turning (Tower360Ride), a glass-roofed boarding deck, a ticket booth and shops
        /// round a stone-paved yard.</summary>
        static void Tower360(WorldCtx ctx, Transform parent, Vector2 p, System.Random rng)
        {
            var site = MakeSite(ctx, p, ctx.lakeCenter, 13f);
            var mb = new MeshBuilder { M = Matrix4x4.TRS(site.origin, site.rot, Vector3.one) };
            Plinth(mb, new Vector3(30f, 0f, 26f), site.depth, M(Atlas.MatStone));
            Vector2 white = S(Pal.ShirtWhite);
            const float H = 34f, half = 0.8f;
            // the mast: four legs, rings and cross bracing
            foreach (float x in new[] { -half, half })
                foreach (float z in new[] { -half, half })
                    mb.Cylinder(new Vector3(x, 0f, z), 0.09f, 0.08f, H, 6, white, true);
            for (float y = 1.5f; y < H; y += 2f)
            {
                mb.Box(new Vector3(0f, y, -half), new Vector3(half * 2f, 0.06f, 0.06f), white);
                mb.Box(new Vector3(0f, y, half), new Vector3(half * 2f, 0.06f, 0.06f), white);
                mb.Box(new Vector3(-half, y, 0f), new Vector3(0.06f, 0.06f, half * 2f), white);
                mb.Box(new Vector3(half, y, 0f), new Vector3(0.06f, 0.06f, half * 2f), white);
                foreach (float s in new[] { -1f, 1f })
                {
                    mb.Limb(new Vector3(-half, y, s * half), new Vector3(half, y + 2f, s * half), 0.03f, 0.03f, 4, white, false);
                    mb.Limb(new Vector3(s * half, y, -half), new Vector3(s * half, y + 2f, half), 0.03f, 0.03f, 4, white, false);
                }
            }
            for (int k = 0; k < 8; k++)   // the crown
            {
                float a = k * Mathf.PI / 4f;
                mb.Limb(new Vector3(0f, H, 0f), new Vector3(Mathf.Cos(a) * 1.4f, H + 1.6f, Mathf.Sin(a) * 1.4f), 0.04f, 0.03f, 4, white);
            }
            mb.Sphere(new Vector3(0f, H + 1.9f, 0f), Vector3.one * 0.3f, 6, 4, S(Pal.Gold));
            // boarding deck under a glass roof, a ticket booth, shops facing the yard
            mb.Box(new Vector3(0f, 0.3f, 0f), new Vector3(7f, 0.6f, 7f), M(Atlas.MatConcrete));    // the boarding deck round the mast
            foreach (float x in new[] { 4.2f, 9.8f })                                                // the queue shelter beside it, glass-roofed
                foreach (float z in new[] { -2.5f, 2.5f }) mb.Cylinder(new Vector3(x, 0f, z), 0.06f, 0.06f, 3f, 6, S(Pal.IronBlack), true);
            mb.Box(new Vector3(7f, 3.05f, 0f), new Vector3(6f, 0.06f, 5.4f), M(Atlas.MatGlass));
            for (float z = -2.4f; z <= 2.41f; z += 1.2f) mb.Box(new Vector3(7f, 0.5f, z), new Vector3(5.2f, 0.06f, 0.06f), S(Pal.IronBlack));   // queue rails
            mb.Box(new Vector3(-9f, 1.3f, -8f), new Vector3(3f, 2.6f, 2.4f), S(Pal.SareePink));            // ticket counter
            mb.Box(new Vector3(-9f, 1.4f, -6.78f), new Vector3(1.2f, 0.8f, 0.05f), M(Atlas.MatGlass));
            mb.Box(new Vector3(-9f, 2.75f, -8f), new Vector3(3.4f, 0.2f, 2.8f), S(Pal.BoatGreen));
            HillShop(mb, new Vector3(8f, 0f, -9.5f), 0f, 4f, rng);
            HillShop(mb, new Vector3(12.2f, 0f, -9.5f), 0f, 4f, rng);
            HillShop(mb, new Vector3(-13f, 0f, 2f), 90f, 4.4f, rng);
            var go = ctx.MeshObject("Tower 360 (Snow View)", parent, mb, ctx.mats.atlas, null, true, true);

            // the gondola: an open ring round the mast, bronze plate, gold rail, people standing at it
            var g = new MeshBuilder();
            Vector2 bronze = S(Pal.WoodDark), gold = S(Pal.Gold);
            const int seg = 24;
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                Vector3 o0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), o1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                g.Quad(o0 * 2.6f, o1 * 2.6f, o1 * 2.6f + Vector3.up * 1.15f, o0 * 2.6f + Vector3.up * 1.15f, new Rect(bronze, Vector2.zero), o0);
                g.Quad(o1 * 2.6f, o0 * 2.6f, o0 * 2.6f + Vector3.up * 1.15f, o1 * 2.6f + Vector3.up * 1.15f, new Rect(bronze, Vector2.zero), -o0);
                g.Quad(o0 * 1.2f, o1 * 1.2f, o1 * 2.6f, o0 * 2.6f, new Rect(S(Pal.Concrete), Vector2.zero), Vector3.up);   // the floor
                g.Limb(o0 * 2.62f + Vector3.up * 1.2f, o1 * 2.62f + Vector3.up * 1.2f, 0.05f, 0.05f, 5, gold, false);
                if (k % 3 == 0) g.Box(o0 * 2.6f + Vector3.up * 0.6f, new Vector3(0.12f, 1.25f, 0.12f), gold);
            }
            g.Box(new Vector3(0f, 1.6f, -2.62f), new Vector3(3f, 0.45f, 0.04f), S(Pal.JacketBlue));   // the ride's name band
            var gond = ctx.MeshObject("Tower 360 gondola", go.transform.parent, g, ctx.mats.atlas, null, true, false);
            gond.transform.SetPositionAndRotation(site.origin + Vector3.up * 0.6f, site.rot);
            gond.AddComponent<Tower360Ride>().top = H - 4f;
            Sign(ctx, "Tower 360", site, new Vector3(-6f, 0f, -11f), parent);
            ctx.Zone("Tower 360 (Snow View)", site.origin, 40f);
            ctx.landmarkSites["Tower 360"] = site.origin;
            var cam = new GameObject("ViewShot Tower 360");   // PreviewCapture
            cam.transform.SetParent(parent, false);
            cam.transform.SetPositionAndRotation(site.origin + Vector3.up * 1.7f, site.rot);
        }

        /// <summary>The lane outside the ropeway's Mallital station: stalls and small shops either side.</summary>
        static void RopewayMarket(WorldCtx ctx, Transform parent, Vector2 st, Vector2 toward, System.Random rng)
        {
            Vector2 side = new Vector2(-toward.y, toward.x);
            int[] tarps = { Pal.BoatBlue, Pal.BoatRed, Pal.BoatYellow, Pal.JacketGreen };
            var mb = new MeshBuilder();
            for (int k = 0; k < 6; k++)
            {
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector2 q = st + toward * (10f + k * 4.2f) + side * (sgn * 5.5f);
                    if (ctx.InLake(q) || WorldBuilder.NearAnyRoad(ctx, q)) continue;
                    float y = ctx.H(q);
                    var face = -side * sgn;
                    float yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
                    mb.M = Matrix4x4.TRS(new Vector3(q.x, y, q.y), Quaternion.identity, Vector3.one);
                    if ((k + (sgn > 0 ? 1 : 0)) % 2 == 0) TarpStall(mb, Vector3.zero, yaw, tarps[rng.Next(tarps.Length)], rng);
                    else HillShop(mb, Vector3.zero, yaw, 3.8f, rng);
                }
            }
            mb.M = Matrix4x4.identity;
            ctx.MeshObject("Ropeway market (Mallital)", parent, mb, ctx.mats.atlas, null, true, true);
        }


        // ------------------------------------------------------------------ the Khurpatal road (2026-11-11)
        /// <summary>Lover's Point: a railed ledge over the valley towards Khurpatal, with a bench and a tea stall.</summary>
        static void LoversPoint(MeshBuilder mb, Site s, System.Random rng)
        {
            WoodFence(mb, new Vector3(-5f, 0f, 4.5f), new Vector3(5f, 0f, 4.5f));
            WoodFence(mb, new Vector3(5f, 0f, 4.5f), new Vector3(5.6f, 0f, -0.5f));
            WoodFence(mb, new Vector3(-5f, 0f, 4.5f), new Vector3(-5.6f, 0f, -0.5f));
            mb.Box(new Vector3(-1.2f, 0.42f, 3.2f), new Vector3(2.2f, 0.08f, 0.5f), WoodBrown);   // a plank bench at the rail
            foreach (float x in new[] { -2.1f, -0.3f }) mb.Box(new Vector3(x, 0.2f, 3.2f), new Vector3(0.12f, 0.4f, 0.4f), WoodBrown);
            TarpStall(mb, new Vector3(2.5f, 0f, -3.8f), 0f, Pal.BoatRed, rng);
            for (int k = 0; k < 4; k++) Boulder(mb, new Vector3(-5f + k * 3.2f, -0.1f, -1.5f - (float)rng.NextDouble() * 2f), 0.4f + (float)rng.NextDouble() * 0.4f, rng);
        }

        /// <summary>A lookout over Khurpatal: concrete edge with an iron railing, binoculars, stalls under tarps.</summary>
        static void KhurpatalLookout(MeshBuilder mb, Site s, System.Random rng)
        {
            Plinth(mb, new Vector3(14f, 0f, 8f), s.depth, M(Atlas.MatConcrete));
            var rail = new List<Vector3>();
            for (float x = -7f; x <= 7.01f; x += 2f) rail.Add(new Vector3(x, 0f, 4f));
            PropKit.Railing(mb, new MeshBuilder(), rail);
            foreach (float x in new[] { -3f, 1.5f }) Telescope(mb, new Vector3(x, 0f, 3.2f));
            TarpStall(mb, new Vector3(-3.5f, 0f, -2.6f), 0f, Pal.BoatBlue, rng);
            TarpStall(mb, new Vector3(2.5f, 0f, -2.6f), 0f, Pal.JacketGreen, rng);
        }

        /// <summary>Barapathar: the pony stand where rides to Tiffin Top and Land's End start - a beaten-earth yard, a tethering
        /// rail, a rate board and tea stalls. (No ponies until the user's own models arrive.)</summary>
        static void PonyStand(MeshBuilder mb, Site s, System.Random rng)
        {
            Plinth(mb, new Vector3(22f, 0f, 14f), Mathf.Min(s.depth, 1.5f), S(Pal.ShoeBrown));
            for (float x = -8f; x <= 8.01f; x += 2f) mb.Box(new Vector3(x, 0.55f, -5f), new Vector3(0.1f, 1.1f, 0.1f), WoodBrown);
            mb.Box(new Vector3(0f, 1.0f, -5f), new Vector3(16.2f, 0.08f, 0.08f), S(Pal.WoodLight));   // the tethering rail
            mb.Box(new Vector3(-9f, 1.8f, 4f), new Vector3(2.6f, 1.6f, 0.08f), S(Pal.BoatYellow));       // the rate board
            foreach (float x in new[] { -10.2f, -7.8f }) mb.Box(new Vector3(x, 1.2f, 4.05f), new Vector3(0.1f, 2.4f, 0.1f), WoodBrown);
            TarpStall(mb, new Vector3(5f, 0f, 3.5f), 180f, Pal.BoatBlue, rng);
            TarpStall(mb, new Vector3(9f, 0f, 3.5f), 180f, Pal.BoatRed, rng);
        }

        /// <summary>Shops and stalls along the road nearest a lake point (Sariyatal, Khurpatal), facing the road.</summary>
        static void LakeMarket(WorldCtx ctx, Transform parent, string name, Vector2 p, int count, System.Random rng)
        {
            if (!WorldBuilder.NearestMotorRoad(ctx, p, 250f, out var rp, out var tan)) return;
            var mb = new MeshBuilder();
            Vector2 n = new Vector2(-tan.y, tan.x);
            if (Vector2.Dot(p - rp, n) > 0f) n = -n;   // the shops stand on the side away from the water
            int[] tarps = { Pal.BoatBlue, Pal.BoatRed, Pal.BoatYellow, Pal.JacketGreen };
            int placed = 0;
            for (int k = -count; k <= count && placed < count; k++)
            {
                Vector2 q = rp + tan * (k * 4.4f) + n * 6.5f;
                if (ctx.InLake(q) || WorldBuilder.NearAnyRoad(ctx, q)) continue;
                float yaw = Mathf.Atan2(-n.x, -n.y) * Mathf.Rad2Deg;
                mb.M = Matrix4x4.TRS(new Vector3(q.x, ctx.H(q), q.y), Quaternion.identity, Vector3.one);
                if (k % 2 == 0) HillShop(mb, Vector3.zero, yaw, 4f, rng);
                else TarpStall(mb, Vector3.zero, yaw, tarps[rng.Next(tarps.Length)], rng);
                placed++;
            }
            mb.M = Matrix4x4.identity;
            ctx.MeshObject(name + " shops", parent, mb, ctx.mats.atlas, null, true, true);
            ctx.Zone(name, new Vector3(p.x, ctx.H(p), p.y), 120f);
            var cam = new GameObject("ViewShot " + name);
            cam.transform.SetParent(parent, false);
            Vector2 eye = rp + tan * (count * 4.4f + 8f) + n * 1.5f;
            Vector3 e3 = new Vector3(eye.x, ctx.H(eye) + 1.7f, eye.y);
            cam.transform.SetPositionAndRotation(e3, Quaternion.LookRotation(new Vector3(p.x, ctx.H(p) + 1f, p.y) - e3));
        }

        // ------------------------------------------------------------------ entry
        public static void Build(WorldCtx ctx)
        {
            var parent = new GameObject("Landmarks").transform;
            parent.SetParent(ctx.worldRoot, false);
            var rng = new System.Random(77);
            Vector2 lc = ctx.lakeCenter;

            void Make(string poiName, string title, float radius, System.Action<MeshBuilder, Site> build, float zoneR, Vector2? faceOverride = null, Vector3? signLocal = null, float? bearing = null)
            {
                var poi = ctx.Poi(poiName);
                if (!poi.HasValue) { Debug.LogWarning("[Nainital] POI not found: " + poiName); return; }
                // a lookout faces what it looks at (compass bearing), everything else faces the lake
                Vector2 face = bearing.HasValue ? poi.Value + new Vector2(Mathf.Sin(bearing.Value * Mathf.Deg2Rad), Mathf.Cos(bearing.Value * Mathf.Deg2Rad)) * 100f : faceOverride ?? lc;
                var site = MakeSite(ctx, poi.Value, face, radius);
                var mb = new MeshBuilder { M = Matrix4x4.TRS(site.origin, site.rot, Vector3.one) };
                build(mb, site);
                Emit(ctx, title, site, mb, parent);
                Sign(ctx, title, site, signLocal ?? new Vector3(radius * 0.45f, 0, radius * 0.75f), parent);
                ctx.Zone(title, site.origin, zoneR);
                ctx.landmarkSites[title] = site.origin;
                var cam = new GameObject("ViewShot " + title);   // PreviewCapture: the site, and what you see from it
                cam.transform.SetParent(parent, false);
                cam.transform.SetPositionAndRotation(site.origin + Vector3.up * 1.7f, site.rot);
            }

            Make("Naina Devi", "Naina Devi Mandir", 12f, (mb, s) => NainaDevi(mb, s.depth), 45f, null, new Vector3(5f, 0, 10f));
            // G.O.A.N. models when imported (on a stone plinth), else the code-built versions
            Make("Gurudwara", "Gurudwara Sri Guru Singh Sabha", 10f, (mb, s) =>
            {
                if (WorldBuilder.PlaceGoanLandmark(ctx, "Proto_Gurudwara", s.origin, s.rot, parent, out var sz))
                    Plinth(mb, new Vector3(sz.x + 3f, 0, sz.z + 3f), s.depth, M(Atlas.MatMarble));
                else Gurudwara(mb, s.depth);
            }, 30f);
            Make("Church", "St. Francis Church", 15f, (mb, s) =>
            {
                if (WorldBuilder.PlaceGoanLandmark(ctx, "Proto_Church", s.origin, s.rot, parent, out var sz))
                    Plinth(mb, new Vector3(sz.x + 3f, 0, sz.z + 3f), s.depth, M(Atlas.MatStone));
                else Church(mb, s.depth);
            }, 30f, null, new Vector3(5f, 0, 14f));
            Make("Raj Bhavan", "Raj Bhavan", 30f, (mb, s) => RajBhavan(mb, s.depth, rng), 120f, null, new Vector3(10f, 0, 15f));
            Make("Bada Bazar", "Bada Bazar", 14f, (mb, s) => Bazaar(mb, s.depth, rng), 70f, null, new Vector3(0, 0, 14f));
            Make("Snow View", "Snow View", 17f, (mb, s) => SnowViewTop(mb, s, rng), 90f, null, new Vector3(-6f, 0, 9f), 25f);
            if (ctx.landmarkSites.TryGetValue("Snow View", out var svTop))   // the ropeway arrives at its station, beside the plaza
            {
                var svm = GameObject.Find("ViewShot Snow View");
                var svRot = svm ? Quaternion.Euler(0f, svm.transform.eulerAngles.y, 0f) : Quaternion.identity;
                ctx.landmarkSites["Snow View ropeway"] = svTop + svRot * new Vector3(13f, 0f, -4f);
            }
            Make("Naina Peak summit", "Naini Peak (China Peak)", 8f, (mb, s) => NainaPeakTop(mb, s, rng), 80f, null, new Vector3(-4f, 0, 3f));
            Make("Tiffin Top", "Tiffin Top (Dorothy's Seat)", 7f, (mb, s) => TiffinTop(mb, s, rng), 60f, null, new Vector3(3f, 0, 2f));
            Make("Land's End", "Land's End", 7f, (mb, s) => LandsEnd(mb, s, rng), 60f, null, new Vector3(-3f, 0, 2f), 244f);
            Make("Himalaya Darshan", "Himalaya Darshan", 9f, (mb, s) => HimalayaDarshan(mb, s, rng), 60f, null, new Vector3(-6.5f, 0, 2.5f), 25f);
            Make("Eco Cave Garden", "Eco Cave Garden", 13f, (mb, s) => EcoCaves(mb, s.depth, rng), 60f, null, new Vector3(0, 0, 9f));
            Make("Nainital Zoo", "Nainital Zoo", 7f, (mb, s) => ZooGate(mb, s.depth), 130f, null, new Vector3(5f, 0, 2f));
            Make("Nainital Stadium", "The Flats", 16f, (mb, s) => { }, 110f, null, new Vector3(0, 0, 6f));   // the ground itself: WorldBuilder.Flats.cs

            // Boat House Club sits on the Mallital shore beside the boat stand.
            Vector2 clubPos = ctx.mallitalShore + ctx.mallitalAlong * 55f;
            var clubSite = MakeSite(ctx, ctx.PushFromLake(clubPos, 16f), lc, 10f, 16f);
            var cmb = new MeshBuilder { M = Matrix4x4.TRS(clubSite.origin, clubSite.rot, Vector3.one) };
            BoatClub(cmb, clubSite.depth, rng);
            Emit(ctx, "Boat House Club", clubSite, cmb, parent);
            Sign(ctx, "Boat House Club", clubSite, new Vector3(-9f, 0, 9f), parent);
            ctx.Zone("Boat House Club", clubSite.origin, 30f);

            // the Khurpatal road
            Make("Lovers Point", "Lover's Point", 7f, (mb, s) => LoversPoint(mb, s, rng), 50f, null, new Vector3(-3f, 0, 2f), 225f);
            Make("Barapathar", "Barapathar (pony stand)", 11f, (mb, s) => PonyStand(mb, s, rng), 60f, null, new Vector3(-6f, 0, 6f));
            Make("Khurpatal Lake View Point", "Khurpatal Lake View Point", 8f, (mb, s) => KhurpatalLookout(mb, s, rng), 50f, null, new Vector3(-6f, 0, 2.5f), 222f);
            Make("Khurpatal View Point", "Khurpatal View Point", 8f, (mb, s) => KhurpatalLookout(mb, s, rng), 50f, null, new Vector3(-6f, 0, 2.5f), 205f);
            var sariya = ctx.Poi("Sariyatal");
            if (sariya.HasValue) LakeMarket(ctx, parent, "Sariyatal", sariya.Value, 5, rng);
            var khurpa = ctx.Poi("Khurpatal");
            if (khurpa.HasValue) LakeMarket(ctx, parent, "Khurpatal", khurpa.Value, 6, rng);
            var t360 = ctx.Poi("Tower 360");
            if (t360.HasValue) Tower360(ctx, parent, t360.Value, rng);
            BuildRopeway(ctx, parent);
            BuildExtras(ctx, parent, rng);
        }

        // ------------------------------------------------------------------ extra Nainital details
        public static void PrayerFlags(MeshBuilder mb, Vector3 a, Vector3 b, int count)
        {
            int[] cols = { Px.FlagBlue, Px.FlagWhite, Px.FlagRed, Px.FlagGreen, Px.FlagYellow };
            mb.Limb(a, b, 0.012f, 0.012f, 3, Atlas.ExtraUV(Px.FlagWhite), false);
            Vector3 dir = (b - a);
            Vector3 side = Vector3.Cross(Vector3.up, dir.normalized) * 0.01f;
            for (int i = 1; i < count; i++)
            {
                float t = i / (float)count;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * dir.magnitude * 0.06f);
                Vector3 along = dir.normalized * 0.16f;
                var uv = new Rect(Atlas.ExtraUV(cols[i % cols.Length]), Vector2.zero);
                mb.QuadDouble(p - along - side + Vector3.down * 0.3f, p + along - side + Vector3.down * 0.3f, p + along - side, p - along - side, uv, side);
            }
        }

        static void FlagPoles(MeshBuilder mb, Vector3 a, Vector3 b)
        {
            mb.Limb(a, a + Vector3.up * 4.5f, 0.05f, 0.04f, 4, S(Pal.WoodDark));
            mb.Limb(b, b + Vector3.up * 4.5f, 0.05f, 0.04f, 4, S(Pal.WoodDark));
            PrayerFlags(mb, a + Vector3.up * 4.4f, b + Vector3.up * 4.4f, 18);
        }

        static void TibetanMarket(MeshBuilder mb, float depth, System.Random rng)
        {
            Plinth(mb, new Vector3(30, 0, 14), Mathf.Min(depth, 1.5f), M(Atlas.MatStone));
            for (int i = 0; i < 7; i++)
            {
                float x = -12f + i * 4f;
                foreach (int side in new[] { -1, 1 })
                {
                    float z = side * 4f;
                    Vector2 aw = Atlas.ExtraUV(new[] { Px.FlagRed, Px.FlagBlue, Px.FlagYellow, Px.FlagGreen }[rng.Next(4)]);
                    mb.Box(new Vector3(x, 1.4f, z + side * 1.4f), new Vector3(3.6f, 2.8f, 0.15f), S(Pal.WoodDark));
                    mb.Box(new Vector3(x, 0.5f, z), new Vector3(3.2f, 1f, 1.2f), S(Pal.WoodLight));
                    for (int k = 0; k < 5; k++)   // jackets & shawls hanging
                        mb.Box(new Vector3(x - 1.4f + k * 0.7f, 1.9f, z + side * 1.25f), new Vector3(0.5f, 0.75f, 0.06f),
                               S(new[] { Pal.JacketRed, Pal.JacketBlue, Pal.Shawl, Pal.CapWool, Pal.JacketGreen, Pal.SareePink }[rng.Next(6)]));
                    mb.QuadDouble(new Vector3(x - 1.9f, 2.8f, z + side * 1.5f), new Vector3(x + 1.9f, 2.8f, z + side * 1.5f),
                                  new Vector3(x + 1.9f, 2.4f, z - side * 0.9f), new Vector3(x - 1.9f, 2.4f, z - side * 0.9f), new Rect(aw, Vector2.zero), Vector3.up);
                }
            }
            FlagPoles(mb, new Vector3(-14f, 0, 0), new Vector3(14f, 0, 0));
        }

        static void PashanDevi(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(7, 0, 6), Mathf.Min(depth, 2.5f), M(Atlas.MatStone));
            Vector2 red = S(Pal.TempleRed), orange = M(Atlas.MatTempleOrange), gold = M(Atlas.MatGold);
            mb.Sphere(new Vector3(0, 0.2f, -1.6f), new Vector3(2.2f, 2.0f, 1.4f), 7, 4, M(Atlas.MatDarkStone), 0f, 90f);   // the rock
            mb.Box(new Vector3(0, 1.2f, -0.5f), new Vector3(1.4f, 1.6f, 0.2f), orange);    // sindoor-painted face of the rock
            foreach (float x in new[] { -1.6f, 1.6f }) mb.Cylinder(new Vector3(x, 0, 1.2f), 0.12f, 0.12f, 2.6f, 6, red, true);
            mb.Box(new Vector3(0, 2.7f, 0.4f), new Vector3(4f, 0.18f, 2.6f), red);
            for (int i = -2; i <= 2; i++) mb.Sphere(new Vector3(i * 0.5f, 2.3f, 1.2f), Vector3.one * 0.1f, 6, 3, gold);
            mb.Limb(new Vector3(2.4f, 0, -1f), new Vector3(2.4f, 4.5f, -1f), 0.04f, 0.03f, 4, S(Pal.WoodDark));
            mb.Tri(new Vector3(2.4f, 4.4f, -1f), new Vector3(2.4f, 3.6f, -1f), new Vector3(3.8f, 4f, -1f), S(Pal.TempleRed), Vector3.forward);
            mb.Tri(new Vector3(2.4f, 4.4f, -1f), new Vector3(2.4f, 3.6f, -1f), new Vector3(3.8f, 4f, -1f), S(Pal.TempleRed), Vector3.back);
        }

        static void BusStand(MeshBuilder mb, float depth)
        {
            Plinth(mb, new Vector3(34, 0, 22), Mathf.Min(depth, 2f), M(Atlas.MatConcrete));
            for (int i = 0; i < 5; i++) mb.Cylinder(new Vector3(-12f + i * 6f, 0, 9f), 0.15f, 0.15f, 3.4f, 6, S(Pal.IronGreen), true);
            mb.Box(new Vector3(0, 3.5f, 8f), new Vector3(28f, 0.2f, 4.5f), Atlas.SolidUV(Pal.BoatGreen));
            mb.Box(new Vector3(0, 0.45f, 9.5f), new Vector3(20f, 0.1f, 0.6f), S(Pal.WoodLight));
            mb.Box(new Vector3(0, 4.1f, 10.2f), new Vector3(8f, 1f, 0.15f), S(Pal.Saffron));    // "Tallital Bus Stand" board
        }

        static void BuildExtras(WorldCtx ctx, Transform parent, System.Random rng)
        {
            Vector2 lc = ctx.lakeCenter;
            // (Tibetan market: by Naina Devi's gate, see WorldBuilder.Mallital.cs)
            // Pashan Devi temple on Thandi Sadak
            foreach (var r in ctx.world.roads)
            {
                if (string.IsNullOrEmpty(r.name) || r.name.IndexOf("Thandi", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var pts = Geo.ToPoints(r.pts);
                Vector2 mid = pts[pts.Length / 2];
                Vector2 away = (mid - lc).normalized;
                var site = MakeSite(ctx, mid + away * 9f, mid, 4f, 6f);
                var mb = new MeshBuilder { M = Matrix4x4.TRS(site.origin, site.rot, Vector3.one) };
                PashanDevi(mb, site.depth);
                Emit(ctx, "Pashan Devi Mandir", site, mb, parent);
                ctx.InfoSign("Pashan Devi Mandir", "Thandi Sadak par chattan mein bana chhota sa mandir - chattan par sindoor se Maa ki aakriti. Shaant aur pavitra jagah.",
                             site.origin + site.rot * new Vector3(3.5f, 0, 3f), site.rot, parent);
                ctx.Zone("Pashan Devi Mandir", site.origin, 25f);
                ctx.landmarkSites["Pashan Devi Mandir"] = site.origin;
                break;
            }
            // (Tallital bus stand: see WorldBuilder.Tallital.cs)
            // prayer flags at viewpoints
            var flagMb = new MeshBuilder();
            foreach (var key in new[] { "Snow View", "Naini Peak (China Peak)", "Tiffin Top (Dorothy's Seat)", "Land's End", "Himalaya Darshan", "Lover's Point", "Khurpatal View Point" })
            {
                if (!ctx.landmarkSites.TryGetValue(key, out var c)) continue;
                Vector3 a = c + new Vector3(-6f, 0, -5f), b = c + new Vector3(7f, 0, -4f);
                a.y = ctx.H(new Vector2(a.x, a.z));
                b.y = ctx.H(new Vector2(b.x, b.z));
                FlagPoles(flagMb, a, b);
                Vector3 b2 = c + new Vector3(2f, 0, 7f);
                b2.y = ctx.H(new Vector2(b2.x, b2.z));
                FlagPoles(flagMb, b, b2);
            }
            ctx.MeshObject("Prayer flags", parent, flagMb, ctx.mats.atlas, null, false);
        }

        /// <summary>Lower station near the Flats, cable with pylons up to Snow View, moving cabin.</summary>
        static void BuildRopeway(WorldCtx ctx, Transform parent)
        {
            if (!ctx.landmarkSites.TryGetValue("Snow View ropeway", out var upper) && !ctx.landmarkSites.TryGetValue("Snow View", out upper)) return;
            var station = ctx.Poi("Ropeway Mallital station");   // Google Maps (2026-11-11)
            Vector2 lowerP = station ?? ctx.PushFromLake(ctx.mallitalShore + ctx.mallitalAlong * -45f, 22f);
            Vector3 lower = new Vector3(lowerP.x, ctx.H(lowerP), lowerP.y);
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            var st = new BuildingKit.Style { wall = 1, roof = 2, flatRoof = true, litChance = 0.8f };
            Vector2 dir2 = new Vector2(upper.x - lower.x, upper.z - lower.z).normalized;
            float yaw = Mathf.Atan2(dir2.x, dir2.y) * Mathf.Rad2Deg;
            BuildingKit.House(mb, col, lowerP, 10f, 7f, yaw, 2, ctx.H(lowerP) - 1f, ctx.H(lowerP), st, new System.Random(9));
            Vector3 a = lower + Vector3.up * 7.5f, b = upper + Vector3.up * 5.5f;
            var pts = new List<Vector3> { a };
            // add pylons where the straight cable would come too close to the ground
            for (int pass = 0; pass < 3; pass++)
            {
                float worst = 0f; Vector3 worstP = default; int worstSeg = -1;
                var all = new List<Vector3>(pts) { b };
                for (int sgi = 0; sgi < all.Count - 1; sgi++)
                    for (int k = 1; k < 40; k++)
                    {
                        Vector3 q = Vector3.Lerp(all[sgi], all[sgi + 1], k / 40f);
                        float clearance = q.y - ctx.H(new Vector2(q.x, q.z));
                        if (12f - clearance > worst) { worst = 12f - clearance; worstP = q; worstSeg = sgi; }
                    }
                if (worstSeg < 0) break;
                Vector3 top = new Vector3(worstP.x, ctx.H(new Vector2(worstP.x, worstP.z)) + 16f, worstP.z);
                pts.Insert(worstSeg + 1, top);
                mb.Limb(new Vector3(top.x, top.y - 16f, top.z), top, 0.5f, 0.3f, 6, S(Pal.StoneGrey));
                mb.Box(top + Vector3.up * 0.2f, new Vector3(3f, 0.4f, 0.6f), Quaternion.Euler(0, yaw + 90f, 0), S(Pal.StoneGrey));
            }
            pts.Add(b);
            Vector3 side = Quaternion.Euler(0, yaw + 90f, 0) * Vector3.forward * 1.2f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                mb.Limb(pts[i] + side, pts[i + 1] + side, 0.04f, 0.04f, 4, S(Pal.IronBlack), false);
                mb.Limb(pts[i] - side, pts[i + 1] - side, 0.04f, 0.04f, 4, S(Pal.IronBlack), false);
            }
            ctx.MeshObject("Ropeway (Mallital - Snow View)", parent, mb, ctx.mats.atlas, col);

            // the cabin (2025 video): red, windows all round, a white roof, hung from a yoke under a carriage on the cables
            var cab = new MeshBuilder();
            Vector2 red = S(Pal.JacketRed), glassUV = M(Atlas.MatGlass), steel = S(Pal.StoneGrey);
            cab.Box(new Vector3(0, -2.75f, 0), new Vector3(2.2f, 0.9f, 2.6f), red);                 // lower body
            cab.Box(new Vector3(0, -1.55f, 0), new Vector3(2.2f, 0.12f, 2.6f), red);                // the band over the windows
            foreach (float x in new[] { -1.04f, 1.04f })
                foreach (float z in new[] { -1.24f, 1.24f }) cab.Box(new Vector3(x, -1.95f, z), new Vector3(0.12f, 0.8f, 0.12f), red);   // corner pillars
            cab.Box(new Vector3(0, -1.95f, 1.28f), new Vector3(1.96f, 0.78f, 0.03f), glassUV);
            cab.Box(new Vector3(0, -1.95f, -1.28f), new Vector3(1.96f, 0.78f, 0.03f), glassUV);
            cab.Box(new Vector3(1.08f, -1.95f, 0), new Vector3(0.03f, 0.78f, 2.36f), glassUV);
            cab.Box(new Vector3(-1.08f, -1.95f, 0), new Vector3(0.03f, 0.78f, 2.36f), glassUV);
            cab.Box(new Vector3(0, -1.4f, 0), new Vector3(2.35f, 0.18f, 2.75f), S(Pal.SailWhite));   // white roof
            cab.Limb(new Vector3(-0.5f, -1.3f, 0), new Vector3(-0.15f, -0.25f, 0), 0.05f, 0.05f, 5, steel);   // the yoke
            cab.Limb(new Vector3(0.5f, -1.3f, 0), new Vector3(0.15f, -0.25f, 0), 0.05f, 0.05f, 5, steel);
            cab.Box(new Vector3(0, -0.15f, 0), new Vector3(0.5f, 0.35f, 1.6f), steel);               // the carriage on the cables
            foreach (float z in new[] { -0.6f, 0.6f }) cab.Limb(new Vector3(-0.3f, 0.02f, z), new Vector3(0.3f, 0.02f, z), 0.12f, 0.12f, 8, S(Pal.IronBlack));
            var cabinGo = ctx.MeshObject("Ropeway Cabin", parent, cab, ctx.mats.atlas, null, true, false);
            var rw = cabinGo.AddComponent<Ropeway>();
            rw.cabin = cabinGo.transform;
            rw.cable = pts.ToArray();
            cabinGo.AddComponent<RopewayRide>();   // E at a station: ride it (ticket Rs 300)
            cabinGo.transform.position = pts[0];
            ctx.Zone("Ropeway (Mallital)", lower, 25f);
            RopewayMarket(ctx, parent, lowerP, -dir2, new System.Random(390));
        }
    }
}

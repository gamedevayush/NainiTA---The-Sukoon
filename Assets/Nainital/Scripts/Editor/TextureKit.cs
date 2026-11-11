using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Procedural textures + materials (no external art needed).</summary>
    public static class TextureKit
    {
        public class Materials
        {
            public Material atlas, asphalt, paver, dirtPath, water, sky, terrain, marker, rain;
            public TerrainLayer[] layers;
        }

        // ------------------------------------------------------------------ noise
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float VNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int Wrap(int v) => ((v % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Tileable fBm; u,v in [0,1).</summary>
        static float Fbm(float u, float v, int baseFreq, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int f = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += VNoise(u * f, v * f, f, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
                f *= 2;
            }
            return sum / norm;
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        // ------------------------------------------------------------------ file helpers
        static Texture2D SavePng(Texture2D tex, string path, bool repeat, bool srgb = true, int maxSize = 1024, bool boxMips = false, bool normalMap = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.mipmapEnabled = true;
            imp.mipmapFilter = boxMips ? TextureImporterMipFilter.BoxFilter : TextureImporterMipFilter.KaiserFilter;
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;   // facades are seen along the street, at a glancing angle
            imp.maxTextureSize = maxSize;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = Mathf.Min(maxSize, 2048);
            android.format = TextureImporterFormat.ASTC_4x4;
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Configure an already-present PBR texture (from Tools/process_textures.py).</summary>
        static Texture2D ImportPbr(string path, bool normal, int maxSize = 1024)
        {
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 4;
            imp.maxTextureSize = maxSize;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = maxSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Normal map for the atlas, embossed from luminance inside each cell (windows recessed, frames raised, tin ridges).</summary>
        static Texture2D AtlasNormal(Color32[] px)
        {
            int S = Atlas.Size, C = Atlas.CellPx, R = Mathf.Max(1, C / 128);   // sample distance either side
            float scale = (C / 64f) / R * 0.8f;                                 // the same relief as the 64 px atlas had
            var n = new Color32[S * S];
            float L(int x, int y) { var c = px[y * S + x]; return (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f; }
            System.Threading.Tasks.Parallel.For(0, S, y =>
            {
                for (int x = 0; x < S; x++)
                {
                    int cx = x / C, cy = y / C;
                    bool textured = cy <= Atlas.RoofRow && !(cy < Atlas.RoofRow && cx >= Atlas.ExtraCol0);
                    if (!textured) { n[y * S + x] = new Color32(128, 128, 255, 255); continue; }
                    int x0 = cx * C, y0 = cy * C;
                    int xl = Mathf.Max(x0, x - R), xr = Mathf.Min(x0 + C - 1, x + R), yd = Mathf.Max(y0, y - R), yu = Mathf.Min(y0 + C - 1, y + R);
                    float strength = (cy == Atlas.RoofRow && cx < Atlas.RoofColors ? 5f : 3.2f) * scale;
                    float dx = (L(xr, y) - L(xl, y)) * strength, dy = (L(x, yu) - L(x, yd)) * strength;
                    var v = new Vector3(-dx, -dy, 1f).normalized;
                    n[y * S + x] = new Color32((byte)(v.x * 127.5f + 127.5f), (byte)(v.y * 127.5f + 127.5f), (byte)(v.z * 127.5f + 127.5f), 255);
                }
            });
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false, true);
            t.SetPixels32(n);
            t.Apply(false);
            return t;
        }

        /// <summary>Detail mask (alpha): plaster micro-detail on walls/roofs/stone, none on solid palette colours. One
        /// value a cell, so a small texture does.</summary>
        static Texture2D AtlasDetailMask()
        {
            const int S = 1024;
            int C = S / Atlas.Cells;
            var m = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int cx = x / C, cy = y / C;
                    bool on = cy < Atlas.RoofRow ? cx < Atlas.ExtraCol0 : cy == Atlas.RoofRow && cx != Atlas.MatGlass && cx != Atlas.MatGold;
                    m[y * S + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
                }
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false, true);
            t.SetPixels32(m);
            t.Apply(false);
            return t;
        }

        static T SaveAsset<T>(T obj, string path) where T : Object
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        // ------------------------------------------------------------------ atlas
        /// <summary>Fill one atlas cell from f(u, v): cell units, 0..64 across and up, whatever the cell's pixel size.</summary>
        static void Cell(Color32[] px, int col, int row, System.Func<float, float, Color> f)
        {
            int cp = Atlas.CellPx;
            float k = 64f / cp;
            for (int y = 0; y < cp; y++)
                for (int x = 0; x < cp; x++)
                {
                    Color c = f((x + 0.5f) * k, (y + 0.5f) * k);
                    c.a = 1f;
                    px[(row * cp + y) * Atlas.Size + col * cp + x] = c;
                }
        }

        /// <summary>Fill one atlas cell from f(x, y) in the cell's own pixels (photo textures at full detail).</summary>
        static void CellPx(Color32[] px, int col, int row, System.Func<int, int, Color> f)
        {
            int cp = Atlas.CellPx;
            for (int y = 0; y < cp; y++)
                for (int x = 0; x < cp; x++)
                {
                    Color c = f(x, y);
                    c.a = 1f;
                    px[(row * cp + y) * Atlas.Size + col * cp + x] = c;
                }
        }

        static bool InRect(int x, int y, int x0, int y0, int x1, int y1) => x >= x0 && x < x1 && y >= y0 && y < y1;

        // ---- CC0 Poly Haven photo textures (Tools/fetch_textures.py + process_textures.py), box-filtered down for the atlas
        static readonly System.Collections.Generic.Dictionary<string, (Color[] px, float mean)> photoCache =
            new System.Collections.Generic.Dictionary<string, (Color[], float)>();

        static (Color[] px, float mean) Photo(string name, int size)
        {
            string key = name + "@" + size;
            if (photoCache.TryGetValue(key, out var hit)) return hit;
            string path = "Assets/Nainital/Data/PBR/" + name + "_albedo.png";
            if (!System.IO.File.Exists(path)) return photoCache[key] = (null, 0f);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(System.IO.File.ReadAllBytes(path));
            var src = tex.GetPixels();
            int w = tex.width, h = tex.height, bx = Mathf.Max(1, w / size), by = Mathf.Max(1, h / size);
            var dst = new Color[size * size];
            float sum = 0f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color acc = Color.clear;
                    for (int j = 0; j < by; j++)
                        for (int i = 0; i < bx; i++) acc += src[Mathf.Min(h - 1, y * by + j) * w + Mathf.Min(w - 1, x * bx + i)];
                    acc /= bx * by;
                    acc.a = 1f;
                    dst[y * size + x] = acc;
                    sum += acc.grayscale;
                }
            Object.DestroyImmediate(tex);
            return photoCache[key] = (dst, sum / (size * size));
        }

        /// <summary>Photo texture colour, tiled.</summary>
        static Color PhotoRgb(string name, int size, int x, int y, Color fallback)
        {
            var p = Photo(name, size);
            if (p.px == null) return fallback;
            return p.px[((y % size + size) % size) * size + ((x % size + size) % size)];
        }

        /// <summary>Photo luminance around its mean (about -0.3..0.3), tiled; 0 when the texture is missing.</summary>
        internal static float PhotoDev(string name, int size, int x, int y)
        {
            var p = Photo(name, size);
            if (p.px == null) return 0f;
            return p.px[((y % size + size) % size) * size + ((x % size + size) % size)].grayscale - p.mean;
        }

        static readonly Color Glass = new Color(0.12f, 0.16f, 0.21f);

        static Color32[] lastAtlasPixels;

        /// <summary>The atlas: facades painted by FacadeArt (windows, doors, balconies, shops, verandahs on weathered
        /// plaster), tin roofs, photo materials, and the solid palettes.</summary>
        static void BuildAtlas(out Texture2D baseTex, out Texture2D emisTex)
        {
            int S = Atlas.Size, C = Atlas.CellPx;
            float k = 64f / C;
            var px = new Color32[S * S];
            var em = new Color32[S * S];
            var black = new Color32(0, 0, 0, 255);
            for (int i = 0; i < em.Length; i++) em[i] = black;

            // the photo scans are read before the worker threads start (the cache must not fill while they run)
            Photo("plastered_wall_04", 256);
            Photo("corrugated_iron_02", 256);
            FacadeArt.Dev = PhotoDev;
            FacadeArt.Rgb = PhotoRgb;
            // atlas styles: WinALit 0, WinADark 1, WinBLit 2, WinBDark 3, ShopLit 4, ShopDark 5, Plain 6, Door 7, BalconyLit 8, BalconyDark 9
            int[] kinds = { FacadeArt.WinA, FacadeArt.WinA, FacadeArt.WinB, FacadeArt.WinB, FacadeArt.Shop, FacadeArt.Shop,
                            FacadeArt.Plain, FacadeArt.Door, FacadeArt.Balcony, FacadeArt.Balcony };
            var walls = new Color[Atlas.WallColors];
            for (int w = 0; w < Atlas.WallColors; w++)
            {
                Color wc = Atlas.WallPalette[w];
                wc = Color.Lerp(new Color(wc.grayscale, wc.grayscale, wc.grayscale * 0.97f), wc, 0.62f) * 0.96f;   // faded paint
                wc.a = 1f;
                walls[w] = wc;
            }
            int extras = Atlas.Extra.Length;
            System.Threading.Tasks.Parallel.For(0, Atlas.WallColors * Atlas.Cells, job =>
            {
                int w = job / Atlas.Cells, s = job % Atlas.Cells;
                if (s >= Atlas.ExtraCol0 && (11 - w) * 6 + (s - Atlas.ExtraCol0) < extras) return;   // a solid extra colour, below
                Color wc = walls[w];
                bool facade = s < Atlas.StyleCount;
                bool lit = s == Atlas.StyleWinALit || s == Atlas.StyleWinBLit || s == Atlas.StyleShopLit || s == Atlas.StyleBalconyLit;
                int seed = w * 31 + s * 7;
                for (int y = 0; y < C; y++)
                    for (int x = 0; x < C; x++)
                    {
                        float u = (x + 0.5f) * k, v = (y + 0.5f) * k;
                        Color e = Color.black;
                        Color c = facade ? FacadeArt.Bay(kinds[s], lit, w, wc, u, v, seed, out e) : FacadeArt.Plaster(wc, u, v, w * 13 + s);
                        c.a = 1f; e.a = 1f;
                        int idx = (w * C + y) * S + s * C + x;
                        px[idx] = c;
                        em[idx] = e;
                    }
            });

            // Extra solid colours (cars, flags, grass ...) in columns 10..15 of rows 11..0
            for (int i = 0; i < Atlas.Extra.Length; i++)
            {
                Color ec = Atlas.Extra[i];
                int col = Atlas.ExtraCol0 + i % 6, row = 11 - i / 6;
                Cell(px, col, row, (u, v) => ec);
                bool glow = Atlas.ExtraEmissive(i);
                Cell(em, col, row, (u, v) => glow ? ec * 1.2f : Color.black);
            }

            // Row 12: corrugated tin roofs + surface materials
            for (int r = 0; r < Atlas.RoofColors; r++)
            {
                Color rc = Atlas.RoofPalette[r];
                int rr = r;
                Cell(px, r, Atlas.RoofRow, (u, v) => FacadeArt.TinRoof(rc, u, v, rr * 5 + 1));
            }
            bool stonePhoto = Photo("old_stone_wall", C).px != null;
            CellPx(px, Atlas.MatStone, Atlas.RoofRow, (x, y) =>
            {
                if (stonePhoto) return Mul(PhotoRgb("old_stone_wall", C, x, y, Color.grey), 1.05f);
                int ux = (int)(x * k), uy = (int)(y * k), row = uy / 10, off = (row % 2) * 8;
                bool mortar = uy % 10 == 0 || (ux + off) % 16 == 0;
                return mortar ? new Color(0.62f, 0.6f, 0.56f) : Mul(new Color(0.5f, 0.49f, 0.46f), 0.8f + 0.35f * Hash((ux + off) / 16, row, 7));
            });
            CellPx(px, Atlas.MatDarkStone, Atlas.RoofRow, (x, y) =>
            {
                if (stonePhoto) return Mul(PhotoRgb("old_stone_wall", C, x + C / 3, y + C / 5, Color.grey), 0.62f);
                int ux = (int)(x * k), uy = (int)(y * k), row = uy / 12, off = (row % 2) * 10;
                bool mortar = uy % 12 == 0 || (ux + off) % 20 == 0;
                return mortar ? new Color(0.35f, 0.34f, 0.33f) : Mul(new Color(0.3f, 0.29f, 0.28f), 0.8f + 0.4f * Hash((ux + off) / 20, row, 9));
            });
            bool woodPhoto = Photo("brown_planks_04", C).px != null;
            CellPx(px, Atlas.MatWood, Atlas.RoofRow, (x, y) =>
            {
                if (woodPhoto) return PhotoRgb("brown_planks_04", C, x, y, Color.grey);
                float u = x * k, v = y * k;
                bool gap = (int)v % 8 == 0;
                return gap ? new Color(0.2f, 0.13f, 0.08f) : Mul(new Color(0.5f, 0.34f, 0.2f), 0.8f + 0.35f * Fbm(u / 64f, v / 256f, 16, 2, 11));
            });
            CellPx(px, Atlas.MatConcrete, Atlas.RoofRow, (x, y) => PhotoRgb("concrete_wall_008", C, x, y, Mul(new Color(0.62f, 0.61f, 0.58f), 0.85f + 0.2f * Fbm(x * k / 64f, y * k / 64f, 8, 3, 13))));
            Cell(px, Atlas.MatTempleOrange, Atlas.RoofRow, (u, v) => Mul(new Color(0.95f, 0.5f, 0.12f), 0.9f + 0.12f * Fbm(u / 64f, v / 64f, 4, 2, 15)));
            Cell(px, Atlas.MatGold, Atlas.RoofRow, (u, v) => Mul(new Color(0.95f, 0.72f, 0.22f), 0.85f + 0.3f * Fbm(u / 64f, v / 64f, 8, 2, 17)));
            Cell(px, Atlas.MatMarble, Atlas.RoofRow, (u, v) => Mul(new Color(0.95f, 0.94f, 0.9f), 0.93f + 0.07f * Fbm(u / 64f, v / 64f, 4, 4, 19)));
            Cell(px, Atlas.MatGlass, Atlas.RoofRow, (u, v) => Color.Lerp(Glass, Glass * 1.8f, v / 64f));
            Cell(px, Atlas.MatBlack, Atlas.RoofRow, (u, v) => new Color(0.06f, 0.06f, 0.07f));

            // Rows 13..15: solid palette
            for (int i = 0; i < Atlas.Solid.Length; i++)
            {
                Color sc = Atlas.Solid[i];
                int col = i % Atlas.Cells, row = Atlas.PaletteRow + i / Atlas.Cells;
                Cell(px, col, row, (u, v) => sc);
                if (Atlas.SolidEmissive(i))
                    Cell(em, col, row, (u, v) => Color.white);
            }

            lastAtlasPixels = px;
            baseTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            baseTex.SetPixels32(px);
            baseTex.Apply(false);
            emisTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            emisTex.SetPixels32(em);
            emisTex.Apply(false);
        }

        // ------------------------------------------------------------------ tiling textures
        /// <summary>alpha = smoothness for URP terrain layers, so terrain textures use ~0 (matte).</summary>
        static Texture2D Tiling(int size, System.Func<float, float, Color> f, float alpha = 1f)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var c = f(x / (float)size, y / (float)size);
                    c.a = alpha;
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply(false);
            return t;
        }

        static Color Grass(float u, float v)
        {
            float n = Fbm(u, v, 8, 5, 101), m = Fbm(u, v, 32, 3, 102);
            Color a = new Color(0.25f, 0.38f, 0.14f), b = new Color(0.40f, 0.48f, 0.20f), dry = new Color(0.52f, 0.47f, 0.28f);
            Color c = Color.Lerp(a, b, n);
            c = Color.Lerp(c, dry, Mathf.Clamp01((Fbm(u, v, 4, 3, 103) - 0.6f) * 2.5f));
            return Mul(c, 0.85f + 0.3f * m);
        }

        static Color Forest(float u, float v)
        {
            float n = Fbm(u, v, 8, 5, 201), needles = Fbm(u, v, 64, 2, 202);
            Color soil = new Color(0.22f, 0.17f, 0.11f), moss = new Color(0.17f, 0.25f, 0.11f), needle = new Color(0.38f, 0.26f, 0.14f);
            Color c = Color.Lerp(soil, moss, n);
            c = Color.Lerp(c, needle, Mathf.Clamp01((needles - 0.55f) * 3f));
            return Mul(c, 0.85f + 0.3f * Fbm(u, v, 16, 2, 203));
        }

        static Color Rock(float u, float v)
        {
            float n = Fbm(u, v, 4, 6, 301);
            float crack = Mathf.Abs(Fbm(u, v, 8, 3, 302) - 0.5f);
            Color c = Color.Lerp(new Color(0.36f, 0.34f, 0.31f), new Color(0.56f, 0.53f, 0.48f), n);
            if (crack < 0.03f) c = Mul(c, 0.6f);
            return Color.Lerp(c, new Color(0.3f, 0.36f, 0.2f), Mathf.Clamp01((Fbm(u, v, 8, 3, 303) - 0.65f) * 2f));
        }

        static Color Dirt(float u, float v)
        {
            float n = Fbm(u, v, 8, 5, 401), pebble = Fbm(u, v, 64, 2, 402);
            Color c = Color.Lerp(new Color(0.40f, 0.31f, 0.21f), new Color(0.55f, 0.45f, 0.33f), n);
            if (pebble > 0.7f) c = Mul(c, 1.25f);
            return c;
        }

        static Color Asphalt(float u, float v)
        {
            float n = Fbm(u, v, 16, 4, 501), grit = Hash((int)(u * 256), (int)(v * 256), 502);
            Color c = Mul(new Color(0.2f, 0.2f, 0.21f), 0.85f + 0.3f * n + 0.1f * grit);
            if (u < 0.04f || u > 0.96f) c = Color.Lerp(c, new Color(0.45f, 0.42f, 0.38f), 0.6f); // worn edges
            return c;
        }

        static Color Paver(float u, float v)
        {
            int bx = Mathf.FloorToInt(u * 8), by = Mathf.FloorToInt(v * 8);
            float fx = u * 8 - bx, fy = v * 8 - by;
            bool joint = fx < 0.06f || fy < 0.06f;
            float n = Hash(bx, by, 601);
            Color c = Color.Lerp(new Color(0.55f, 0.5f, 0.45f), new Color(0.68f, 0.62f, 0.55f), n);
            return joint ? new Color(0.3f, 0.28f, 0.25f) : Mul(c, 0.9f + 0.15f * Fbm(u, v, 16, 2, 602));
        }

        // ------------------------------------------------------------------ public entry
        public static Materials Create(string genRoot)
        {
            string tex = genRoot + "/Textures", mat = genRoot + "/Materials";
            Directory.CreateDirectory(tex);
            Directory.CreateDirectory(mat);

            BuildAtlas(out var aBase, out var aEm);
            var atlasBase = SavePng(aBase, tex + "/Atlas_Base.png", false, true, Atlas.Size, true);
            var atlasEm = SavePng(aEm, tex + "/Atlas_Emission.png", false, true, Mathf.Min(Atlas.Size, 2048), true);   // lit windows: half size is plenty

            var grass = SavePng(Tiling(512, Grass, 0.04f), tex + "/T_Grass.png", true);
            var forest = SavePng(Tiling(512, Forest, 0.02f), tex + "/T_ForestFloor.png", true);
            var rock = SavePng(Tiling(512, Rock, 0.1f), tex + "/T_Rock.png", true);
            var dirt = SavePng(Tiling(512, Dirt, 0.03f), tex + "/T_Dirt.png", true);
            var asphalt = SavePng(Tiling(256, Asphalt), tex + "/T_Asphalt.png", true, true, 256);
            var paver = SavePng(Tiling(256, Paver), tex + "/T_Paver.png", true, true, 256);

            // ---- PBR upgrade (CC0 Poly Haven photo textures prepared by Tools/process_textures.py)
            string pbr = "Assets/Nainital/Data/PBR/";
            Texture2D P(string n) => ImportPbr(pbr + n + "_albedo.png", false);
            Texture2D PN(string n) => ImportPbr(pbr + n + "_normal.png", true);
            var atlasNormalTex = AtlasNormal(lastAtlasPixels);
            var atlasNormal = SavePng(atlasNormalTex, tex + "/Atlas_Normal.png", false, false, Atlas.Size, true, true);
            lastAtlasPixels = null;
            var atlasMask = SavePng(AtlasDetailMask(), tex + "/Atlas_DetailMask.png", false, false, 1024, true);
            var plasterDetail = ImportPbr(pbr + "plaster_detail.png", false, 512);
            var plasterNormal = PN("white_rough_plaster");
            if (plasterDetail)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(pbr + "plaster_detail.png");
                imp.sRGBTexture = false;
                imp.SaveAndReimport();
            }

            var m = new Materials();
            var simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var weathered = Shader.Find("Nainital/AtlasLit");   // Lit + dulled paint and world-space grime (SurfaceLook)
            m.atlas = SaveAsset(new Material(weathered ? weathered : lit) { name = "M_Atlas" }, mat + "/M_Atlas.mat");
            m.atlas.SetTexture("_BaseMap", atlasBase);
            m.atlas.SetColor("_BaseColor", Color.white);
            m.atlas.SetTexture("_EmissionMap", atlasEm);
            m.atlas.SetColor("_EmissionColor", new Color(0.004f, 0.003f, 0.002f));   // non-black keeps _EMISSION alive
            m.atlas.EnableKeyword("_EMISSION");
            m.atlas.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.atlas.SetFloat("_Smoothness", 0.22f);
            m.atlas.SetFloat("_Metallic", 0f);
            m.atlas.SetFloat("_EnvironmentReflections", 1f);
            m.atlas.SetTexture("_BumpMap", atlasNormal);
            m.atlas.SetFloat("_BumpScale", 1f);
            m.atlas.EnableKeyword("_NORMALMAP");
            if (plasterDetail && plasterNormal)
            {
                m.atlas.SetTexture("_DetailMask", atlasMask);
                m.atlas.SetTexture("_DetailAlbedoMap", plasterDetail);
                m.atlas.SetTextureScale("_DetailAlbedoMap", new Vector2(48f, 48f));
                m.atlas.SetFloat("_DetailAlbedoMapScale", 0.35f);
                m.atlas.SetTexture("_DetailNormalMap", plasterNormal);
                m.atlas.SetFloat("_DetailNormalMapScale", 0.7f);
                m.atlas.EnableKeyword("_DETAIL_MULX2");
            }
            m.atlas.enableInstancing = true;
            EditorUtility.SetDirty(m.atlas);   // saved with it: the grass draws the atlas instanced (was lost on reload: black frame)

            Material Tiled(string name, Texture2D t, float smooth, Texture2D albedoPbr = null, Texture2D normalPbr = null, float tiling = 1f)
            {
                var mm = SaveAsset(new Material(lit) { name = name }, mat + "/" + name + ".mat");
                mm.SetTexture("_BaseMap", albedoPbr ? albedoPbr : t);
                mm.SetColor("_BaseColor", Color.white);
                mm.SetFloat("_Smoothness", albedoPbr ? 0.65f : smooth);   // scales the albedo-alpha smoothness; rain pushes it to ~1
                if (albedoPbr)
                {
                    mm.SetFloat("_SmoothnessTextureChannel", 1f);   // smoothness from albedo alpha
                    mm.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                    mm.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
                }
                if (normalPbr)
                {
                    mm.SetTexture("_BumpMap", normalPbr);
                    mm.EnableKeyword("_NORMALMAP");
                }
                mm.enableInstancing = true;
                return mm;
            }
            m.asphalt = Tiled("M_Asphalt", asphalt, 0.18f, P("asphalt_02"), PN("asphalt_02"), 1f);
            m.paver = Tiled("M_Paver", paver, 0.12f, P("rectangular_paving"), PN("rectangular_paving"), 1f);
            m.dirtPath = Tiled("M_DirtPath", dirt, 0.05f, P("dirt"), PN("dirt"), 1f);

            Material Transparent(string name, Shader sh, Color c, bool additive)
            {
                var mm = SaveAsset(new Material(sh) { name = name }, mat + "/" + name + ".mat");
                mm.SetColor("_BaseColor", c);
                mm.SetFloat("_Surface", 1f);
                mm.SetFloat("_Blend", additive ? 2f : 0f);
                mm.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mm.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                mm.SetFloat("_ZWrite", 0f);
                mm.SetFloat("_Cull", 0f);
                mm.SetOverrideTag("RenderType", "Transparent");
                mm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (additive) mm.EnableKeyword("_BLENDMODE_ADD");
                mm.renderQueue = 3000;
                return mm;
            }
            m.marker = Transparent("M_Marker", Shader.Find("Universal Render Pipeline/Unlit"), new Color(1f, 0.85f, 0.2f, 0.35f), false);
            var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            m.rain = Transparent("M_Rain", particleShader, new Color(0.8f, 0.86f, 1f, 0.5f), false);

            m.water = SaveAsset(new Material(Shader.Find("Nainital/Water")) { name = "M_Water" }, mat + "/M_Water.mat");
            m.water.SetColor("_ShallowColor", new Color(0.34f, 0.46f, 0.56f));   // deep, dark blue lake, not turquoise
            m.water.SetVector("_Absorption", new Vector4(0.62f, 0.26f, 0.16f, 0f));
            m.water.SetFloat("_Clarity", 0.75f);
            m.water.SetColor("_DeepColor", new Color(0.006f, 0.024f, 0.055f));
            m.sky = SaveAsset(new Material(Shader.Find("Nainital/Sky")) { name = "M_Sky" }, mat + "/M_Sky.mat");
            m.terrain = SaveAsset(new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "M_Terrain" }, mat + "/M_Terrain.mat");

            // tint: the photo grass/forest scans are autumn-brown, Kumaon hills are lush green
            TerrainLayer Layer(string name, Texture2D t, float tile, string pbrName, Color tint)
            {
                var albedo = P(pbrName);
                var normal = PN(pbrName);
                var l = new TerrainLayer { diffuseTexture = albedo ? albedo : t, tileSize = new Vector2(tile, tile), smoothness = 0f, metallic = 0f, name = name };
                if (albedo) { l.diffuseRemapMin = Vector4.zero; l.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f); }
                if (normal) { l.normalMapTexture = normal; l.normalScale = 0.9f; }
                return SaveAsset(l, mat + "/" + name + ".terrainlayer");
            }
            m.layers = new[]
            {
                // re-textured (user, 2026-11-11: "nainital pe texturing"): the old lawn-green grass and green-tinted forest floor
                // read as a golf course. Real ground in the 2025-26 videos: dry mixed grass in the open, dark leaf litter under
                // the oaks and deodars, grey stony soil on cut banks, packed brown earth in town. Tints only mute the scans.
                Layer("TL_Grass", grass, 7f, P("forrest_ground_01") ? "forrest_ground_01" : "leafy_grass", P("forrest_ground_01") ? new Color(0.60f, 0.83f, 0.64f) : new Color(0.38f, 0.56f, 0.32f)),
                Layer("TL_ForestFloor", forest, 8f, P("forest_leaves_02") ? "forest_leaves_02" : "forest_ground_04", P("forest_leaves_02") ? new Color(0.56f, 0.66f, 0.84f) : new Color(0.33f, 0.47f, 0.26f)),
                Layer("TL_Rock", rock, 9f, P("rocky_trail") ? "rocky_trail" : "rock_ground_02", new Color(0.82f, 0.86f, 0.92f)),
                Layer("TL_Dirt", dirt, 6f, "forest_ground_04", new Color(0.80f, 0.80f, 0.80f)),   // Kumaon earth is dark grey-brown, not tan
            };
            AssetDatabase.SaveAssets();
            return m;
        }
    }
}

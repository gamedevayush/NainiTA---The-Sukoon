using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Layout of the shared texture atlas (16 x 16 cells of CellPx pixels) used by
    /// buildings, props, trees and characters. One material for (almost) everything keeps
    /// draw calls low on phones.
    /// Rows 0..11  : building wall colours, columns = facade styles (windows / shop / door ...)
    /// Row 12      : roofs (cols 0..6) and surface materials (cols 7..15)
    /// Rows 13..15 : 48 solid palette colours
    /// </summary>
    public static class Atlas
    {
        public const int Cells = 16;
        public const int CellPx = 256;   // 4096 atlas: a 3.2 m bay at 1.25 cm a pixel (was 64: a window was a white cross)
        public const int Size = Cells * CellPx;
        const float InsetPx = 2.5f * CellPx / 64f;   // the same margin in UV at any atlas size

        public const int WallColors = 12;
        public const int StyleWinALit = 0, StyleWinADark = 1, StyleWinBLit = 2, StyleWinBDark = 3,
                         StyleShopLit = 4, StyleShopDark = 5, StylePlain = 6, StyleDoor = 7,
                         StyleBalconyLit = 8, StyleBalconyDark = 9;
        public const int StyleCount = 10;

        public const int RoofRow = 12;
        public const int RoofColors = 7;
        public const int MatStone = 7, MatDarkStone = 8, MatWood = 9, MatConcrete = 10, MatTempleOrange = 11,
                         MatGold = 12, MatMarble = 13, MatGlass = 14, MatBlack = 15;

        public const int PaletteRow = 13;

        public static readonly Color[] WallPalette =
        {
            new Color(0.93f, 0.92f, 0.88f), // white
            new Color(0.93f, 0.87f, 0.72f), // cream
            new Color(0.95f, 0.85f, 0.50f), // light yellow
            new Color(0.93f, 0.72f, 0.72f), // pale pink
            new Color(0.62f, 0.78f, 0.88f), // sky blue
            new Color(0.70f, 0.84f, 0.66f), // light green
            new Color(0.96f, 0.74f, 0.56f), // peach
            new Color(0.62f, 0.61f, 0.58f), // grey stone
            new Color(0.78f, 0.72f, 0.86f), // lavender
            new Color(0.68f, 0.33f, 0.26f), // brick red
            new Color(0.85f, 0.64f, 0.25f), // mustard
            new Color(0.30f, 0.58f, 0.58f), // teal
        };

        public static readonly Color[] RoofPalette =
        {
            new Color(0.62f, 0.13f, 0.10f), // red tin
            new Color(0.42f, 0.10f, 0.12f), // maroon
            new Color(0.17f, 0.45f, 0.22f), // green
            new Color(0.10f, 0.28f, 0.16f), // dark green
            new Color(0.33f, 0.40f, 0.48f), // blue grey
            new Color(0.45f, 0.26f, 0.14f), // rust
            new Color(0.48f, 0.48f, 0.47f), // grey
        };

        /// <summary>48 solid colours (see <see cref="Pal"/>).</summary>
        public static readonly Color[] Solid =
        {
            new Color(0.87f, 0.70f, 0.56f), new Color(0.76f, 0.57f, 0.42f), new Color(0.58f, 0.41f, 0.29f),
            new Color(0.08f, 0.07f, 0.07f), new Color(0.25f, 0.16f, 0.10f), new Color(0.62f, 0.12f, 0.12f),
            new Color(0.15f, 0.25f, 0.50f), new Color(0.20f, 0.38f, 0.22f), new Color(0.85f, 0.65f, 0.15f),
            new Color(0.12f, 0.12f, 0.13f), new Color(0.20f, 0.27f, 0.42f), new Color(0.55f, 0.48f, 0.33f),
            new Color(0.10f, 0.10f, 0.11f), new Color(0.28f, 0.18f, 0.11f), new Color(0.85f, 0.85f, 0.82f),
            new Color(0.90f, 0.90f, 0.88f), new Color(0.92f, 0.45f, 0.10f), new Color(0.85f, 0.30f, 0.55f),
            new Color(0.55f, 0.20f, 0.30f), new Color(0.30f, 0.30f, 0.55f), new Color(0.30f, 0.21f, 0.14f),
            new Color(0.10f, 0.23f, 0.14f), new Color(0.14f, 0.30f, 0.17f), new Color(0.24f, 0.36f, 0.16f),
            new Color(0.20f, 0.36f, 0.15f), new Color(0.28f, 0.44f, 0.18f), new Color(0.09f, 0.20f, 0.12f),
            new Color(0.10f, 0.20f, 0.15f), new Color(0.07f, 0.07f, 0.08f), new Color(1.00f, 0.85f, 0.55f),
            new Color(0.75f, 0.15f, 0.12f), new Color(0.12f, 0.35f, 0.70f), new Color(0.95f, 0.75f, 0.10f),
            new Color(0.10f, 0.55f, 0.30f), new Color(0.95f, 0.95f, 0.92f), new Color(0.85f, 0.20f, 0.20f),
            new Color(0.62f, 0.45f, 0.28f), new Color(0.35f, 0.23f, 0.14f), new Color(1.00f, 0.55f, 0.05f),
            new Color(0.95f, 0.72f, 0.20f), new Color(0.95f, 0.93f, 0.88f), new Color(0.75f, 0.15f, 0.10f),
            new Color(0.52f, 0.52f, 0.50f), new Color(0.65f, 0.64f, 0.60f), new Color(0.06f, 0.06f, 0.07f),
            new Color(0.30f, 0.48f, 0.20f), new Color(1.00f, 0.78f, 0.45f), new Color(0.15f, 0.45f, 0.65f),
        };

        /// <summary>Solid palette slots that glow at night.</summary>
        public static bool SolidEmissive(int i) => i == Pal.LampBulb || i == Pal.WindowGlow;

        public static Rect CellRect(int col, int row)
        {
            float s = 1f / Cells, inset = InsetPx / Size;
            return new Rect(col * s + inset, row * s + inset, s - 2 * inset, s - 2 * inset);
        }

        public static Vector2 CellCenter(int col, int row) => new Vector2((col + 0.5f) / Cells, (row + 0.5f) / Cells);

        public static Vector2 SolidUV(int paletteIndex) =>
            CellCenter(paletteIndex % Cells, PaletteRow + paletteIndex / Cells);

        // ---- Extra solid colours live in the unused facade columns 10..15 of rows 11..0 (up to 72 slots).
        public const int ExtraCol0 = 10;

        public static readonly Color[] Extra =
        {
            new Color(0.95f, 0.08f, 0.06f), // 0 tail light (emissive)
            new Color(1.00f, 0.60f, 0.08f), // 1 indicator amber (emissive)
            new Color(0.92f, 0.92f, 0.90f), // 2 car white
            new Color(0.62f, 0.64f, 0.66f), // 3 car silver
            new Color(0.70f, 0.06f, 0.07f), // 4 car red
            new Color(0.08f, 0.22f, 0.55f), // 5 car blue
            new Color(0.05f, 0.05f, 0.06f), // 6 car black
            new Color(0.98f, 0.80f, 0.10f), // 7 taxi yellow
            new Color(0.12f, 0.40f, 0.22f), // 8 car green
            new Color(0.04f, 0.04f, 0.045f),// 9 tyre
            new Color(0.72f, 0.73f, 0.75f), // 10 rim
            new Color(0.85f, 0.86f, 0.88f), // 11 chrome
            new Color(0.10f, 0.30f, 0.62f), // 12 bus blue
            new Color(0.94f, 0.94f, 0.92f), // 13 bus white
            new Color(0.97f, 0.88f, 0.20f), // 14 number plate (commercial yellow)
            new Color(0.07f, 0.09f, 0.12f), // 15 tinted car glass
            new Color(0.10f, 0.35f, 0.85f), // 16 prayer flag blue
            new Color(0.97f, 0.97f, 0.97f), // 17 prayer flag white
            new Color(0.88f, 0.10f, 0.10f), // 18 prayer flag red
            new Color(0.10f, 0.62f, 0.25f), // 19 prayer flag green
            new Color(0.98f, 0.85f, 0.10f), // 20 prayer flag yellow
            new Color(0.45f, 0.38f, 0.30f), // 21 monkey fur
            new Color(0.85f, 0.55f, 0.50f), // 22 monkey face
            new Color(0.33f, 0.52f, 0.20f), // 23 grass blade
            new Color(0.50f, 0.55f, 0.24f), // 24 dry grass blade
            new Color(1.00f, 0.95f, 0.80f), // 25 headlight (emissive)
            new Color(0.55f, 0.42f, 0.30f), // 26 bronze statue
            new Color(0.80f, 0.12f, 0.35f), // 27 bougainvillea pink
        };

        public static bool ExtraEmissive(int i) => i == Px.TailLight || i == Px.Indicator || i == Px.Headlight;

        public static Vector2 ExtraUV(int i) => CellCenter(ExtraCol0 + i % 6, 11 - i / 6);

        public static Rect Facade(int wallColor, int style) => CellRect(style, wallColor);
        public static Vector2 WallSolid(int wallColor) => CellCenter(StylePlain, wallColor);
        public static Rect Roof(int roofColor) => CellRect(roofColor, RoofRow);
        public static Rect Material(int mat) => CellRect(mat, RoofRow);
        public static Vector2 MaterialUV(int mat) => CellCenter(mat, RoofRow);
    }

    /// <summary>Indices into <see cref="Atlas.Solid"/>.</summary>
    public static class Pal
    {
        public const int SkinLight = 0, SkinMid = 1, SkinDark = 2, HairBlack = 3, HairBrown = 4,
            JacketRed = 5, JacketBlue = 6, JacketGreen = 7, JacketYellow = 8, JacketBlack = 9,
            Jeans = 10, PantsKhaki = 11, PantsBlack = 12, ShoeBrown = 13, ShoeWhite = 14, ShirtWhite = 15,
            SareeOrange = 16, SareePink = 17, Shawl = 18, CapWool = 19, Trunk = 20, Deodar = 21, DeodarLight = 22,
            Pine = 23, Oak = 24, OakLight = 25, Cypress = 26, IronGreen = 27, IronBlack = 28, LampBulb = 29,
            BoatRed = 30, BoatBlue = 31, BoatYellow = 32, BoatGreen = 33, SailWhite = 34, Canopy = 35,
            WoodLight = 36, WoodDark = 37, Saffron = 38, Gold = 39, TempleWhite = 40, TempleRed = 41,
            StoneGrey = 42, Concrete = 43, WaterTank = 44, Grass = 45, WindowGlow = 46, Awning = 47;
    }

    /// <summary>Indices into <see cref="Atlas.Extra"/> (use with Atlas.ExtraUV).</summary>
    public static class Px
    {
        public const int TailLight = 0, Indicator = 1, CarWhite = 2, CarSilver = 3, CarRed = 4, CarBlue = 5, CarBlack = 6,
            TaxiYellow = 7, CarGreen = 8, Tyre = 9, Rim = 10, Chrome = 11, BusBlue = 12, BusWhite = 13, Plate = 14,
            CarGlass = 15, FlagBlue = 16, FlagWhite = 17, FlagRed = 18, FlagGreen = 19, FlagYellow = 20,
            MonkeyFur = 21, MonkeyFace = 22, GrassBlade = 23, GrassDry = 24, Headlight = 25, Bronze = 26, Bougainvillea = 27;
    }
}

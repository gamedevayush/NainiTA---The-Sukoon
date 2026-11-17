using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// How the fake interiors look, one setting for every room (user, 2026-11-18: "tool jisse main set kr sku, pos and
    /// zoom wagera sab, for all"). Two groups - Mall Road's shops (ShopGlass) and the other ground floors (house windows,
    /// bazaar shops: AtlasLit) - each with the photo's size on its back plane (bigger = the room looks smaller, further),
    /// the height of the photo's middle above the floor, a sideways shift and the back-plane depths; then the light.
    /// Lives in Resources/InteriorLook.asset; sent to the shaders (NainitalRooms.hlsl) as globals at start, and live from
    /// the editor window Nainital -> Fake Interior Look (or this asset's inspector).
    /// </summary>
    [CreateAssetMenu(menuName = "Nainital/Interior Look", fileName = ResourceName)]
    public class InteriorLook : ScriptableObject
    {
        public const string ResourceName = "InteriorLook";

        [Header("Mall Road shops (glass and open fronts, kiosks)")]
        [Tooltip("Photo size on its back plane, metres. More = the room looks smaller / further.")]
        public float mallSize = 4.8f;
        [Tooltip("Height of the photo's middle above the floor, metres. Less = everything lower.")]
        public float mallMid = 1.1f;
        [Tooltip("Sideways shift of the photo, metres. + = to the right as seen from the street.")]
        public float mallSide = 0f;
        [Tooltip("How far behind the glass the room is, metres (restaurants, cafes, banks, hotels).")]
        public float mallGlassDepth = 3.0f;
        [Tooltip("How far behind an open shop front or a kiosk window the room is, metres.")]
        public float mallOpenDepth = 3.4f;
        [Tooltip("Photo stretched sideways (1 = as shot).")]
        public float mallStretchX = 1f;
        [Tooltip("Photo stretched up and down (1 = as shot).")]
        public float mallStretchY = 1f;
        [Tooltip("The glass / opening's width as a share of the shop's front (1 = all of it). Outside it: a panel.")]
        [Range(0.2f, 1f)] public float mallOpenWidth = 1f;
        [Tooltip("Where the opening starts above the floor, metres (0 = from the floor).")]
        public float mallSill = 0f;
        [Tooltip("Where the opening ends above the floor, metres (2.75 = the whole front).")]
        public float mallTop = 2.75f;
        [Tooltip("The panel's colour where the opening has been made smaller.")]
        public Color mallPanel = new Color(0.36f, 0.25f, 0.16f);

        [Header("Other ground floors (house windows, bazaar shops)")]
        public float baySize = 4.8f;
        public float bayMid = 1.1f;
        public float baySide = 0f;
        [Tooltip("Behind a window, metres.")]
        public float bayWindowDepth = 2.6f;
        [Tooltip("Behind an open bazaar shop, metres.")]
        public float bayShopDepth = 3.2f;
        public float bayStretchX = 1f;
        public float bayStretchY = 1f;

        [Header("Light")]
        [Tooltip("How much of the sun lights the room by day.")]
        public float dayLight = 0.22f;
        [Tooltip("Light that is always there.")]
        public float ambient = 0.03f;
        [Tooltip("How strongly a lit window / shop glows at night.")]
        public float nightLight = 2.4f;
        [Tooltip("Mall Road shops' own tube lights by day.")]
        public float shopDayLight = 0.13f;
        [Tooltip("At a slant the room stops sliding after this many metres.")]
        public float maxSlide = 4.5f;

        static readonly int Set = Shader.PropertyToID("_NtLookSet");
        static readonly int MallSize = Shader.PropertyToID("_NtMallSize"), MallMid = Shader.PropertyToID("_NtMallMid"),
                            MallSide = Shader.PropertyToID("_NtMallSide"), MallGlass = Shader.PropertyToID("_NtMallGlassDepth"),
                            MallOpen = Shader.PropertyToID("_NtMallOpenDepth");
        static readonly int BaySize = Shader.PropertyToID("_NtBaySize"), BayMid = Shader.PropertyToID("_NtBayMid"),
                            BaySide = Shader.PropertyToID("_NtBaySide"), BayWindow = Shader.PropertyToID("_NtBayWindowDepth"),
                            BayShop = Shader.PropertyToID("_NtBayShopDepth");
        static readonly int Day = Shader.PropertyToID("_NtRoomDay"), Amb = Shader.PropertyToID("_NtRoomAmbient"),
                            Night = Shader.PropertyToID("_NtRoomNight"), ShopDay = Shader.PropertyToID("_NtShopDayLight"),
                            Slide = Shader.PropertyToID("_NtMaxSlide");
        static readonly int MallSX = Shader.PropertyToID("_NtMallStretchX"), MallSY = Shader.PropertyToID("_NtMallStretchY"),
                            BaySX = Shader.PropertyToID("_NtBayStretchX"), BaySY = Shader.PropertyToID("_NtBayStretchY"),
                            OpenW = Shader.PropertyToID("_NtMallOpenW"), Sill = Shader.PropertyToID("_NtMallSill"),
                            Top = Shader.PropertyToID("_NtMallTop"), Panel = Shader.PropertyToID("_NtMallPanel");

        /// <summary>Send this look to every room (shader globals).</summary>
        public void Apply()
        {
            Shader.SetGlobalFloat(MallSize, mallSize);
            Shader.SetGlobalFloat(MallMid, mallMid);
            Shader.SetGlobalFloat(MallSide, mallSide);
            Shader.SetGlobalFloat(MallGlass, mallGlassDepth);
            Shader.SetGlobalFloat(MallOpen, mallOpenDepth);
            Shader.SetGlobalFloat(BaySize, baySize);
            Shader.SetGlobalFloat(BayMid, bayMid);
            Shader.SetGlobalFloat(BaySide, baySide);
            Shader.SetGlobalFloat(BayWindow, bayWindowDepth);
            Shader.SetGlobalFloat(BayShop, bayShopDepth);
            Shader.SetGlobalFloat(Day, dayLight);
            Shader.SetGlobalFloat(Amb, ambient);
            Shader.SetGlobalFloat(Night, nightLight);
            Shader.SetGlobalFloat(ShopDay, shopDayLight);
            Shader.SetGlobalFloat(Slide, maxSlide);
            Shader.SetGlobalFloat(MallSX, mallStretchX);
            Shader.SetGlobalFloat(MallSY, mallStretchY);
            Shader.SetGlobalFloat(BaySX, bayStretchX);
            Shader.SetGlobalFloat(BaySY, bayStretchY);
            Shader.SetGlobalFloat(OpenW, mallOpenWidth);
            Shader.SetGlobalFloat(Sill, mallSill);
            Shader.SetGlobalFloat(Top, mallTop);
            Shader.SetGlobalColor(Panel, mallPanel);
            Shader.SetGlobalFloat(Set, 1f);
        }

        /// <summary>The saved look (Resources/InteriorLook), or the defaults when there is none.</summary>
        public static InteriorLook Load()
        {
            var a = Resources.Load<InteriorLook>(ResourceName);
            return a ? a : CreateInstance<InteriorLook>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => Load().Apply();

        void OnValidate() => Apply();   // inspector edits show at once
    }
}

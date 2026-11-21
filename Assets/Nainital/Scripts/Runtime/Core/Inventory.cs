using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// What the player owns (owner, 21 Nov 2026: "everything bought goes to the house"). Every shop item bought to keep
    /// lands here (Shop.cs, ShopUI.cs); the house shows them (Owned, NameKey). Outfits are items too (ids start with
    /// "outfit_"); the one worn changes the player's clothes (OutfitLook). Saved with PlayerPrefs: "nt_inv" (ids joined by
    /// '|', in the order bought) and "nt_outfit". A new game clears both (SaveGame.Clear -> ResetAll).
    ///
    /// Owned holds each id once (buying the same thing again is not offered: the shop shows "at home"). The default
    /// outfit is always worn-able and is not in Owned (it was never bought). Things eaten on the spot and services
    /// (a phone recharge) never come here. Ids carry their kind as a prefix - see ShopCatalog.Kind.
    /// </summary>
    public static class Inventory
    {
        public const string DefaultOutfit = "outfit_default";
        const string KeyOwned = "nt_inv", KeyWorn = "nt_outfit";

        static readonly List<string> owned = new List<string>();
        static string worn = DefaultOutfit;
        static bool loaded;

        /// <summary>Every item bought, in the order bought (each id once; not the default outfit).</summary>
        public static IReadOnlyList<string> Owned { get { Load(); return owned; } }

        public static bool Has(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            Load();
            return id == DefaultOutfit || owned.Contains(id);
        }

        /// <summary>Puts an item in the house (no money is taken here: Shop does GameState.TrySpend first).</summary>
        public static void Add(string id)
        {
            if (string.IsNullOrEmpty(id) || id == DefaultOutfit) return;
            Load();
            if (owned.Contains(id)) return;
            owned.Add(id);
            Save();
            Changed?.Invoke();
        }

        /// <summary>The item's name as a Hinglish Loc key (show it with Loc.T). Unknown ids come back as they are.</summary>
        public static string NameKey(string id)
        {
            var it = ShopCatalog.Get(id);
            return it != null ? it.key : id;
        }

        public static string WornOutfit { get { Load(); return worn; } }

        /// <summary>Wears an owned outfit (or the default) and changes the player's look at once.</summary>
        public static void Wear(string outfitId)
        {
            if (string.IsNullOrEmpty(outfitId) || !outfitId.StartsWith("outfit_") || !Has(outfitId)) return;
            Load();
            if (worn != outfitId)
            {
                worn = outfitId;
                Save();
            }
            OutfitLook.Apply();
            Changed?.Invoke();
        }

        /// <summary>The outfits the player can wear: the default first, then the bought ones.</summary>
        public static IEnumerable<string> Outfits()
        {
            Load();
            yield return DefaultOutfit;
            for (int i = 0; i < owned.Count; i++)
                if (owned[i].StartsWith("outfit_")) yield return owned[i];
        }

        public static event System.Action Changed;

        /// <summary>A new game: nothing owned, the default clothes back on.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyOwned);
            PlayerPrefs.DeleteKey(KeyWorn);
            owned.Clear();
            worn = DefaultOutfit;
            loaded = true;
            OutfitLook.Apply();
            Changed?.Invoke();
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            owned.Clear();
            foreach (var s in PlayerPrefs.GetString(KeyOwned, "").Split('|'))
                if (!string.IsNullOrEmpty(s) && !owned.Contains(s)) owned.Add(s);
            worn = PlayerPrefs.GetString(KeyWorn, DefaultOutfit);
            if (worn != DefaultOutfit && !owned.Contains(worn)) worn = DefaultOutfit;
        }

        static void Save()
        {
            PlayerPrefs.SetString(KeyOwned, string.Join("|", owned));
            PlayerPrefs.SetString(KeyWorn, worn);
            PlayerPrefs.Save();
        }
    }

    /// <summary>One thing a shop or a restaurant sells.</summary>
    public sealed class ShopItem
    {
        public string id;
        /// <summary>Hinglish Loc key of its name.</summary>
        public string key;
        public int price;
        /// <summary>Eaten / taken on the spot: PlayerStats.Eat(food, health), never kept.</summary>
        public bool now;
        /// <summary>A service (phone recharge): paid, a toast, nothing kept.</summary>
        public bool service;
        public float food, health;
        public bool Keep => !now && !service;
    }

    /// <summary>
    /// Every item in the game's shops and restaurants. The id's prefix is its kind (for the house: where to put it):
    /// outfit_ (clothes, worn), wool_ (woollens), sweet_ (sweet boxes), candle_, umbrella_ / bag_ (rain gear, bags),
    /// med_ (medicines kept), gadget_ (phone things), souv_ / toy_ (souvenirs, toys), book_, bakery_, fruit_, gym_.
    /// Kept items cost what the shops on Mall Road ask; food eaten now and restaurant meals never reach Inventory.
    /// </summary>
    public static class ShopCatalog
    {
        static Dictionary<string, ShopItem> items;

        public static ShopItem Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (items == null) Fill();
            return items.TryGetValue(id, out var it) ? it : null;
        }

        /// <summary>The kind of an item: its id up to the first '_' ("outfit", "candle" ...).</summary>
        public static string Kind(string id)
        {
            int k = string.IsNullOrEmpty(id) ? -1 : id.IndexOf('_');
            return k > 0 ? id.Substring(0, k) : id ?? "";
        }

        static void K(string id, string key, int price) => items[id] = new ShopItem { id = id, key = key, price = price };
        static void N(string id, string key, int price, float food, float health) => items[id] = new ShopItem { id = id, key = key, price = price, now = true, food = food, health = health };
        static void S(string id, string key, int price) => items[id] = new ShopItem { id = id, key = key, price = price, service = true };

        static void Fill()
        {
            items = new Dictionary<string, ShopItem>();
            // ---- outfits (OutfitLook): the default is the player's own black puffer and jeans
            K(Inventory.DefaultOutfit, "Kaala puffer jacket (apna)", 0);
            K("outfit_maroon", "Maroon puffer jacket", 1500);
            K("outfit_mustard", "Sarson peela puffer jacket", 1500);
            K("outfit_olive", "Fauji hara jacket, khaki pant", 2000);
            K("outfit_pahadi", "Neela jacket aur Pahadi topi", 1200);
            K("outfit_winter", "Slaty jacket aur oon ki topi", 1100);
            // ---- woollens
            K("wool_shawl", "Kumaoni shawl", 1200);
            K("wool_sweater", "Haath ka bunaa sweater", 900);
            K("wool_muffler", "Oon ka muffler", 250);
            K("wool_gloves", "Oon ke dastaane", 150);
            K("wool_socks", "Oon ke moze", 120);
            K("wool_blanket", "Bhed ki oon ka kambal", 1600);
            // ---- sweets
            K("sweet_balmithai", "Bal Mithai ka dibba", 250);
            K("sweet_singori", "Singori ka dibba", 200);
            N("now_balmithai", "Bal Mithai abhi khao", 40, 0.12f, 6f);
            N("now_jalebi", "Garam jalebi abhi khao", 50, 0.18f, 8f);
            // ---- candles
            K("candle_rose", "Gulab wali candle", 120);
            K("candle_jar", "Kaanch ke jar wali candle", 250);
            K("candle_lake", "Naini jheel wali candle", 450);
            K("candle_set", "Rangeen candles ka set", 350);
            // ---- rain gear, bags
            K("umbrella_black", "Kaala chhaata", 300);
            K("umbrella_rainbow", "Rangeen chhaata", 450);
            K("umbrella_raincoat", "Barsaati (raincoat)", 650);
            K("bag_backpack", "Trekking backpack", 900);
            // ---- medical
            K("med_firstaid", "First aid kit", 300);
            K("med_balm", "Dard ka balm", 60);
            K("med_vicks", "Sardi-zukaam ki dawai", 90);
            N("now_painkiller", "Dard ki goli abhi lo", 50, 0f, 35f);
            N("now_ors", "ORS ghol abhi piyo", 30, 0.05f, 15f);
            // ---- phone
            S("svc_recharge_small", "Recharge chhota pack", 199);
            S("svc_recharge_big", "Recharge mahine ka pack", 349);
            K("gadget_earphones", "Earphones", 499);
            K("gadget_powerbank", "Power bank", 999);
            K("gadget_cover", "Phone cover (Naini wala)", 199);
            // ---- souvenirs and toys
            K("toy_monkey", "Bandar wala teddy", 250);
            K("souv_boat", "Lakdi ki naav (model)", 350);
            K("souv_magnet", "Nainital fridge magnet", 80);
            K("souv_snowglobe", "Naini jheel ka snow globe", 300);
            K("toy_ropeway", "Khilona ropeway trolley", 400);
            K("souv_bell", "Pital ki ghanti", 220);
            // ---- books
            K("book_corbett", "Jim Corbett ki shikaar kahaniyan", 350);
            K("book_trek", "Kumaon trekking guide", 300);
            K("book_comics", "Comics ka set", 150);
            K("book_diary", "Naini diary aur pen", 120);
            // ---- bakery
            K("bakery_plumcake", "Plum cake", 400);
            K("bakery_cookies", "Atta biscuits ka dabba", 180);
            K("bakery_bread", "Brown bread", 60);
            N("now_patties", "Garam patties abhi khao", 30, 0.2f, 6f);
            N("now_pastry", "Pastry abhi khao", 70, 0.18f, 5f);
            // ---- fruit
            K("fruit_basket", "Phalon ki tokri", 350);
            K("fruit_apples", "Ramgarh ke seb (1 kilo)", 160);
            N("now_banana", "Kele abhi khao", 40, 0.2f, 8f);
            N("now_peach", "Aadu aur plum abhi khao", 60, 0.18f, 8f);
            // ---- gym gear
            K("gym_dumbbells", "Dumbbell ka joda", 1200);
            K("gym_mat", "Yoga / gym mat", 600);
            K("gym_shoes", "Running shoes", 1800);
            K("gym_bottle", "Shaker bottle", 250);
            // ---- restaurant meals (eaten at the table: Restaurant.cs)
            N("meal_chai", "Adrak wali chai", 20, 0.05f, 4f);
            N("meal_coffee", "Garam coffee", 60, 0.06f, 5f);
            N("meal_maggi", "Masala Maggi", 60, 0.3f, 12f);
            N("meal_momos", "Steam momos", 80, 0.35f, 14f);
            N("meal_thukpa", "Thukpa", 120, 0.45f, 18f);
            N("meal_pizza", "Pizza", 250, 0.6f, 22f);
            N("meal_thali", "Pahadi thali", 180, 0.85f, 30f);
            N("meal_paratha", "Aloo paratha", 90, 0.45f, 16f);
            N("meal_chole", "Chole bhature", 120, 0.55f, 20f);
            N("meal_rajma", "Rajma chawal", 130, 0.6f, 22f);
            N("meal_sandwich", "Grilled sandwich", 100, 0.35f, 12f);
            N("meal_pakode", "Pakode", 70, 0.3f, 10f);
            N("meal_bhatt", "Bhatt ki churkani aur bhaat", 150, 0.65f, 24f);
            N("meal_softy", "Softy ice cream", 50, 0.1f, 4f);
        }
    }

    /// <summary>
    /// The worn outfit on the player: the player's own Tripo model with a recoloured texture (Resources/Outfits/&lt;id&gt;,
    /// made from the model's own texture by Tools/outfits/make_outfits.py: only the jacket, trousers and shoes change, the
    /// face and hair stay his), plus a cap for the Pahadi and the winter outfits (a few dozen triangles on the head bone).
    /// The default outfit puts the original material back. Applied when the scene starts (OutfitBoot) and on Wear().
    /// </summary>
    public static class OutfitLook
    {
        enum Cap { None, Topi, Beanie }

        static (string tex, Cap cap) Def(string id)
        {
            switch (id)
            {
                case "outfit_maroon": return ("Outfits/outfit_maroon", Cap.None);
                case "outfit_mustard": return ("Outfits/outfit_mustard", Cap.None);
                case "outfit_olive": return ("Outfits/outfit_olive", Cap.None);
                case "outfit_pahadi": return ("Outfits/outfit_pahadi", Cap.Topi);
                case "outfit_winter": return ("Outfits/outfit_winter", Cap.Beanie);
                default: return (null, Cap.None);
            }
        }

        static HumanoidRig forRig;
        static Renderer[] rends;
        static Material[][] originals;
        static Material inst;
        static Texture2D tex;
        static GameObject capGo;
        static Cap capKind;
        static readonly Dictionary<Cap, Mesh> capMeshes = new Dictionary<Cap, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            forRig = null;
            if (PlayerController.Instance) new GameObject("Outfit Boot").AddComponent<OutfitBoot>();
        }

        public static void Apply()
        {
            var p = PlayerController.Instance;
            var rig = p ? p.Rig : null;
            if (!rig || !rig.Model) return;
            if (forRig != rig)
            {
                forRig = rig;
                rends = rig.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                originals = new Material[rends.Length][];
                for (int i = 0; i < rends.Length; i++) originals[i] = rends[i].sharedMaterials;
                capGo = null;
                capKind = Cap.None;
            }
            var (texPath, cap) = Def(Inventory.WornOutfit);

            // the clothes: a copy of the model's material with the outfit's texture
            Texture2D next = texPath != null ? Resources.Load<Texture2D>(texPath) : null;
            if (texPath != null && !next) Debug.LogWarning("[Nainital] outfit texture missing: Resources/" + texPath + " (Tools/outfits/make_outfits.py)");
            Material baseMat = originals.Length > 0 && originals[0].Length > 0 ? originals[0][0] : null;
            if (next && baseMat)
            {
                if (!inst) inst = new Material(baseMat) { name = baseMat.name + " (outfit)" };
                if (inst.HasProperty("_BaseMap")) inst.SetTexture("_BaseMap", next);
                inst.mainTexture = next;
                for (int i = 0; i < rends.Length; i++)
                {
                    if (!rends[i]) continue;
                    var m = (Material[])originals[i].Clone();
                    for (int k = 0; k < m.Length; k++) if (m[k] == baseMat) m[k] = inst;
                    rends[i].sharedMaterials = m;
                }
            }
            else
                for (int i = 0; i < rends.Length; i++) if (rends[i]) rends[i].sharedMaterials = originals[i];
            if (tex && tex != next) Resources.UnloadAsset(tex);
            tex = next;

            // the cap
            if (capKind != cap || (cap != Cap.None && !capGo))
            {
                if (capGo) Object.Destroy(capGo);
                capGo = null;
                capKind = cap;
                if (cap != Cap.None) capGo = MakeCap(rig, cap);
            }
        }

        /// <summary>A cap on the head: its rim on the crown, from the model's bounds (the top of the hair) and the head
        /// bone (the head's middle), placed while standing and then carried by the head bone.</summary>
        static GameObject MakeCap(HumanoidRig rig, Cap cap)
        {
            var head = rig.Head;
            if (!head) return null;
            float top = float.MinValue;
            foreach (var r in rends) if (r) top = Mathf.Max(top, r.bounds.max.y);
            if (top == float.MinValue) return null;
            Vector3 fwd = Vector3.ProjectOnPlane(rig.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 crown = new Vector3(head.position.x, top, head.position.z) - fwd * 0.012f;
            if (!capMeshes.TryGetValue(cap, out var mesh) || !mesh) capMeshes[cap] = mesh = CapMesh(cap);
            var go = new GameObject(cap == Cap.Topi ? "Pahadi topi" : "Oon ki topi") { layer = rig.gameObject.layer };
            go.transform.SetPositionAndRotation(crown, Quaternion.LookRotation(fwd, Vector3.up));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GameAssets.AtlasMaterial;
            go.transform.SetParent(head, true);
            return go;
        }

        /// <summary>Origin = the top of the hair, +z forward. Topi: the Kumaoni cap, black with a coloured band, a little
        /// longer than wide. Beanie: a red woollen cap with a folded brim and a pom-pom.</summary>
        static Mesh CapMesh(Cap cap)
        {
            var mb = new MeshBuilder();
            if (cap == Cap.Topi)
            {
                Vector2 body = Atlas.SolidUV(Pal.HairBlack), band = Atlas.SolidUV(Pal.BoatGreen);
                mb.Cylinder(new Vector3(0, -0.075f, 0), 0.1f, 0.098f, 0.1f, 14, body, true, false, 1.12f);
                mb.Cylinder(new Vector3(0, -0.078f, 0), 0.102f, 0.102f, 0.022f, 14, band, false, false, 1.12f);
            }
            else
            {
                Vector2 wool = Atlas.SolidUV(Pal.CapWool), brim = Atlas.SolidUV(Pal.ShirtWhite);
                mb.Sphere(new Vector3(0, -0.06f, 0), new Vector3(0.1f, 0.09f, 0.112f), 12, 4, wool, 0f, 90f);
                mb.Cylinder(new Vector3(0, -0.095f, 0), 0.103f, 0.102f, 0.04f, 12, wool, false, false, 1.12f);
                mb.Cylinder(new Vector3(0, -0.098f, 0), 0.106f, 0.105f, 0.018f, 12, brim, false, false, 1.12f);
                mb.Sphere(new Vector3(0, 0.035f, 0), new Vector3(0.03f, 0.028f, 0.03f), 6, 4, brim);
            }
            return mb.ToMesh(cap == Cap.Topi ? "Topi" : "Beanie");
        }
    }

    /// <summary>Puts the saved outfit on the player once the rig has built (the frame after the scene loads).</summary>
    public class OutfitBoot : MonoBehaviour
    {
        void Start()
        {
            OutfitLook.Apply();
            Destroy(gameObject);
        }
    }
}
